using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Momentum.Application.Abstractions.Sync;
using Momentum.Domain.Sync;
using Momentum.Infrastructure.Persistence;
using EntityState = Momentum.Domain.Sync.EntityState;

namespace Momentum.Infrastructure.Sync;

/// <summary>
/// Pull side — RAW SQL (GOREV slice-2b1 D5/D6). Incremental changes are bounded by the
/// <c>pg_snapshot_xmin</c> horizon and ordered by <c>(commit_xid, server_seq)</c>; the full snapshot
/// captures the horizon in one REPEATABLE READ txn and returns the <c>(horizon, 0)</c> continuation.
/// </summary>
public sealed class SyncPuller(SyncDbContext db) : ISyncPuller
{
    private const int PageSize = 500;

    /// <summary>
    /// IS-EMRI-o86-D2 D-DUZELTME (K-o89/5): iki horizonun MAX'ini alip TEK esige sokan onceki
    /// mantik (o86-D) KALKTI -- iki horizonun esik semantigi FARKLIDIR, tek bir karsilastirmaya
    /// SIKISTIRILAMAZ:
    /// <list type="bullet">
    /// <item>GC horizonu: bugunku gibi -- <see cref="ResyncPolicy.ShouldResync"/> (KESIN kucuktur,
    /// <c>since &lt; gc</c>). Saf politika DEGISMEDI (DUR noktasi 1).</item>
    /// <item>Kullanici horizonu: <c>since &lt;= userHorizon</c> (KAPSAYICI). Gerekce: horizon TAM
    /// SINIRA yazilir (<c>pg_current_xact_id()</c>, D2) VE uyenin imleci de snapshot'tan AYNI
    /// sinira (<c>pg_snapshot_xmin</c>) gelebilir -- ikisi ESIT cikabilir (olculdu: 753==753, o86-D
    /// bagimsiz denetimi). Kesin-kucuktur bu esitlikte KACIRIR; kapsayici KACIRMAZ. Daveti ZATEN
    /// gormus bir istemcinin imleci `(X, seq&gt;=1)` olur ⇒ `since &lt;= (X,0)` FALSE ⇒ gereksiz
    /// resync YOK -- imleci tam `(X,0)` olan istemci yalniz BIR KEZ fazladan snapshot alir,
    /// zararsiz.</item>
    /// </list>
    /// Sonuc ikisinin VEYA'sidir -- `g &gt; u ? g : u` karsilastirmasi ve `SyncCursor` uzerindeki
    /// `&gt;` bagimliligi bu satirdan KALKTI.
    /// </summary>
    public async Task<bool> ShouldResyncAsync(Guid actorId, SyncCursor since, CancellationToken cancellationToken)
    {
        var gcHorizon = await ReadGcHorizonAsync(cancellationToken);
        var gcResync = gcHorizon is { } gc && ResyncPolicy.ShouldResync(since, gc);

        var userHorizon = await ReadUserHorizonAsync(actorId, cancellationToken);
        var userResync = userHorizon is { } u && since <= u;

        return gcResync || userResync;
    }

    private async Task<SyncCursor?> ReadGcHorizonAsync(CancellationToken cancellationToken)
    {
        await using var command = await db.CreateRawCommandAsync(
            "SELECT gc_horizon_xid::text, gc_horizon_seq FROM sync_gc_state WHERE id = 1", cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0))
        {
            return null; // no GC horizon set
        }

        return new SyncCursor(ulong.Parse(reader.GetString(0), CultureInfo.InvariantCulture), reader.GetInt64(1));
    }

    private async Task<SyncCursor?> ReadUserHorizonAsync(Guid actorId, CancellationToken cancellationToken)
    {
        await using var command = await db.CreateRawCommandAsync(
            "SELECT horizon_xid::text, horizon_seq FROM user_resync_horizon WHERE user_id = @actorId", cancellationToken);
        command.Parameters.AddWithValue("actorId", actorId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0))
        {
            return null; // no join-backfill debt for this user
        }

        return new SyncCursor(ulong.Parse(reader.GetString(0), CultureInfo.InvariantCulture), reader.GetInt64(1));
    }

    public async Task<PullPage> PullIncrementalAsync(Guid actorId, SyncCursor since, CancellationToken cancellationToken)
    {
        // xid8 has no bigint cast in Postgres (M1): pass sinceXid as text + ::xid8.
        // KANIT/o84: a bare ORDER BY name binds to the SELECT list's ::text alias first (shadowing, sorted lexicographically) -- qualified ORDER BY + distinct cast names fix that.
        // IS-EMRI-o86-A §D1 (IKI YOL DA PAZARLIKSIZ): sahip OR scope-uye OR old_scope-uye (bir gorev
        // projeden cikarilinca old_scope_id'si eski projedir -- bu kol dusseydi uye ekraninda hayalet
        // satir kalirdi). o84 dersi: ORDER BY nitelikli (o.) kalir, gölgelenmez.
        // IS-EMRI-o86-A2 §B: project_access GORUNUMU uzerinden (sahip uyelik tablosuna
        // YAZILMAZ, §C3 -- gorunum olmadan sahip kendi PROJESININ uyelerinin yazdigi
        // degisiklikleri artimli pull'da GOREMEZDI, bulgu 1'in ta kendisi). §G: bu dosyada
        // uyelik tablosunun adi GECMEZ -- erisim YALNIZ gorunum uzerindendir (mekanik kapi).
        // Onur kilidi (4. bulgu, o86-A2 canli tur adim 9): `o.owner_id = @actorId` TEK BASINA
        // sonsuz bir arka kapiydi -- bir op'u YAZDIGI o SATIR (outbox degismez/append-only) actor
        // projeden cikarildiktan SONRA bile actor'in KENDI owner_id'siyle damgali KALIR, bu sart
        // eskiden HER ZAMAN gecerdi. Artik yalniz scope'suz (kisisel, Gelen Kutusu) satirlarda
        // gecerlidir -- scope tasiyan bir satir icin gorunurluk SADECE guncel uyelikten (project_access)
        // gelir, o satiri KIMIN yazdigindan degil.
        await using var command = await db.CreateRawCommandAsync(
            "SELECT o.commit_xid::text AS commit_xid_text, o.server_seq, o.payload::text AS payload_text FROM outbox_messages o " +
            "WHERE commit_xid < pg_snapshot_xmin(pg_current_snapshot()) " +
            "AND (commit_xid, server_seq) > (@sinceXid::xid8, @sinceSeq) " +
            "AND ( ( o.owner_id = @actorId AND o.scope_id IS NULL AND o.old_scope_id IS NULL ) " +
            "   OR o.scope_id     IN (SELECT project_id FROM project_access WHERE user_id = @actorId) " +
            "   OR o.old_scope_id IN (SELECT project_id FROM project_access WHERE user_id = @actorId) ) " +
            "ORDER BY o.commit_xid, o.server_seq LIMIT " + PageSize,
            cancellationToken);
        command.Parameters.AddWithValue("sinceXid", since.Xid.ToString(CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("sinceSeq", since.Seq);
        command.Parameters.AddWithValue("actorId", actorId);

        var changes = new List<ChangeRecord>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var cursor = new SyncCursor(ulong.Parse(reader.GetString(0), CultureInfo.InvariantCulture), reader.GetInt64(1));
                changes.Add(new ChangeRecord(cursor, reader.GetString(2)));
            }
        }

        var next = changes.Count > 0 ? changes[^1].Cursor : since;
        return new PullPage(changes, next, changes.Count == PageSize);
    }

    public async Task<SnapshotPage> SnapshotAsync(Guid actorId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);

        var horizon = await ReadHorizonAsync(cancellationToken);
        var owned = await ReadOwnedEntitiesAsync(actorId, cancellationToken);

        var entities = new List<SnapshotEntity>(owned.Count);
        foreach (var (entityType, entityId) in owned)
        {
            var state = new EntityState();
            await SyncRowHydration.HydrateAsync(db, state, entityType, entityId, cancellationToken);
            entities.Add(Project(entityType, entityId, state));
        }

        await transaction.CommitAsync(cancellationToken);
        return new SnapshotPage(entities, SyncCursor.AtHorizon(horizon));
    }

    private async Task<ulong> ReadHorizonAsync(CancellationToken cancellationToken)
    {
        await using var command = await db.CreateRawCommandAsync(
            "SELECT pg_snapshot_xmin(pg_current_snapshot())::text", cancellationToken);
        var text = (string)(await command.ExecuteScalarAsync(cancellationToken))!;
        return ulong.Parse(text, CultureInfo.InvariantCulture);
    }

    private async Task<List<(string EntityType, Guid EntityId)>> ReadOwnedEntitiesAsync(Guid actorId, CancellationToken cancellationToken)
    {
        // IS-EMRI-o86-A §D2: PullIncrementalAsync ile AYNI uc-kollu kural -- olmadan taze kurulmus bir
        // istemci paylasilan hicbir seyi gormez (o85-A'nin C2 kaniti tam buydu), dilim yarim kalir.
        // IS-EMRI-o86-A2 §B: PullIncrementalAsync ile AYNI gorunum-tabanli kural (project_access).
        // Onur kilidi (4. bulgu, o86-A2 canli tur adim 9 -- H7b): outbox GECMISINE bakan tek bir
        // uc-kollu kural burada YETMEZ -- bu sorgu YALNIZ "hangi entityId'ler" listesini uretir,
        // HydrateAsync ardindan varligin GUNCEL durumunu doner. Bir gorev ONCE kisisel (scope'suz,
        // owner_id=actor) yaratilip SONRA bir projeye TASINMIS olabilir -- o eski kisisel outbox
        // satiri (scope_id IS NULL) narrow edilmis kurali bile GECER, entity listeye girer, hidrasyon
        // GUNCEL (proje-ici) durumu sizdirir. Tek dogru olcum GUNCEL materyalize durumdur:
        // Task icin tasks.project_id/owner_id, Project icin project_access (sahip zaten onun ICINDE).
        // Diger tipler (TaskList/Tag) hic scope tasimaz (ReadProjectId yalniz Task'ta calisir) --
        // bugunku outbox-tabanli davranista KALIR, degismez.
        await using var command = await db.CreateRawCommandAsync(
            "SELECT 'Task' AS aggregate_type, t.entity_id AS aggregate_id FROM tasks t " +
            "WHERE (t.project_id IS NULL AND t.owner_id = @actorId) " +
            "   OR t.project_id IN (SELECT project_id FROM project_access WHERE user_id = @actorId) " +
            "UNION " +
            "SELECT 'Project' AS aggregate_type, p.entity_id AS aggregate_id FROM projects p " +
            "WHERE p.entity_id IN (SELECT project_id FROM project_access WHERE user_id = @actorId) " +
            "UNION " +
            "SELECT DISTINCT o.aggregate_type, o.aggregate_id FROM outbox_messages o " +
            "WHERE o.aggregate_type NOT IN ('Task', 'Project') " +
            "  AND ( ( o.owner_id = @actorId AND o.scope_id IS NULL AND o.old_scope_id IS NULL ) " +
            "     OR o.scope_id     IN (SELECT project_id FROM project_access WHERE user_id = @actorId) " +
            "     OR o.old_scope_id IN (SELECT project_id FROM project_access WHERE user_id = @actorId) ) " +
            "ORDER BY aggregate_type, aggregate_id", cancellationToken);
        command.Parameters.AddWithValue("actorId", actorId);

        var result = new List<(string, Guid)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add((reader.GetString(0), reader.GetGuid(1)));
        }

        return result;
    }

    private static SnapshotEntity Project(string entityType, Guid entityId, EntityState entity)
    {
        var scalars = new List<SnapshotScalar>();
        foreach (var (field, register) in entity.Fields)
        {
            if (register.HasValue)
            {
                scalars.Add(new SnapshotScalar(field, register.Value, register.Key.Hlc, register.Key.OperationId));
            }
        }

        foreach (var (field, register) in entity.Orders)
        {
            if (register.HasValue)
            {
                scalars.Add(new SnapshotScalar(field, register.Value, register.Key.Hlc, register.Key.OperationId));
            }
        }

        var groups = new List<SnapshotGroup>();
        foreach (var (name, group) in entity.Groups)
        {
            if (group.HasValue)
            {
                groups.Add(new SnapshotGroup(name, group.Fields, group.Key.Hlc, group.Key.OperationId));
            }
        }

        var sets = new List<SnapshotSet>();
        foreach (var (setName, set) in entity.Sets)
        {
            var elements = set.DumpTags()
                .Where(tag => tag is { Cancelled: false, Hlc: not null })
                .GroupBy(tag => tag.Element, StringComparer.Ordinal)
                .Select(group => new SnapshotSetElement(
                    group.Key,
                    group.Select(tag => new SnapshotActiveTag(tag.Tag, tag.Hlc!.Value)).ToList()))
                .ToList();

            if (elements.Count > 0)
            {
                sets.Add(new SnapshotSet(setName, elements));
            }
        }

        return new SnapshotEntity(entityType, entityId, scalars, sets, groups);
    }
}

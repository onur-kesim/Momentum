using System.Text.Json;
using Momentum.Application.Abstractions.Sync;
using Momentum.Application.Features.Sync;
using Momentum.Domain.Sync;
using Shouldly;
using Xunit;

namespace Momentum.Persistence.Tests;

/// <summary>
/// GOREV-slice-3d G7/D9 -- `owner_id` KUSURU düzeltmesinin çekme GÖRÜNÜRLÜĞÜ üzerindeki etkisi
/// (gerçek PostgreSQL, Testcontainers). `ScopeAndDriftAnchorTests.D9_...` SQL sütunlarını doğrudan
/// ölçer; bu dosya AYNI senaryoyu `SyncPuller`in `owner_id` süzgeci ÜZERİNDEN, uçtan uca ölçer.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class D9OwnerIdVisibilityTests(PostgresFixture fixture)
{
    /// <summary>
    /// IS-EMRI-o86-A §A1 (ILK IS, kod yazilmadan ONCE kosuldu) + §B/G1: bir `Project` op'unun
    /// `scope_id`si kendi `entityId`sidir (proje scope'un kendisidir), `old_scope_id` her zaman null
    /// (bir projenin scope'u degismez). §B uygulanmadan once bu test KIRMIZI dustu (scope_id NULL
    /// cikti) -- ham cikti KANIT/o86A/01-olcum.txt'e alindi, tasarimin varsayimini (postProjectId bir
    /// Project op'unda null'dur) DOGRULADI. §B'nin mutant-ispati da AYNI testtir (G1).
    /// </summary>
    [Fact]
    public async Task Project_op_scope_id_kendi_entity_id_sidir_old_scope_id_her_zaman_null()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var actor = Guid.NewGuid();
        var projectEntity = Guid.NewGuid();
        var opId = Guid.CreateVersion7();

        await app.SyncAsync(actor, Wire.PushNoPull(actor, Wire.Op(opId, actor, projectEntity, actor, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(actor, 1)) },
            entityType: "Project")));

        var scopeId = await Db.ScalarAsync<Guid?>(connectionString, "SELECT scope_id FROM outbox_messages WHERE operation_id = @op", ("op", opId));
        var oldScopeId = await Db.ScalarAsync<Guid?>(connectionString, "SELECT old_scope_id FROM outbox_messages WHERE operation_id = @op", ("op", opId));

        scopeId.ShouldBe(projectEntity, "Project op'unun scope_id'si kendi entityId'sidir");
        oldScopeId.ShouldBeNull("bir projenin scope'u degismez");
    }

    [Fact]
    public async Task D9_baslik_X_govde_actorId_Y_ise_satir_Xin_cekmesinde_gorunur_Yninkinde_gorunmez()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var actorX = Guid.NewGuid();
        var actorY = Guid.NewGuid();
        var entity = Guid.NewGuid();
        var opId = Guid.CreateVersion7();

        // Baslik (kimlik dogrulama) X'tir; govdenin actorId'si Y'dir (enjeksiyon senaryosu).
        await app.SyncAsync(actorX, Wire.PushNoPull(actorX, Wire.TaskField(opId, actorX, entity, actorY, "title", "D9 gorunurluk testi")));

        var xSnapshot = await app.SnapshotAsync(actorX);
        xSnapshot.Entities.ShouldContain(e => e.EntityId == entity, "D9: outbox owner_id KIMLIK DOGRULANMIS X'tir -- X'in cekmesinde GORUNMELI");

        var ySnapshot = await app.SnapshotAsync(actorY);
        ySnapshot.Entities.ShouldNotContain(e => e.EntityId == entity, "D9: govdenin actorId iddiasi (Y) outbox owner_id'yi BELIRLEMEMELI -- Y'nin cekmesinde GORUNMEMELI");
    }

    /// <summary>
    /// IS-EMRI-o83 F5 KILIDI: D9'un onceki beyanini SUPERSEDE eder. Eskiden ("D9 BEYAN") actor_id
    /// (denetim kaydi) BILEREK govdeden geliyordu; artik GERCEK kullanicilar var ve govdenin actorId
    /// iddiasini denetim kaydinda bile serbest birakmak bir kullaniciya baskasinin degisikligini
    /// YUKLEYEBILMEK demek (dilim 3 isbirliginde sahtekarlik riski) -- bu yuzden actor_id de artik
    /// KIMLIK DOGRULANMIS aktorden yazilir, govdenin (Y) iddiasi YOK SAYILIR.
    /// </summary>
    [Fact]
    public async Task D9_outbox_owner_id_VE_actor_id_ikisi_de_artik_dogrulanan_aktorden_yazilir()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var actorX = Guid.NewGuid();
        var actorY = Guid.NewGuid();
        var entity = Guid.NewGuid();
        var opId = Guid.CreateVersion7();

        // Baslik (kimlik dogrulama) X'tir; govdenin actorId'si Y'dir (enjeksiyon senaryosu, degismedi).
        await app.SyncAsync(actorX, Wire.PushNoPull(actorX, Wire.TaskField(opId, actorX, entity, actorY, "title", "F5 SQL dogrulamasi")));

        var ownerId = await Db.ScalarAsync<Guid>(connectionString, "SELECT owner_id FROM outbox_messages WHERE operation_id = @op", ("op", opId));
        var actorId = await Db.ScalarAsync<Guid>(connectionString, "SELECT actor_id FROM outbox_messages WHERE operation_id = @op", ("op", opId));

        // GOREV-slice-3d 8.2 deseni korunur: bu sorgu Testcontainers icinde kosar, PowerShell o
        // baglantiyi GOREMEZ -- KANITI ayagin KENDISI yazar. Ortam degiskeni yoksa SESSIZCE atlamak
        // YASAK, test firlatir.
        var kanitDizini = Environment.GetEnvironmentVariable("MOMENTUM_KANIT_DIZIN")
            ?? throw new InvalidOperationException("MOMENTUM_KANIT_DIZIN ortam degiskeni ayarli degil -- KANIT sessizce atlanamaz.");
        Directory.CreateDirectory(kanitDizini);
        await File.WriteAllTextAsync(Path.Combine(kanitDizini, "outbox-sorgu.txt"),
            $"SELECT owner_id, actor_id FROM outbox_messages WHERE operation_id = @op (op={opId})\n" +
            $"actorX (kimlik dogrulama basligi) = {actorX}\n" +
            $"actorY (govdenin actorId iddiasi, ARTIK EZILIYOR) = {actorY}\n" +
            $"owner_id  = {ownerId}  (beklenen: actorX)\n" +
            $"actor_id  = {actorId}  (beklenen: actorX -- o83 F5, D9'u supersede eder)\n");

        ownerId.ShouldBe(actorX, "owner_id KIMLIK DOGRULAMADAN gelir");
        actorId.ShouldBe(actorX, "IS-EMRI-o83 F5: actor_id de artik KIMLIK DOGRULANMIS aktorden yazilir -- govdenin (Y) iddiasi yok sayilir");
    }

    /// <summary>
    /// IS-EMRI-o83 F5 -- ikinci yari: bayt-duzeyinde OutboxRecord.ActorId (DB sutunu) duzeltilse bile,
    /// WireMapping.ClampedPayload govdenin actorId'sini AYRICA yankiliyor olabilirdi -- bu payload,
    /// BASKA istemcilerin pull ile GERCEKTEN okudugu JSON'un ta kendisidir (WireChange.Payload). Bu
    /// test DB sutununu DEGIL, tam da o yankilanan payload'in icindeki actorId alanini olcer.
    /// </summary>
    [Fact]
    public async Task F5_pull_payloadindaki_WireOp_ActorId_de_authenticated_aktorden_gelir_govdeden_DEGIL()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var actorX = Guid.NewGuid();
        var actorY = Guid.NewGuid();
        var entity = Guid.NewGuid();
        var opId = Guid.CreateVersion7();

        // Bos taban imlec (entity henuz yok), sonra AYNI istekte push+pull -- X kendi yazdigi
        // degisikligi "changes" olarak geri okur (SyncCommandHandler: push ONCE, pull SONRA, ayni istek).
        var taban = await app.SnapshotAsync(actorX);
        var yanit = await app.SyncAsync(actorX, new SyncRequest(actorX, null,
            new WireCursor(taban.NextCursor.Xid, taban.NextCursor.Seq),
            [Wire.TaskField(opId, actorX, entity, actorY, "title", "F5 payload testi")]));

        var degisiklik = yanit.Changes.ShouldHaveSingleItem();
        degisiklik.Payload.GetProperty("actorId").GetGuid()
            .ShouldBe(actorX, "IS-EMRI-o83 F5: pull payload'indaki WireOp.ActorId de kimlik dogrulanmis aktorden gelir, govdenin (Y) iddiasi DEGIL");
    }

    /// <summary>
    /// IS-EMRI-o86-A G3+G4: uye B hem ARTIMLI hem TAZE-KURULUM (snapshot) yolunda projeyi VE gorevini
    /// GORUR; uye OLMAYAN C HICBIRINDE gormez (pozitif+negatif, o83-G dersi -- bos liste her iddiayi gecirir).
    /// </summary>
    [Fact]
    public async Task Member_sees_project_and_task_via_incremental_and_snapshot_non_member_sees_nothing()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var nonMember = Guid.NewGuid();
        var project = Guid.NewGuid();
        var task = Guid.NewGuid();

        // Member'in baseline cursor'u -- uyelik/gorev DOGMADAN once (artimlinin GERCEKTEN yeni geleni
        // getirdigini kanitlamak icin).
        var baseline = await app.SnapshotAsync(member);

        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(owner, 1)) },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 2,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new([new WireSetAdd(member.ToString(), Guid.CreateVersion7(), Wire.Hlc(owner, 2))], null),
            },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner,
            Wire.TaskField(Guid.CreateVersion7(), owner, task, owner, "projectId", project.ToString(), counter: 3)));

        // G3: artimli pull -- UYE GORUR.
        var memberPage = await app.PullAsync(member, baseline.NextCursor);
        var memberEntities = memberPage.Changes
            .Select(c => JsonDocument.Parse(c.PayloadJson).RootElement.GetProperty("entityId").GetGuid())
            .ToHashSet();
        memberEntities.ShouldContain(project);
        memberEntities.ShouldContain(task);

        // G3 POZITIF KONTROL: uye OLMAYAN C GORMEZ.
        var nonMemberBaseline = await app.SnapshotAsync(nonMember);
        var nonMemberPage = await app.PullAsync(nonMember, nonMemberBaseline.NextCursor);
        nonMemberPage.Changes.ShouldBeEmpty();

        // G4: TAZE KURULUM snapshot -- UYE projeyi VE gorevi GORUR.
        var memberSnapshot = await app.SnapshotAsync(member);
        var memberSnapshotIds = memberSnapshot.Entities.Select(e => e.EntityId).ToHashSet();
        memberSnapshotIds.ShouldContain(project);
        memberSnapshotIds.ShouldContain(task);

        // G4 POZITIF KONTROL: uye OLMAYAN C'nin taze kurulumu BOS.
        var nonMemberSnapshot = await app.SnapshotAsync(nonMember);
        nonMemberSnapshot.Entities.ShouldBeEmpty();
    }

    /// <summary>
    /// IS-EMRI-o86-A G5 (PAZARLIKSIZ): bir gorev projeden CIKARILINCA (projectId -> null), o degisiklik
    /// UYEYE old_scope_id UZERINDEN ULASIR -- bu kol dusseydi uyenin ekraninda hayalet satir kalirdi.
    /// </summary>
    [Fact]
    public async Task Task_removed_from_project_reaches_member_via_old_scope_id()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var project = Guid.NewGuid();
        var task = Guid.NewGuid();

        // Uyenin BASLANGIC cursor'u -- her sey olmadan ONCE (snapshot ONCE alinirsa, ondan sonraki
        // pull, snapshot'in KENDISI zaten kapsadigi icin yanlislikla BOS doner -- pazarliksiz sira budur).
        var baseline = await app.SnapshotAsync(member);

        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(owner, 1)) },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 2,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new([new WireSetAdd(member.ToString(), Guid.CreateVersion7(), Wire.Hlc(owner, 2))], null),
            },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner,
            Wire.TaskField(Guid.CreateVersion7(), owner, task, owner, "projectId", project.ToString(), counter: 3)));

        // Uyenin cursor'unu gorevin P'ye GIRISINI GORMUS noktaya ilerlet.
        var afterCreate = await app.PullAsync(member, baseline.NextCursor);
        afterCreate.Changes.ShouldNotBeEmpty();

        // Gorev Gelen Kutusu'na tasinir (projectId -> null): scope_id NULL, old_scope_id = P.
        var moveOpId = Guid.CreateVersion7();
        await app.SyncAsync(owner, Wire.PushNoPull(owner,
            Wire.TaskField(moveOpId, owner, task, owner, "projectId", null, counter: 4)));

        var afterMove = await app.PullAsync(member, afterCreate.NextCursor);
        var degisiklik = afterMove.Changes.ShouldHaveSingleItem();
        JsonDocument.Parse(degisiklik.PayloadJson).RootElement.GetProperty("entityId").GetGuid().ShouldBe(task);

        (await Db.ScalarAsync<Guid>(connectionString, "SELECT old_scope_id FROM outbox_messages WHERE operation_id = @o", ("o", moveOpId)))
            .ShouldBe(project);
    }

    /// <summary>
    /// IS-EMRI-o86-A G6: uye OLMAYANIN (mevcut scope'lu Task) yazimi RED · uyenin `members` yazimi RED
    /// (en sert kural -- sahip-yalniz, aksi halde bir uye sahibi silip projeyi CALARDI) · sahibin
    /// `members` yazimi KABUL.
    /// </summary>
    [Fact]
    public async Task Write_authorization_rejects_non_member_and_member_membership_write_accepts_owner()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        var project = Guid.NewGuid();
        var task = Guid.NewGuid();

        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(owner, 1)) },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 2,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new([new WireSetAdd(member.ToString(), Guid.CreateVersion7(), Wire.Hlc(owner, 2))], null),
            },
            entityType: "Project")));
        await app.SyncAsync(owner, Wire.PushNoPull(owner,
            Wire.TaskField(Guid.CreateVersion7(), owner, task, owner, "projectId", project.ToString(), counter: 3)));

        // Uye OLMAYAN (outsider), P kapsamindaki gorevi yazmaya calisir -- RED.
        var outsiderResponse = await app.SyncAsync(outsider, Wire.PushNoPull(outsider,
            Wire.TaskField(Guid.CreateVersion7(), outsider, task, outsider, "title", "calindi", counter: 1)));
        outsiderResponse.Applied.ShouldHaveSingleItem().Code.ShouldBe(nameof(IngestResultCode.RejectedForbidden));
        (await Db.ScalarAsync<string>(connectionString, "SELECT title FROM tasks WHERE entity_id = @e", ("e", task)))
            .ShouldBeNull("outsider'in yazimi materyalize OLMAMALI");

        // UYE (member), members setine yazmaya calisir (baskasini eklemeye) -- RED (sahip-yalniz kural).
        var memberResponse = await app.SyncAsync(member, Wire.PushNoPull(member, Wire.Op(Guid.CreateVersion7(), member, project, member, 4,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new([new WireSetAdd(outsider.ToString(), Guid.CreateVersion7(), Wire.Hlc(member, 4))], null),
            },
            entityType: "Project")));
        memberResponse.Applied.ShouldHaveSingleItem().Code.ShouldBe(nameof(IngestResultCode.RejectedForbidden));
        (await Db.ScalarAsync<long>(connectionString,
            "SELECT count(*) FROM project_members WHERE project_id = @p AND user_id = @o", ("p", project), ("o", outsider)))
            .ShouldBe(0L, "uyenin members yazimi ETKISIZ kalmali");

        // SAHIP (owner), members'a yazar -- KABUL.
        var ownerResponse = await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 5,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new([new WireSetAdd(outsider.ToString(), Guid.CreateVersion7(), Wire.Hlc(owner, 5))], null),
            },
            entityType: "Project")));
        ownerResponse.Applied.ShouldHaveSingleItem().Code.ShouldBe(nameof(IngestResultCode.Applied));
        (await Db.ScalarAsync<long>(connectionString,
            "SELECT count(*) FROM project_members WHERE project_id = @p AND user_id = @o", ("p", project), ("o", outsider)))
            .ShouldBe(1L);
    }
}

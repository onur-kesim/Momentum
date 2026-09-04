using System.Text.Json;
using Mediator;
using Momentum.Application.Abstractions.Sync;
using Momentum.Domain.Sync;

namespace Momentum.Application.Features.Sync;

/// <summary>
/// Orchestrates /v1/sync (ADR 0002 K2-E, GOREV slice-2b1 D4/D5/D6). Push = one txn per op:
/// lock (client -> entity) -> DB dedup -> hydrate -> DOMAIN decides (2a SyncIngest) -> dump delta +
/// outbox + client-clock + dedup in the SAME commit. Then pull (xmin horizon) or full snapshot.
/// The decision recipe lives ONLY in Domain — this handler never re-implements LWW/CRDT/registry (§5).
/// </summary>
public sealed class SyncCommandHandler : ICommandHandler<SyncCommand, SyncResponse>
{
    private const string ProjectIdField = "projectId";

    private readonly ISyncTransaction _transaction;
    private readonly ISyncStore _store;
    private readonly IOutboxWriter _outbox;
    private readonly IProcessedOperations _processed;
    private readonly IClientClock _clientClock;
    private readonly ISyncPuller _puller;
    private readonly IEntityMaterializer _materializer;
    private readonly TimeProvider _timeProvider;

    public SyncCommandHandler(
        ISyncTransaction transaction,
        ISyncStore store,
        IOutboxWriter outbox,
        IProcessedOperations processed,
        IClientClock clientClock,
        ISyncPuller puller,
        IEntityMaterializer materializer,
        TimeProvider timeProvider)
    {
        _transaction = transaction;
        _store = store;
        _outbox = outbox;
        _processed = processed;
        _clientClock = clientClock;
        _puller = puller;
        _materializer = materializer;
        _timeProvider = timeProvider;
    }

    public async ValueTask<SyncResponse> Handle(SyncCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var receiveWall = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

        var applied = new List<WireIngestResult>();
        Hlc? maxEffective = null;

        // --- push: one transaction per op (poison op drops only its own txn) ---
        foreach (var wireOp in request.Ops ?? [])
        {
            var result = await ProcessOpAsync(wireOp, receiveWall, command.ActorId, cancellationToken);
            applied.Add(WireMapping.ToWire(result));
            if (result is { Code: IngestResultCode.Applied, EffectiveOpHlc: { } effective }
                && (maxEffective is null || effective.CompareTo(maxEffective.Value) > 0))
            {
                maxEffective = effective;
            }
        }

        // --- pull / snapshot (after the client's own pushes are committed) ---
        WireCursor? nextCursor = null;
        var hasMore = false;
        var resyncRequired = false;
        IReadOnlyList<WireChange> changes = [];
        IReadOnlyList<WireSnapshotEntity>? snapshot = null;

        if (request.SinceCursor is null)
        {
            var page = await _puller.SnapshotAsync(command.ActorId, cancellationToken);
            snapshot = page.Entities.Select(ToWire).ToList();
            nextCursor = new WireCursor(page.NextCursor.Xid, page.NextCursor.Seq);
        }
        else
        {
            var since = new SyncCursor(request.SinceCursor.Xid, request.SinceCursor.Seq);
            if (await _puller.ShouldResyncAsync(since, cancellationToken))
            {
                resyncRequired = true; // flag-only: client re-requests with sinceCursor=null (named refinement)
                nextCursor = request.SinceCursor;
            }
            else
            {
                var page = await _puller.PullIncrementalAsync(command.ActorId, since, cancellationToken);
                changes = page.Changes.Select(ToWire).ToList();
                nextCursor = new WireCursor(page.NextCursor.Xid, page.NextCursor.Seq);
                hasMore = page.HasMore;
            }
        }

        // serverHlc: max effective produced now, else last known client clock, else null (informational only).
        WireHlc? serverHlc;
        if (maxEffective is { } produced)
        {
            serverHlc = WireMapping.ToWire(produced);
        }
        else
        {
            var last = await _clientClock.GetAsync(request.ClientId, cancellationToken);
            serverHlc = last is { } value ? WireMapping.ToWire(value) : null;
        }

        return new SyncResponse(serverHlc, nextCursor, hasMore, resyncRequired, applied, changes, snapshot);
    }

    private async ValueTask<IngestResult> ProcessOpAsync(WireOp wireOp, long receiveWall, Guid authenticatedActorId, CancellationToken cancellationToken)
    {
        await using var scope = await _transaction.BeginOpScopeAsync(cancellationToken);
        var op = WireMapping.ToDomain(wireOp);

        // Fixed lock order: client then entity (ADR 0002 K2-A4 / B1 — deadlock-free).
        await _store.LockClientAsync(op.ClientId, cancellationToken);
        await _store.LockEntityAsync(op.EntityType, op.EntityId, cancellationToken);

        // DB dedup precheck (replaces the in-memory dedup; Duplicate is rebuilt from the row).
        var prior = await _processed.GetAsync(op.ClientId, op.OperationId, cancellationToken);
        if (prior is not null)
        {
            await scope.CommitAsync(cancellationToken);
            return prior.Code == IngestResultCode.Applied
                ? new IngestResult(op.OperationId, IngestResultCode.Duplicate, prior.EffectiveOpHlc)
                : new IngestResult(op.OperationId, prior.Code, prior.EffectiveOpHlc);
        }

        // Hydrate the entity + seed the client clock, then let the DOMAIN decide.
        var syncState = new SyncState();
        var entity = syncState.GetOrCreateEntity(op.EntityType, op.EntityId);
        await _store.HydrateAsync(entity, op.EntityType, op.EntityId, cancellationToken);

        // IS-EMRI-o86-A §E karar tablosu satir 1 ("Varlik yeni"): PRE-op (hydrate SONRASI, Ingest'ten
        // ONCE) olculur -- hic hidratlanan kanal yoksa bu entityId'ye bugune kadar HIC op gelmemistir.
        var isNewEntity = entity.Fields.Count == 0 && entity.Orders.Count == 0
            && entity.Groups.Count == 0 && entity.Sets.Count == 0;

        var preProjectId = ReadProjectId(op.EntityType, entity);

        var clockStore = new ClientClockStore();
        if (await _clientClock.GetAsync(op.ClientId, cancellationToken) is { } seed)
        {
            clockStore.Merge(op.ClientId, seed);
        }

        var result = new SyncIngest(syncState, clockStore).Ingest(op, receiveWall);

        if (result.Code == IngestResultCode.RejectedInvalid)
        {
            await scope.CommitAsync(cancellationToken); // ERRATA: RejectedInvalid is NOT recorded
            return result;
        }

        if (result is { Code: IngestResultCode.Applied, EffectiveOpHlc: { } effective })
        {
            // IS-EMRI-o86-A §E: yazma yetkisi kapisi. `entity` burada POST-op durumdadir (Ingest
            // yukarida zaten uyguladi) -- hedef scope (Task'in yeni projectId'si) BUNDAN okunur.
            // PersistDeltaAsync/MaterializeAsync'ten ONCE, hic yan etki olusmadan kontrol edilir.
            if (!await IsAuthorizedAsync(op, entity, isNewEntity, authenticatedActorId, cancellationToken))
            {
                // RejectedInvalid ile AYNI ERRATA deseni (A3): KAYDEDILMEZ -- uyelik zamanla
                // degisebilir, sonraki bir retry (uye yapilinca) yeniden degerlendirilebilmeli.
                await scope.CommitAsync(cancellationToken);
                return new IngestResult(op.OperationId, IngestResultCode.RejectedForbidden, null);
            }

            await _store.PersistDeltaAsync(op, entity, cancellationToken);
            // slice-3a F1: SAME op-transaction, right after PersistDeltaAsync, Applied branch ONLY.
            // ownerId is the AUTHENTICATED actor -- NEVER op.ActorId (F5, mutant-3's target).
            await _materializer.MaterializeAsync(op, entity, authenticatedActorId, cancellationToken);
            // IS-EMRI-o83 F5 KILIDI (bu dilimin ASIL KAZANCI): D9'un onceki beyani -- "actor_id
            // (denetim kaydi) govdeden gelmeye devam eder, ikisi bilerek ayri" -- artik SUPERSEDE
            // edildi (bkz. BuildOutbox). Simdi GERCEK kullanicilar var; govdenin actorId iddiasini
            // denetim kaydinda bile serbest birakmak, bir kullaniciya baskasinin yaptigi degisikligi
            // YUKLEYEBILMEK demektir (dilim 3 isbirliginde gercek bir sahtekarlik riski).
            await _clientClock.UpsertGreatestAsync(op.ClientId, effective, cancellationToken);
            await _outbox.WriteAsync(BuildOutbox(wireOp, op, entity, effective, preProjectId, receiveWall, authenticatedActorId), cancellationToken);
        }

        await _processed.RecordAsync(op.ClientId, op.OperationId, result.Code, result.EffectiveOpHlc, cancellationToken);
        await scope.CommitAsync(cancellationToken);
        return result;
    }

    private OutboxRecord BuildOutbox(WireOp wireOp, ChangeOperation op, EntityState entity, Hlc effective, string? preProjectId, long receiveWall, Guid authenticatedActorId)
    {
        var postProjectId = ReadProjectId(op.EntityType, entity);
        // IS-EMRI-o86-A §B (PAZARLIKSIZ): bir Project op'unun scope'u PROJENIN KENDISIDIR --
        // davet op'u (members yazimi) bir Project op'udur; scope tasimazsa ne hub yayar ne pull
        // tasir, davet edilen kisi asla ogrenemez. Diger tipler bugunku davranista kalir.
        var scopeId = op.EntityType == "Project" ? op.EntityId : TryScope(postProjectId);
        Guid? oldScopeId = null; // bir projenin scope'u degismez -- Project icin HER ZAMAN null.
        if (op.EntityType == "Task"
            && op.Fields.ContainsKey(ProjectIdField)
            && !string.Equals(preProjectId, postProjectId, StringComparison.Ordinal))
        {
            oldScopeId = TryScope(preProjectId);
        }

        var now = _timeProvider.GetUtcNow();
        return new OutboxRecord(
            AggregateType: op.EntityType,
            AggregateId: op.EntityId,
            OperationId: op.OperationId,
            // slice-3d D9 (K69 kusur duzeltmesi): OwnerId KIMLIK DOGRULAMADAN gelir, ASLA op.ActorId --
            // aksi halde govdede baskasinin actorId'sini tasiyan bir op DOGRU materialize olur ama
            // outbox satiri YANLIS sahibe etiketlenir ve o satir hic kimsenin cekmesinde gorunmez.
            OwnerId: authenticatedActorId,
            ScopeId: scopeId,
            OldScopeId: oldScopeId,
            // IS-EMRI-o83 F5 KILIDI: govdenin actorId iddiasi ARTIK YOK SAYILIR (D9'un onceki
            // "actor_id govdeden gelmeye devam eder" beyanini SUPERSEDE eder -- yukaridaki yorum).
            ActorId: authenticatedActorId,
            EventType: $"{op.EntityType}.changed",
            Payload: WireMapping.ClampedPayload(wireOp, receiveWall, authenticatedActorId),
            Hlc: effective.Encode(),
            OccurredAt: now,
            AvailableAt: now); // slice-2b2 D6-1: ONE clock source (TimeProvider) -- no SQL now() default
    }

    /// <summary>
    /// IS-EMRI-o86-A §E karar tablosu (BIREBIR, Cowork+Onur kilidi 20 Agu):
    ///   Varlik yeni                              -> KABUL (yazan sahip olur)
    ///   Task op, hedef scope NULL (Gelen Kutusu) -> KABUL (yazan kendi kutusuna yaziyor)
    ///   Task op, hedef scope P                   -> P'nin sahibi VEYA uyesi ise KABUL
    ///   Project op, alan `members`               -> YALNIZ projects.owner_id ise KABUL (en sert kural --
    ///                                                bir uye members'tan sahibi SILEBILSEYDI projeyi calardi)
    ///   Project op, diger alanlar                -> sahip VEYA uye ise KABUL
    ///   TaskList/Tag                              -> bu dilimin kapsami disi, davranis DEGISMEZ
    /// </summary>
    private async Task<bool> IsAuthorizedAsync(ChangeOperation op, EntityState entity, bool isNewEntity, Guid actorId, CancellationToken cancellationToken)
    {
        if (isNewEntity)
        {
            return true;
        }

        if (op.EntityType == "Task")
        {
            var targetScope = TryScope(ReadProjectId(op.EntityType, entity)); // POST-op (Ingest zaten uyguladi)
            if (targetScope is null)
            {
                return true;
            }

            return await _store.IsProjectOwnerOrMemberAsync(targetScope.Value, actorId, cancellationToken);
        }

        if (op.EntityType == "Project")
        {
            if (op.Sets.ContainsKey("members"))
            {
                return await _store.IsProjectOwnerAsync(op.EntityId, actorId, cancellationToken);
            }

            return await _store.IsProjectOwnerOrMemberAsync(op.EntityId, actorId, cancellationToken);
        }

        return true;
    }

    private static string? ReadProjectId(string entityType, EntityState entity) =>
        entityType == "Task" && entity.Fields.TryGetValue(ProjectIdField, out var register) && register.HasValue
            ? register.Value
            : null;

    private static Guid? TryScope(string? projectId) =>
        projectId is not null && Guid.TryParse(projectId, out var scope) ? scope : null;

    private static WireChange ToWire(ChangeRecord change)
    {
        using var document = JsonDocument.Parse(change.PayloadJson);
        return new WireChange(new WireCursor(change.Cursor.Xid, change.Cursor.Seq), document.RootElement.Clone());
    }

    private static WireSnapshotEntity ToWire(SnapshotEntity entity) => new(
        entity.EntityType,
        entity.EntityId,
        entity.Scalars.Select(s => new WireSnapshotScalar(s.Field, s.Value, WireMapping.ToWire(s.Hlc), s.WinOperationId)).ToList(),
        entity.Sets.Select(set => new WireSnapshotSet(
            set.SetName,
            set.Elements.Select(element => new WireSnapshotElement(
                element.Element,
                element.ActiveTags.Select(tag => new WireSnapshotActiveTag(tag.Tag, WireMapping.ToWire(tag.Hlc))).ToList())).ToList())).ToList(),
        entity.Groups.Select(g => new WireSnapshotGroup(g.Group, g.Fields, WireMapping.ToWire(g.Hlc), g.WinOperationId)).ToList());
}

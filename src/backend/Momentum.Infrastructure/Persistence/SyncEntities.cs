namespace Momentum.Infrastructure.Persistence;

// EF-mapped rows (GOREV slice-2b1 D2). commit_xid + server_seq are DELIBERATELY not mapped on
// OutboxMessage (DB-generated: DEFAULT pg_current_xact_id() + GENERATED ALWAYS AS IDENTITY), added via
// migrationBuilder.Sql. Reads/writes go through raw SQL in the Sync/* implementations; these entities
// exist for the schema (migrations) and model validation.

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string AggregateType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public Guid OperationId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? ScopeId { get; set; }
    public Guid? OldScopeId { get; set; }
    public Guid ActorId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public string Hlc { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? SignaledAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset AvailableAt { get; set; }
}

public sealed class ProcessedOperation
{
    public Guid ClientId { get; set; }
    public Guid OperationId { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public string ResultCode { get; set; } = string.Empty;
    public string? EffectiveOpHlc { get; set; }
}

public sealed class SyncClientClockRow
{
    public Guid ClientId { get; set; }
    public string Hlc { get; set; } = string.Empty;
}

public sealed class SyncScalarMeta
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Field { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string Hlc { get; set; } = string.Empty;
    public Guid WinOperationId { get; set; }
}

public sealed class SyncOrsetTag
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string SetName { get; set; } = string.Empty;
    public string Element { get; set; } = string.Empty;
    public Guid AddTag { get; set; }
    public string? Hlc { get; set; }
    public bool Cancelled { get; set; }
}

public sealed class SyncOrsetRemove
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string SetName { get; set; } = string.Empty;
    public string Element { get; set; } = string.Empty;
    public string Hlc { get; set; } = string.Empty;
}

public sealed class SyncGcState
{
    public int Id { get; set; }
    public long GcHorizonSeq { get; set; }
}

// IS-EMRI-o86-D D1: sync_gc_state'in BIREBIR deseni -- tek fark, tekil satir (id=1) yerine
// KULLANICI-BASINA satir (PK user_id). horizon_xid (xid8, unmapped -- ayni sync_gc_state.gc_horizon_xid
// gibi migrationBuilder.Sql ile eklenir, EF'in bigint cast'i olmadigi icin) bu sinifta YOK.
public sealed class UserResyncHorizon
{
    public Guid UserId { get; set; }
    public long HorizonSeq { get; set; }
}

// GOREV slice-3a D1: materialized read rows. Reads/writes go through RAW SQL (EntityMaterializer /
// TaskReadStore), mirroring every other Sync/* table -- these entities exist for migrations + model
// validation only (ADR 0002 K2-I2).

public sealed class TaskRow
{
    public Guid EntityId { get; set; }
    public Guid OwnerId { get; set; }
    public string? Title { get; set; }
    public string? Notes { get; set; }
    public int? Priority { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? RemindAt { get; set; }
    public Guid? ProjectId { get; set; }
    public bool IsDeleted { get; set; }
    public string? RecurrenceRule { get; set; }
    public string? ListPos { get; set; }
    public string? BoardPos { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool HasDeleteEditConflict { get; set; }
    public List<string> MalformedFields { get; set; } = [];
}

public sealed class TaskListRow
{
    public Guid EntityId { get; set; }
    public Guid OwnerId { get; set; }
    public string? Name { get; set; }
    public bool IsDeleted { get; set; }
    public string? Pos { get; set; }
    public bool HasDeleteEditConflict { get; set; }
    public List<string> MalformedFields { get; set; } = [];
}

// IS-EMRI-o85-B: vitrin/mimari butunluk (Project zaten registry'de kayitli, DILIM 2'de uctan uca
// akiyordu -- bu satir yalniz materyalizasyonu ekler, davranis DEGISMEZ). TaskListRow'un birebir
// deseni + Color (registry'de "color" scalar olarak zaten var).
public sealed class ProjectRow
{
    public Guid EntityId { get; set; }
    public Guid OwnerId { get; set; }
    public string? Name { get; set; }
    public string? Color { get; set; }
    public bool IsDeleted { get; set; }
    public string? Pos { get; set; }
    public bool HasDeleteEditConflict { get; set; }
    public List<string> MalformedFields { get; set; } = [];
}

public sealed class TaskTagRow
{
    public Guid TaskId { get; set; }
    public string Tag { get; set; } = string.Empty;
}

// IS-EMRI-o86-A §C: TaskTagRow'un birebir deseni (task_tags <-> project_members), tek fark eleman
// bir userId'dir (Guid) -- Element string'i C2'de Guid.TryParse'tan GECMIS olarak gelir.
public sealed class ProjectMemberRow
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
}

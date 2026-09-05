using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Momentum.Infrastructure.Persistence.Configurations;

// snake_case mapping (GOREV slice-2b1 D2): explicit ToTable/HasColumnName (no naming package).
// COLLATE "C" on every hlc/effective_op_hlc column so byte order matches the HLC order.

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.AggregateType).HasColumnName("aggregate_type");
        builder.Property(x => x.AggregateId).HasColumnName("aggregate_id");
        builder.Property(x => x.OperationId).HasColumnName("operation_id");
        builder.Property(x => x.OwnerId).HasColumnName("owner_id");
        builder.Property(x => x.ScopeId).HasColumnName("scope_id");
        builder.Property(x => x.OldScopeId).HasColumnName("old_scope_id");
        builder.Property(x => x.ActorId).HasColumnName("actor_id");
        builder.Property(x => x.EventType).HasColumnName("event_type");
        builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
        builder.Property(x => x.Hlc).HasColumnName("hlc").UseCollation("C");
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        builder.Property(x => x.SignaledAt).HasColumnName("signaled_at");
        builder.Property(x => x.Attempts).HasColumnName("attempts").HasDefaultValue(0);
        // slice-2b2 D6-1: NO default (SQL now() is banned -- one clock source, TimeProvider). OutboxWriter
        // always writes this column explicitly; a bare INSERT that omits it now fails NOT NULL (correct).
        builder.Property(x => x.AvailableAt).HasColumnName("available_at");
        builder.HasIndex(x => x.OwnerId).HasDatabaseName("ix_outbox_messages_owner_id");
    }
}

public sealed class ProcessedOperationConfiguration : IEntityTypeConfiguration<ProcessedOperation>
{
    public void Configure(EntityTypeBuilder<ProcessedOperation> builder)
    {
        builder.ToTable("processed_operations");
        builder.HasKey(x => new { x.ClientId, x.OperationId });
        builder.Property(x => x.ClientId).HasColumnName("client_id");
        builder.Property(x => x.OperationId).HasColumnName("operation_id");
        builder.Property(x => x.FirstSeenAt).HasColumnName("first_seen_at");
        builder.Property(x => x.ResultCode).HasColumnName("result_code");
        builder.Property(x => x.EffectiveOpHlc).HasColumnName("effective_op_hlc").UseCollation("C");
    }
}

public sealed class SyncClientClockConfiguration : IEntityTypeConfiguration<SyncClientClockRow>
{
    public void Configure(EntityTypeBuilder<SyncClientClockRow> builder)
    {
        builder.ToTable("sync_client_clock");
        builder.HasKey(x => x.ClientId);
        builder.Property(x => x.ClientId).HasColumnName("client_id");
        builder.Property(x => x.Hlc).HasColumnName("hlc").UseCollation("C");
    }
}

public sealed class SyncScalarMetaConfiguration : IEntityTypeConfiguration<SyncScalarMeta>
{
    public void Configure(EntityTypeBuilder<SyncScalarMeta> builder)
    {
        builder.ToTable("sync_scalar_meta");
        builder.HasKey(x => new { x.EntityType, x.EntityId, x.Field });
        builder.Property(x => x.EntityType).HasColumnName("entity_type");
        builder.Property(x => x.EntityId).HasColumnName("entity_id");
        builder.Property(x => x.Field).HasColumnName("field");
        builder.Property(x => x.Value).HasColumnName("value");
        builder.Property(x => x.Hlc).HasColumnName("hlc").UseCollation("C");
        builder.Property(x => x.WinOperationId).HasColumnName("win_operation_id");
    }
}

public sealed class SyncOrsetTagConfiguration : IEntityTypeConfiguration<SyncOrsetTag>
{
    public void Configure(EntityTypeBuilder<SyncOrsetTag> builder)
    {
        builder.ToTable("sync_orset_tags");
        builder.HasKey(x => new { x.EntityType, x.EntityId, x.SetName, x.Element, x.AddTag });
        builder.Property(x => x.EntityType).HasColumnName("entity_type");
        builder.Property(x => x.EntityId).HasColumnName("entity_id");
        builder.Property(x => x.SetName).HasColumnName("set_name");
        builder.Property(x => x.Element).HasColumnName("element");
        builder.Property(x => x.AddTag).HasColumnName("add_tag");
        builder.Property(x => x.Hlc).HasColumnName("hlc").UseCollation("C");
        builder.Property(x => x.Cancelled).HasColumnName("cancelled").HasDefaultValue(false);
    }
}

public sealed class SyncOrsetRemoveConfiguration : IEntityTypeConfiguration<SyncOrsetRemove>
{
    public void Configure(EntityTypeBuilder<SyncOrsetRemove> builder)
    {
        builder.ToTable("sync_orset_removes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.EntityType).HasColumnName("entity_type");
        builder.Property(x => x.EntityId).HasColumnName("entity_id");
        builder.Property(x => x.SetName).HasColumnName("set_name");
        builder.Property(x => x.Element).HasColumnName("element");
        builder.Property(x => x.Hlc).HasColumnName("hlc").UseCollation("C");
    }
}

public sealed class SyncGcStateConfiguration : IEntityTypeConfiguration<SyncGcState>
{
    public void Configure(EntityTypeBuilder<SyncGcState> builder)
    {
        builder.ToTable("sync_gc_state");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.GcHorizonSeq).HasColumnName("gc_horizon_seq").HasDefaultValue(0L);
    }
}

// IS-EMRI-o86-D D1: SyncGcStateConfiguration'in birebir deseni -- PK yalniz user_id (tekil-satir
// CHECK kisiti yok, bu tablo kullanici-basina cok-satirlidir). horizon_xid ayni gc_horizon_xid gibi
// bu Configuration'da GECMEZ, migration'da RAW SQL ile eklenir (EF'in xid8 destegi yok).
public sealed class UserResyncHorizonConfiguration : IEntityTypeConfiguration<UserResyncHorizon>
{
    public void Configure(EntityTypeBuilder<UserResyncHorizon> builder)
    {
        builder.ToTable("user_resync_horizon");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).HasColumnName("user_id").ValueGeneratedNever();
        builder.Property(x => x.HorizonSeq).HasColumnName("horizon_seq").HasDefaultValue(0L);
    }
}

// GOREV slice-3a D1: materialized read rows (ADR 0002 K2-I2). Position/tag columns COLLATE "C" (D3b) --
// fractional-index keys carry punctuation a linguistic collation may ignore at the primary level;
// OrSetField already compares element identity via StringComparer.Ordinal.

public sealed class TaskRowConfiguration : IEntityTypeConfiguration<TaskRow>
{
    public void Configure(EntityTypeBuilder<TaskRow> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(x => x.EntityId);
        builder.Property(x => x.EntityId).HasColumnName("entity_id").ValueGeneratedNever();
        builder.Property(x => x.OwnerId).HasColumnName("owner_id");
        builder.Property(x => x.Title).HasColumnName("title");
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.Priority).HasColumnName("priority");
        builder.Property(x => x.DueAt).HasColumnName("due_at");
        builder.Property(x => x.RemindAt).HasColumnName("remind_at");
        builder.Property(x => x.ProjectId).HasColumnName("project_id");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(x => x.RecurrenceRule).HasColumnName("recurrence_rule");
        builder.Property(x => x.ListPos).HasColumnName("list_pos").UseCollation("C");
        builder.Property(x => x.BoardPos).HasColumnName("board_pos").UseCollation("C");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.HasDeleteEditConflict).HasColumnName("has_delete_edit_conflict").HasDefaultValue(false);
        builder.Property(x => x.MalformedFields).HasColumnName("malformed_fields").HasDefaultValueSql("'{}'");
        // Optimizes the default includeDeleted=false path; includeDeleted=true still needs a Sort (is_deleted
        // as a leading column) -- deliberate, the default path is the one optimized (GOREV slice-3a D1).
        builder.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.ListPos, x.EntityId }).HasDatabaseName("ix_tasks_owner_deleted_listpos_entity");
        builder.HasIndex(x => new { x.OwnerId, x.ProjectId }).HasDatabaseName("ix_tasks_owner_project");
        // IS-EMRI-o86-A2 4. bulgu duzeltmesi (SyncPuller.ReadOwnedEntitiesAsync): "project_id IN
        // (project_access alt sorgusu)" -- OWNER'DAN BAGIMSIZ, SADECE project_id'ye gore arar.
        // (owner_id, project_id) bilesik indeksi BURADA yardimci OLMAZ (owner_id ONDE, filtrelenmiyor).
        // EXPLAIN Seq Scan gosterdi (kucuk tabloda dogru plan ama olcekte YAVASLAR) -- §A'nin
        // "Seq Scan'a duserse dar bir indeks ekle" kuralinin AYNISI.
        builder.HasIndex(x => x.ProjectId).HasDatabaseName("ix_tasks_project_id");
    }
}

public sealed class TaskListRowConfiguration : IEntityTypeConfiguration<TaskListRow>
{
    public void Configure(EntityTypeBuilder<TaskListRow> builder)
    {
        builder.ToTable("task_lists");
        builder.HasKey(x => x.EntityId);
        builder.Property(x => x.EntityId).HasColumnName("entity_id").ValueGeneratedNever();
        builder.Property(x => x.OwnerId).HasColumnName("owner_id");
        builder.Property(x => x.Name).HasColumnName("name");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(x => x.Pos).HasColumnName("pos").UseCollation("C");
        builder.Property(x => x.HasDeleteEditConflict).HasColumnName("has_delete_edit_conflict").HasDefaultValue(false);
        builder.Property(x => x.MalformedFields).HasColumnName("malformed_fields").HasDefaultValueSql("'{}'");
        builder.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.Pos, x.EntityId }).HasDatabaseName("ix_task_lists_owner_deleted_pos_entity");
    }
}

// IS-EMRI-o85-B: TaskListRowConfiguration'in birebir deseni + color sutunu (registry'de Project'in
// "color" scalar'i zaten kayitli).
public sealed class ProjectRowConfiguration : IEntityTypeConfiguration<ProjectRow>
{
    public void Configure(EntityTypeBuilder<ProjectRow> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(x => x.EntityId);
        builder.Property(x => x.EntityId).HasColumnName("entity_id").ValueGeneratedNever();
        builder.Property(x => x.OwnerId).HasColumnName("owner_id");
        builder.Property(x => x.Name).HasColumnName("name");
        builder.Property(x => x.Color).HasColumnName("color");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(x => x.Pos).HasColumnName("pos").UseCollation("C");
        builder.Property(x => x.HasDeleteEditConflict).HasColumnName("has_delete_edit_conflict").HasDefaultValue(false);
        builder.Property(x => x.MalformedFields).HasColumnName("malformed_fields").HasDefaultValueSql("'{}'");
        builder.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.Pos, x.EntityId }).HasDatabaseName("ix_projects_owner_deleted_pos_entity");
    }
}

public sealed class TaskTagRowConfiguration : IEntityTypeConfiguration<TaskTagRow>
{
    public void Configure(EntityTypeBuilder<TaskTagRow> builder)
    {
        builder.ToTable("task_tags");
        builder.HasKey(x => new { x.TaskId, x.Tag });
        builder.Property(x => x.TaskId).HasColumnName("task_id");
        // K2-E5 soft-ref (named pin, GOREV slice-3a D1): no FK to tasks.entity_id -- entity-cross-reference
        // is soft everywhere else in this schema (outbox aggregate_id, sync_scalar_meta entity_id, ...).
        builder.Property(x => x.Tag).HasColumnName("tag").UseCollation("C");
    }
}

// IS-EMRI-o86-A §C1: TaskTagRowConfiguration'in birebir deseni. PK (project_id, user_id); pull
// sorgusu user_id'den gittigi icin AYRICA ters-sirali indeks (ix_project_members_user_project).
public sealed class ProjectMemberRowConfiguration : IEntityTypeConfiguration<ProjectMemberRow>
{
    public void Configure(EntityTypeBuilder<ProjectMemberRow> builder)
    {
        builder.ToTable("project_members");
        builder.HasKey(x => new { x.ProjectId, x.UserId });
        builder.Property(x => x.ProjectId).HasColumnName("project_id");
        // K2-E5 soft-ref deseni (TaskTagRowConfiguration'daki gibi): projects.entity_id'ye FK yok.
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.HasIndex(x => new { x.UserId, x.ProjectId }).HasDatabaseName("ix_project_members_user_project");
    }
}

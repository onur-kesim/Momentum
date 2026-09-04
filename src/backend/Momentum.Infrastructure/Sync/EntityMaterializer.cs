using Momentum.Application.Abstractions.Sync;
using Momentum.Domain.Sync;
using Momentum.Infrastructure.Persistence;

namespace Momentum.Infrastructure.Sync;

/// <summary>
/// GOREV slice-3a D3 (ADR 0002 K2-I2). TAM-SATIR UPSERT (PAZARLIKSIZ): ignores which channels
/// <paramref name="op"/>'s delta touched -- always projects the ENTIRE resolved <see cref="EntityState"/>
/// via <see cref="TaskProjection.From"/>/<see cref="TaskListProjection.From"/> and writes every column
/// (mutant-1's target: writing only touched columns leaves a group's REPLACE-deleted member stale, since
/// <c>ResolvedGroupField.Apply</c> replaces the whole dictionary but a delta-shaped writer would not see
/// the now-absent member at all). <c>owner_id</c> is DELIBERATELY absent from every <c>DO UPDATE SET</c>
/// (F2: first writer keeps ownership -- a temporary policy, named in ADR 0002).
/// </summary>
public sealed class EntityMaterializer(SyncDbContext db) : IEntityMaterializer
{
    public async Task MaterializeAsync(ChangeOperation op, EntityState state, Guid ownerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(state);

        switch (op.EntityType)
        {
            case "Task":
                await MaterializeTaskAsync(op.EntityId, state, ownerId, cancellationToken);
                break;
            case "TaskList":
                await MaterializeTaskListAsync(op.EntityId, state, ownerId, cancellationToken);
                break;
            case "Project":
                await MaterializeProjectAsync(op.EntityId, state, ownerId, cancellationToken);
                break;
            default:
                break; // D6 anchor: Tag/unrecognized entityType -- silent no-op, zero new rows
        }
    }

    private async Task MaterializeTaskAsync(Guid entityId, EntityState state, Guid ownerId, CancellationToken cancellationToken)
    {
        var projection = TaskProjection.From(entityId, state);

        await using var command = await db.CreateRawCommandAsync(
            "INSERT INTO tasks (entity_id, owner_id, title, notes, priority, due_at, remind_at, project_id, " +
            "is_deleted, recurrence_rule, list_pos, board_pos, status, completed_at, has_delete_edit_conflict, malformed_fields) " +
            "VALUES (@id, @owner, @title, @notes, @priority, @dueAt, @remindAt, @projectId, @isDeleted, @recurrenceRule, " +
            "@listPos, @boardPos, @status, @completedAt, @hasConflict, @malformed) " +
            "ON CONFLICT (entity_id) DO UPDATE SET " +
            "title = excluded.title, notes = excluded.notes, priority = excluded.priority, due_at = excluded.due_at, " +
            "remind_at = excluded.remind_at, project_id = excluded.project_id, is_deleted = excluded.is_deleted, " +
            "recurrence_rule = excluded.recurrence_rule, list_pos = excluded.list_pos, board_pos = excluded.board_pos, " +
            "status = excluded.status, completed_at = excluded.completed_at, " +
            "has_delete_edit_conflict = excluded.has_delete_edit_conflict, malformed_fields = excluded.malformed_fields",
            cancellationToken);
        command.Parameters.AddWithValue("id", entityId);
        command.Parameters.AddWithValue("owner", ownerId);
        command.Parameters.AddWithValue("title", (object?)projection.Title ?? DBNull.Value);
        command.Parameters.AddWithValue("notes", (object?)projection.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("priority", (object?)projection.Priority ?? DBNull.Value);
        command.Parameters.AddWithValue("dueAt", (object?)projection.DueAt ?? DBNull.Value);
        command.Parameters.AddWithValue("remindAt", (object?)projection.RemindAt ?? DBNull.Value);
        command.Parameters.AddWithValue("projectId", (object?)projection.ProjectId ?? DBNull.Value);
        command.Parameters.AddWithValue("isDeleted", projection.IsDeleted);
        command.Parameters.AddWithValue("recurrenceRule", (object?)projection.RecurrenceRule ?? DBNull.Value);
        command.Parameters.AddWithValue("listPos", (object?)projection.ListPos ?? DBNull.Value);
        command.Parameters.AddWithValue("boardPos", (object?)projection.BoardPos ?? DBNull.Value);
        command.Parameters.AddWithValue("status", (object?)projection.Status ?? DBNull.Value);
        command.Parameters.AddWithValue("completedAt", (object?)projection.CompletedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("hasConflict", projection.HasDeleteEditConflict);
        command.Parameters.AddWithValue("malformed", projection.MalformedFields.ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);

        await ReplaceTagsAsync(entityId, projection.Tags, cancellationToken);
    }

    /// <summary>task_tags: delete-all-reinsert for this task_id (TAM-SATIR UPSERT's set-channel analog -- no partial delta).</summary>
    private async Task ReplaceTagsAsync(Guid taskId, IReadOnlyList<string> tags, CancellationToken cancellationToken)
    {
        await using (var delete = await db.CreateRawCommandAsync("DELETE FROM task_tags WHERE task_id = @id", cancellationToken))
        {
            delete.Parameters.AddWithValue("id", taskId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var tag in tags)
        {
            await using var insert = await db.CreateRawCommandAsync(
                "INSERT INTO task_tags (task_id, tag) VALUES (@id, @tag)", cancellationToken);
            insert.Parameters.AddWithValue("id", taskId);
            insert.Parameters.AddWithValue("tag", tag);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task MaterializeTaskListAsync(Guid entityId, EntityState state, Guid ownerId, CancellationToken cancellationToken)
    {
        var projection = TaskListProjection.From(entityId, state);

        await using var command = await db.CreateRawCommandAsync(
            "INSERT INTO task_lists (entity_id, owner_id, name, is_deleted, pos, has_delete_edit_conflict, malformed_fields) " +
            "VALUES (@id, @owner, @name, @isDeleted, @pos, @hasConflict, @malformed) " +
            "ON CONFLICT (entity_id) DO UPDATE SET " +
            "name = excluded.name, is_deleted = excluded.is_deleted, pos = excluded.pos, " +
            "has_delete_edit_conflict = excluded.has_delete_edit_conflict, malformed_fields = excluded.malformed_fields",
            cancellationToken);
        command.Parameters.AddWithValue("id", entityId);
        command.Parameters.AddWithValue("owner", ownerId);
        command.Parameters.AddWithValue("name", (object?)projection.Name ?? DBNull.Value);
        command.Parameters.AddWithValue("isDeleted", projection.IsDeleted);
        command.Parameters.AddWithValue("pos", (object?)projection.Pos ?? DBNull.Value);
        command.Parameters.AddWithValue("hasConflict", projection.HasDeleteEditConflict);
        command.Parameters.AddWithValue("malformed", projection.MalformedFields.ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // IS-EMRI-o85-B: MaterializeTaskListAsync'in birebir deseni (+ color sutunu). IS-EMRI-o86-A ile
    // `members` (OrSet) artik ReplaceMembersAsync'e (asagida) yaziliyor -- ReplaceTagsAsync'in birebir deseni.
    private async Task MaterializeProjectAsync(Guid entityId, EntityState state, Guid ownerId, CancellationToken cancellationToken)
    {
        var projection = ProjectProjection.From(entityId, state);

        await using var command = await db.CreateRawCommandAsync(
            "INSERT INTO projects (entity_id, owner_id, name, color, is_deleted, pos, has_delete_edit_conflict, malformed_fields) " +
            "VALUES (@id, @owner, @name, @color, @isDeleted, @pos, @hasConflict, @malformed) " +
            "ON CONFLICT (entity_id) DO UPDATE SET " +
            "name = excluded.name, color = excluded.color, is_deleted = excluded.is_deleted, pos = excluded.pos, " +
            "has_delete_edit_conflict = excluded.has_delete_edit_conflict, malformed_fields = excluded.malformed_fields",
            cancellationToken);
        command.Parameters.AddWithValue("id", entityId);
        command.Parameters.AddWithValue("owner", ownerId);
        command.Parameters.AddWithValue("name", (object?)projection.Name ?? DBNull.Value);
        command.Parameters.AddWithValue("color", (object?)projection.Color ?? DBNull.Value);
        command.Parameters.AddWithValue("isDeleted", projection.IsDeleted);
        command.Parameters.AddWithValue("pos", (object?)projection.Pos ?? DBNull.Value);
        command.Parameters.AddWithValue("hasConflict", projection.HasDeleteEditConflict);
        command.Parameters.AddWithValue("malformed", projection.MalformedFields.ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);

        await ReplaceMembersAsync(entityId, projection.Members, cancellationToken);
    }

    /// <summary>IS-EMRI-o86-A §C2: ReplaceTagsAsync'in birebir deseni -- delete-all-reinsert for this project_id (TAM-SATIR UPSERT's set-channel analog).</summary>
    private async Task ReplaceMembersAsync(Guid projectId, IReadOnlyList<Guid> members, CancellationToken cancellationToken)
    {
        await using (var delete = await db.CreateRawCommandAsync("DELETE FROM project_members WHERE project_id = @id", cancellationToken))
        {
            delete.Parameters.AddWithValue("id", projectId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var userId in members)
        {
            await using var insert = await db.CreateRawCommandAsync(
                "INSERT INTO project_members (project_id, user_id) VALUES (@id, @user)", cancellationToken);
            insert.Parameters.AddWithValue("id", projectId);
            insert.Parameters.AddWithValue("user", userId);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}

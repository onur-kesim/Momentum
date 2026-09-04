using Momentum.Application.Abstractions.Sync;
using Momentum.Domain.Sync;
using Momentum.Infrastructure.Persistence;
using Npgsql;

namespace Momentum.Infrastructure.Sync;

/// <summary>
/// GOREV slice-3a D4. All read-side SQL lives HERE (mutant-4/5/6*/12/15's mutation targets). Sorting is
/// <c>ORDER BY &lt;pos&gt;, entity_id</c> (Postgres ASC = NULLS LAST); the keyset predicate is TWO-BRANCH
/// (PAZARLIKSIZ, D4): no cursor -&gt; unfiltered; cursor.Pos != null -&gt; past-non-null-position rows OR
/// any null-position row; cursor.Pos == null -&gt; only remaining null-position rows past entity_id.
/// </summary>
public sealed class TaskReadStore(SyncDbContext db) : ITaskReadStore
{
    public async Task<IReadOnlyList<TaskProjection>> ListTasksAsync(
        Guid ownerId, Guid? projectId, bool includeDeleted, int limit, TaskKeysetCursor? cursor, CancellationToken cancellationToken)
    {
        var results = new List<TaskProjection>();
        await using (var command = await db.CreateRawCommandAsync(
            "SELECT entity_id, title, notes, priority, due_at, remind_at, project_id, is_deleted, recurrence_rule, " +
            "list_pos, board_pos, status, completed_at, has_delete_edit_conflict, malformed_fields " +
            "FROM tasks " +
            "WHERE owner_id = @ownerId " +
            "AND (@includeDeleted OR NOT is_deleted) " +
            "AND (@projectId::uuid IS NULL OR project_id = @projectId) " +
            "AND (" +
            "  NOT @hasCursor " +
            "  OR (@cursorPos::text IS NOT NULL AND ((list_pos IS NOT NULL AND (list_pos, entity_id) > (@cursorPos::text, @cursorId::uuid)) OR list_pos IS NULL)) " +
            "  OR (@cursorPos::text IS NULL AND (list_pos IS NULL AND entity_id > @cursorId::uuid))" +
            ") " +
            "ORDER BY list_pos, entity_id " +
            "LIMIT @limit",
            cancellationToken))
        {
            command.Parameters.AddWithValue("ownerId", ownerId);
            command.Parameters.AddWithValue("includeDeleted", includeDeleted);
            command.Parameters.AddWithValue("projectId", (object?)projectId ?? DBNull.Value);
            command.Parameters.AddWithValue("hasCursor", cursor is not null);
            command.Parameters.AddWithValue("cursorPos", (object?)cursor?.Pos ?? DBNull.Value);
            command.Parameters.AddWithValue("cursorId", cursor?.EntityId ?? Guid.Empty);
            command.Parameters.AddWithValue("limit", limit);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(ReadTaskRow(reader));
            }
        }

        var tagsByTask = await ReadTagsAsync(results.Select(t => t.EntityId).ToList(), cancellationToken);
        return results.Select(t => t with { Tags = tagsByTask.GetValueOrDefault(t.EntityId, []) }).ToList();
    }

    public async Task<TaskProjection?> GetTaskByIdAsync(Guid ownerId, Guid entityId, CancellationToken cancellationToken)
    {
        TaskProjection? task = null;
        await using (var command = await db.CreateRawCommandAsync(
            "SELECT entity_id, title, notes, priority, due_at, remind_at, project_id, is_deleted, recurrence_rule, " +
            "list_pos, board_pos, status, completed_at, has_delete_edit_conflict, malformed_fields " +
            "FROM tasks WHERE owner_id = @ownerId AND entity_id = @entityId",
            cancellationToken))
        {
            command.Parameters.AddWithValue("ownerId", ownerId);
            command.Parameters.AddWithValue("entityId", entityId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                task = ReadTaskRow(reader);
            }
        }

        if (task is null)
        {
            return null;
        }

        var tagsByTask = await ReadTagsAsync([task.EntityId], cancellationToken);
        return task with { Tags = tagsByTask.GetValueOrDefault(task.EntityId, []) };
    }

    public async Task<IReadOnlyList<TaskListProjection>> ListTaskListsAsync(
        Guid ownerId, bool includeDeleted, int limit, TaskKeysetCursor? cursor, CancellationToken cancellationToken)
    {
        await using var command = await db.CreateRawCommandAsync(
            "SELECT entity_id, name, is_deleted, pos, has_delete_edit_conflict, malformed_fields " +
            "FROM task_lists " +
            "WHERE owner_id = @ownerId " +
            "AND (@includeDeleted OR NOT is_deleted) " +
            "AND (" +
            "  NOT @hasCursor " +
            "  OR (@cursorPos::text IS NOT NULL AND ((pos IS NOT NULL AND (pos, entity_id) > (@cursorPos::text, @cursorId::uuid)) OR pos IS NULL)) " +
            "  OR (@cursorPos::text IS NULL AND (pos IS NULL AND entity_id > @cursorId::uuid))" +
            ") " +
            "ORDER BY pos, entity_id " +
            "LIMIT @limit",
            cancellationToken);
        command.Parameters.AddWithValue("ownerId", ownerId);
        command.Parameters.AddWithValue("includeDeleted", includeDeleted);
        command.Parameters.AddWithValue("hasCursor", cursor is not null);
        command.Parameters.AddWithValue("cursorPos", (object?)cursor?.Pos ?? DBNull.Value);
        command.Parameters.AddWithValue("cursorId", cursor?.EntityId ?? Guid.Empty);
        command.Parameters.AddWithValue("limit", limit);

        var results = new List<TaskListProjection>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new TaskListProjection(
                EntityId: reader.GetGuid(0),
                Name: reader.IsDBNull(1) ? null : reader.GetString(1),
                IsDeleted: reader.GetBoolean(2),
                Pos: reader.IsDBNull(3) ? null : reader.GetString(3),
                HasDeleteEditConflict: reader.GetBoolean(4),
                MalformedFields: reader.GetFieldValue<string[]>(5)));
        }

        return results;
    }

    // IS-EMRI-o85-B: ListTaskListsAsync'in birebir SQL'i (+ color sutunu). Ayni iki-dalli keyset
    // kurali PAZARLIKSIZ tekrarlanir -- "OR pos IS NULL" kolu DUSMEZ (null bolmesini cokertir).
    public async Task<IReadOnlyList<ProjectProjection>> ListProjectsAsync(
        Guid ownerId, bool includeDeleted, int limit, TaskKeysetCursor? cursor, CancellationToken cancellationToken)
    {
        await using var command = await db.CreateRawCommandAsync(
            "SELECT entity_id, name, color, is_deleted, pos, has_delete_edit_conflict, malformed_fields " +
            "FROM projects " +
            "WHERE owner_id = @ownerId " +
            "AND (@includeDeleted OR NOT is_deleted) " +
            "AND (" +
            "  NOT @hasCursor " +
            "  OR (@cursorPos::text IS NOT NULL AND ((pos IS NOT NULL AND (pos, entity_id) > (@cursorPos::text, @cursorId::uuid)) OR pos IS NULL)) " +
            "  OR (@cursorPos::text IS NULL AND (pos IS NULL AND entity_id > @cursorId::uuid))" +
            ") " +
            "ORDER BY pos, entity_id " +
            "LIMIT @limit",
            cancellationToken);
        command.Parameters.AddWithValue("ownerId", ownerId);
        command.Parameters.AddWithValue("includeDeleted", includeDeleted);
        command.Parameters.AddWithValue("hasCursor", cursor is not null);
        command.Parameters.AddWithValue("cursorPos", (object?)cursor?.Pos ?? DBNull.Value);
        command.Parameters.AddWithValue("cursorId", cursor?.EntityId ?? Guid.Empty);
        command.Parameters.AddWithValue("limit", limit);

        var results = new List<ProjectProjection>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ProjectProjection(
                EntityId: reader.GetGuid(0),
                Name: reader.IsDBNull(1) ? null : reader.GetString(1),
                Color: reader.IsDBNull(2) ? null : reader.GetString(2),
                IsDeleted: reader.GetBoolean(3),
                Pos: reader.IsDBNull(4) ? null : reader.GetString(4),
                HasDeleteEditConflict: reader.GetBoolean(5),
                MalformedFields: reader.GetFieldValue<string[]>(6),
                // IS-EMRI-o86-A: GET /v1/projects vitrin/API paritesi icindir, uyelik dondurmez
                // (§F kapsam disi -- lookup ayri bir uc); bu read-yolu Members'i hic sormaz.
                Members: []));
        }

        return results;
    }

    private static TaskProjection ReadTaskRow(NpgsqlDataReader reader) => new(
        EntityId: reader.GetGuid(0),
        Title: reader.IsDBNull(1) ? null : reader.GetString(1),
        Notes: reader.IsDBNull(2) ? null : reader.GetString(2),
        Priority: reader.IsDBNull(3) ? null : reader.GetInt32(3),
        DueAt: reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
        RemindAt: reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
        ProjectId: reader.IsDBNull(6) ? null : reader.GetGuid(6),
        IsDeleted: reader.GetBoolean(7),
        RecurrenceRule: reader.IsDBNull(8) ? null : reader.GetString(8),
        ListPos: reader.IsDBNull(9) ? null : reader.GetString(9),
        BoardPos: reader.IsDBNull(10) ? null : reader.GetString(10),
        Status: reader.IsDBNull(11) ? null : reader.GetString(11),
        CompletedAt: reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
        HasDeleteEditConflict: reader.GetBoolean(13),
        MalformedFields: reader.GetFieldValue<string[]>(14),
        Tags: []); // filled in by the caller via ReadTagsAsync

    private async Task<Dictionary<Guid, IReadOnlyList<string>>> ReadTagsAsync(IReadOnlyList<Guid> taskIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, IReadOnlyList<string>>();
        if (taskIds.Count == 0)
        {
            return result;
        }

        await using var command = await db.CreateRawCommandAsync(
            "SELECT task_id, tag FROM task_tags WHERE task_id = ANY(@ids) ORDER BY task_id, tag", cancellationToken);
        command.Parameters.AddWithValue("ids", taskIds.ToArray());

        var grouped = new Dictionary<Guid, List<string>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var taskId = reader.GetGuid(0);
            if (!grouped.TryGetValue(taskId, out var list))
            {
                list = [];
                grouped[taskId] = list;
            }

            list.Add(reader.GetString(1));
        }

        foreach (var (taskId, tags) in grouped)
        {
            result[taskId] = tags;
        }

        return result;
    }
}

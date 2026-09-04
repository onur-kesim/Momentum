using Momentum.Application.Abstractions.Sync;
using Momentum.Domain.Sync;
using Momentum.Infrastructure.Persistence;

namespace Momentum.Infrastructure.Sync;

/// <summary>
/// Raw-SQL sync store (GOREV slice-2b1 D2/D4). Advisory locks in the FIXED client->entity order,
/// hydrate via <see cref="SyncRowHydration"/>, and persist the delta the DOMAIN decided with
/// UNCONDITIONAL upserts (no SQL LWW — correctness comes from the entity lock + the Domain decision, §5).
/// </summary>
public sealed class SyncStore(SyncDbContext db) : ISyncStore
{
    public async Task LockClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        await using var command = await db.CreateRawCommandAsync(
            "SELECT pg_advisory_xact_lock(hashtextextended('sync_client:' || @c::text, 0))", cancellationToken);
        command.Parameters.AddWithValue("c", clientId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task LockEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken)
    {
        await using var command = await db.CreateRawCommandAsync(
            "SELECT pg_advisory_xact_lock(hashtextextended('sync_entity:' || @t || ':' || @e::text, 0))", cancellationToken);
        command.Parameters.AddWithValue("t", entityType);
        command.Parameters.AddWithValue("e", entityId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task HydrateAsync(EntityState target, string entityType, Guid entityId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        return SyncRowHydration.HydrateAsync(db, target, entityType, entityId, cancellationToken);
    }

    public async Task PersistDeltaAsync(ChangeOperation op, EntityState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(state);

        foreach (var field in op.Fields.Keys)
        {
            if (state.Fields.TryGetValue(field, out var register) && register.HasValue)
            {
                await UpsertScalarAsync(op.EntityType, op.EntityId, field, register.Value, register.Key, cancellationToken);
            }
        }

        foreach (var field in op.Order.Keys)
        {
            if (state.Orders.TryGetValue(field, out var register) && register.HasValue)
            {
                await UpsertScalarAsync(op.EntityType, op.EntityId, field, register.Value, register.Key, cancellationToken);
            }
        }

        foreach (var groupName in op.Groups.Keys)
        {
            if (state.Groups.TryGetValue(groupName, out var group) && group.HasValue)
            {
                await PersistGroupAsync(op.EntityType, op.EntityId, groupName, group, cancellationToken);
            }
        }

        foreach (var (setName, delta) in op.Sets)
        {
            await PersistSetAsync(op.EntityType, op.EntityId, setName, delta, state, cancellationToken);
        }
    }

    private async Task UpsertScalarAsync(string entityType, Guid entityId, string field, string? value, HlcKey key, CancellationToken cancellationToken)
    {
        await using var command = await db.CreateRawCommandAsync(
            "INSERT INTO sync_scalar_meta (entity_type, entity_id, field, value, hlc, win_operation_id) " +
            "VALUES (@t, @e, @f, @v, @h, @w) " +
            "ON CONFLICT (entity_type, entity_id, field) DO UPDATE SET value = excluded.value, hlc = excluded.hlc, win_operation_id = excluded.win_operation_id",
            cancellationToken);
        command.Parameters.AddWithValue("t", entityType);
        command.Parameters.AddWithValue("e", entityId);
        command.Parameters.AddWithValue("f", field);
        command.Parameters.AddWithValue("v", (object?)value ?? DBNull.Value);
        command.Parameters.AddWithValue("h", key.Hlc.Encode());
        command.Parameters.AddWithValue("w", key.OperationId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task PersistGroupAsync(string entityType, Guid entityId, string groupName, ResolvedGroupField group, CancellationToken cancellationToken)
    {
        // Marker row (field == group name, value NULL) is the SOLE authority for the group HlcKey (B5).
        await UpsertScalarAsync(entityType, entityId, groupName, null, group.Key, cancellationToken);

        foreach (var (member, value) in group.Fields)
        {
            await UpsertScalarAsync(entityType, entityId, $"{groupName}.{member}", value, group.Key, cancellationToken);
        }

        // REPLACE: delete member rows the winner did not write (registry names are dotless -> no false positive).
        var keep = group.Fields.Keys.Select(member => $"{groupName}.{member}").ToArray();
        await using var command = await db.CreateRawCommandAsync(
            "DELETE FROM sync_scalar_meta WHERE entity_type = @t AND entity_id = @e AND field LIKE @prefix AND field <> ALL(@keep)",
            cancellationToken);
        command.Parameters.AddWithValue("t", entityType);
        command.Parameters.AddWithValue("e", entityId);
        command.Parameters.AddWithValue("prefix", groupName + ".%");
        command.Parameters.AddWithValue("keep", keep);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task PersistSetAsync(string entityType, Guid entityId, string setName, SetDelta delta, EntityState state, CancellationToken cancellationToken)
    {
        state.TryGetSet(setName, out var set);

        foreach (var add in delta.Adds)
        {
            Hlc? hlc = add.Hlc;
            var cancelled = false;
            if (set is not null && set.TryGetTag(add.Element, add.Tag, out var resolvedHlc, out var resolvedCancelled))
            {
                hlc = resolvedHlc;
                cancelled = resolvedCancelled;
            }

            await UpsertTagAsync(entityType, entityId, setName, add.Element, add.Tag, hlc, cancelled, cancellationToken);
        }

        foreach (var remove in delta.Removes)
        {
            foreach (var observed in remove.Observed)
            {
                await TombstoneTagAsync(entityType, entityId, setName, remove.Element, observed, cancellationToken);
            }

            await InsertRemoveStampAsync(entityType, entityId, setName, remove.Element, remove.Hlc, cancellationToken);
        }
    }

    private async Task UpsertTagAsync(string entityType, Guid entityId, string setName, string element, Guid tag, Hlc? hlc, bool cancelled, CancellationToken cancellationToken)
    {
        // A later add of an already-cancelled tag ONLY fills hlc (cancelled stays -> born-dead stamp kept, B4).
        // ADR 0002 ERRATA E-1b: GREATEST is a NAMED EXCEPTION to "no SQL LWW/CRDT implementation" (same
        // pattern as sync_client_clock's K2-A4 GREATEST) -- the Domain decision is already made (the
        // caller passed the resolved max), but a caller that persists WITHOUT hydrating first (a stale
        // direct write) must not be able to drag an existing higher stamp backwards. Text GREATEST is
        // safe here: Hlc.Encode() is fixed-width/lowercase-hex and the column is COLLATE "C" (D3b), so
        // byte order matches Hlc.CompareTo; Postgres GREATEST ignores NULL (a tombstone-only row's NULL
        // hlc never beats a real stamp).
        // NOTE (asymmetry, intentionally left alone per GOREV slice-2c D3): this DO UPDATE does not
        // touch `cancelled` -- safe today because adds always run before removes in one op's delta, but
        // that ordering is an implicit dependency, not enforced by this statement.
        await using var command = await db.CreateRawCommandAsync(
            "INSERT INTO sync_orset_tags (entity_type, entity_id, set_name, element, add_tag, hlc, cancelled) " +
            "VALUES (@t, @e, @s, @el, @tag, @h, @c) " +
            "ON CONFLICT (entity_type, entity_id, set_name, element, add_tag) " +
            "DO UPDATE SET hlc = GREATEST(excluded.hlc, sync_orset_tags.hlc)",
            cancellationToken);
        AddSetKey(command, entityType, entityId, setName, element, tag);
        command.Parameters.AddWithValue("h", (object?)hlc?.Encode() ?? DBNull.Value);
        command.Parameters.AddWithValue("c", cancelled);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task TombstoneTagAsync(string entityType, Guid entityId, string setName, string element, Guid tag, CancellationToken cancellationToken)
    {
        // Unseen tag -> (hlc NULL, cancelled=true) marker; existing tag -> just cancel (keep hlc). (B4)
        await using var command = await db.CreateRawCommandAsync(
            "INSERT INTO sync_orset_tags (entity_type, entity_id, set_name, element, add_tag, hlc, cancelled) " +
            "VALUES (@t, @e, @s, @el, @tag, NULL, true) " +
            "ON CONFLICT (entity_type, entity_id, set_name, element, add_tag) DO UPDATE SET cancelled = true",
            cancellationToken);
        AddSetKey(command, entityType, entityId, setName, element, tag);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertRemoveStampAsync(string entityType, Guid entityId, string setName, string element, Hlc hlc, CancellationToken cancellationToken)
    {
        await using var command = await db.CreateRawCommandAsync(
            "INSERT INTO sync_orset_removes (id, entity_type, entity_id, set_name, element, hlc) VALUES (@id, @t, @e, @s, @el, @h)",
            cancellationToken);
        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("t", entityType);
        command.Parameters.AddWithValue("e", entityId);
        command.Parameters.AddWithValue("s", setName);
        command.Parameters.AddWithValue("el", element);
        command.Parameters.AddWithValue("h", hlc.Encode());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddSetKey(Npgsql.NpgsqlCommand command, string entityType, Guid entityId, string setName, string element, Guid tag)
    {
        command.Parameters.AddWithValue("t", entityType);
        command.Parameters.AddWithValue("e", entityId);
        command.Parameters.AddWithValue("s", setName);
        command.Parameters.AddWithValue("el", element);
        command.Parameters.AddWithValue("tag", tag);
    }

    public async Task<bool> IsProjectOwnerAsync(Guid projectId, Guid actorId, CancellationToken cancellationToken)
    {
        await using var command = await db.CreateRawCommandAsync(
            "SELECT EXISTS (SELECT 1 FROM projects WHERE entity_id = @p AND owner_id = @a)", cancellationToken);
        command.Parameters.AddWithValue("p", projectId);
        command.Parameters.AddWithValue("a", actorId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    // IS-EMRI-o86-A2 §B: project_access GORUNUMU uzerinden tek EXISTS (ad/imza SABIT kalir --
    // arayuz ve cagri yerleri degismez). IsProjectOwnerAsync (yukarida) DEGISMEZ -- members yazimi
    // yalniz GERCEK sahibindir, §C3.
    public async Task<bool> IsProjectOwnerOrMemberAsync(Guid projectId, Guid actorId, CancellationToken cancellationToken)
    {
        await using var command = await db.CreateRawCommandAsync(
            "SELECT EXISTS (SELECT 1 FROM project_access WHERE project_id = @p AND user_id = @a)", cancellationToken);
        command.Parameters.AddWithValue("p", projectId);
        command.Parameters.AddWithValue("a", actorId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    // IS-EMRI-o86-A3 §A: FAIL-CLOSED -- `tasks` satiri yoksa (materyalizasyon boslugu, ornegin
    // henuz hic Applied olmus bir op'u olmayan bir entityId) EXISTS false doner, yetki ACMAZ.
    public async Task<bool> IsTaskOwnerAsync(Guid taskId, Guid actorId, CancellationToken cancellationToken)
    {
        await using var command = await db.CreateRawCommandAsync(
            "SELECT EXISTS (SELECT 1 FROM tasks WHERE entity_id = @t AND owner_id = @a)", cancellationToken);
        command.Parameters.AddWithValue("t", taskId);
        command.Parameters.AddWithValue("a", actorId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}

using Momentum.Application.Features.Sync;
using Momentum.Domain.Sync;
using Shouldly;
using Xunit;

namespace Momentum.Persistence.Tests;

/// <summary>
/// D5-a (GOREV slice-3a, ADR 0002 K2-I2): the PERSISTENCE round-trip gate. For each entity: hydrate from
/// persistence -&gt; <see cref="TaskProjection.From"/>/<see cref="TaskListProjection.From"/>, and SEPARATELY
/// read the materialized row via raw SQL (never via <c>From</c> -- that would be tautological, comparing
/// From(hydrate()) against itself). Compared FIELD BY FIELD (Shouldly's collection-aware ShouldBe for
/// list members) -- NEVER record <c>==</c> (the synthesized Equals uses REFERENCE equality on
/// <c>MalformedFields</c>/<c>Tags</c>, which would make the assertion FAIL unconditionally).
/// BEYAN (ZORUNLU): this gate measures the PERSISTENCE CHAIN (does meta capture state fully? does the
/// full-row write leave a stale column?) -- it does NOT measure the projection FUNCTION itself, since
/// both sides call the SAME <c>From</c>; any mutation inside <c>From</c> breaks both sides identically.
/// Its only gated mutant is mutant-1 (delta-shaped writer leaves a REPLACE-shrunk group member stale).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class MaterializationRoundTripTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Task_materialization_round_trips_field_by_field_against_the_hydrated_projection()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var actor = Guid.NewGuid();
        var entity = Guid.NewGuid();
        var tag = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await app.SyncAsync(actor, Wire.PushNoPull(actor,
            Wire.TaskFields(Guid.CreateVersion7(), actor, entity, actor, 1,
                ("title", "Buy milk"), ("notes", "2% preferred"), ("priority", "3"),
                ("dueAt", "2026-07-19T10:00:00Z"), ("projectId", projectId.ToString()))));
        await app.SyncAsync(actor, Wire.PushNoPull(actor, OrderOp(actor, entity, "Task", "listPos", "m1", 2)));
        await app.SyncAsync(actor, Wire.PushNoPull(actor,
            Wire.TaskGroup(Guid.CreateVersion7(), actor, entity, actor, 3, ("status", "done"), ("completedAt", "2026-07-19T11:00:00Z"))));
        // FEWER-member REPLACE (mutant-1's kill surface): a delta-shaped writer that only touches
        // "this op's own keys" would leave completed_at STALE (non-null) instead of clearing it.
        await app.SyncAsync(actor, Wire.PushNoPull(actor,
            Wire.TaskGroup(Guid.CreateVersion7(), actor, entity, actor, 5, ("status", "redo"))));
        await app.SyncAsync(actor, Wire.PushNoPull(actor,
            Wire.TaskSet(Guid.CreateVersion7(), actor, entity, actor, 4, adds: [new WireSetAdd("el0", tag, Wire.Hlc(actor, 4))], removes: null)));

        var hydrated = await app.HydrateAsync("Task", entity);
        var projected = TaskProjection.From(entity, hydrated);

        // Independent raw-SQL read of the materialized row -- TaskProjection.From is NEVER called here.
        (await Db.ScalarAsync<string>(connectionString, "SELECT title FROM tasks WHERE entity_id = @e", ("e", entity))).ShouldBe(projected.Title);
        (await Db.ScalarAsync<string>(connectionString, "SELECT notes FROM tasks WHERE entity_id = @e", ("e", entity))).ShouldBe(projected.Notes);
        (await Db.ScalarAsync<int?>(connectionString, "SELECT priority FROM tasks WHERE entity_id = @e", ("e", entity))).ShouldBe(projected.Priority);
        (await ReadTimestampAsync(connectionString, "SELECT due_at FROM tasks WHERE entity_id = @e", entity)).ShouldBe(projected.DueAt);
        (await ReadTimestampAsync(connectionString, "SELECT remind_at FROM tasks WHERE entity_id = @e", entity)).ShouldBe(projected.RemindAt);
        (await Db.ScalarAsync<Guid?>(connectionString, "SELECT project_id FROM tasks WHERE entity_id = @e", ("e", entity))).ShouldBe(projected.ProjectId);
        (await Db.ScalarAsync<bool>(connectionString, "SELECT is_deleted FROM tasks WHERE entity_id = @e", ("e", entity))).ShouldBe(projected.IsDeleted);
        (await Db.ScalarAsync<string>(connectionString, "SELECT recurrence_rule FROM tasks WHERE entity_id = @e", ("e", entity))).ShouldBe(projected.RecurrenceRule);
        (await Db.ScalarAsync<string>(connectionString, "SELECT list_pos FROM tasks WHERE entity_id = @e", ("e", entity))).ShouldBe(projected.ListPos);
        (await Db.ScalarAsync<string>(connectionString, "SELECT board_pos FROM tasks WHERE entity_id = @e", ("e", entity))).ShouldBe(projected.BoardPos);
        (await Db.ScalarAsync<string>(connectionString, "SELECT status FROM tasks WHERE entity_id = @e", ("e", entity))).ShouldBe(projected.Status);
        (await ReadTimestampAsync(connectionString, "SELECT completed_at FROM tasks WHERE entity_id = @e", entity)).ShouldBe(projected.CompletedAt);
        (await Db.ScalarAsync<bool>(connectionString, "SELECT has_delete_edit_conflict FROM tasks WHERE entity_id = @e", ("e", entity))).ShouldBe(projected.HasDeleteEditConflict);
        (await Db.ScalarAsync<string[]>(connectionString, "SELECT malformed_fields FROM tasks WHERE entity_id = @e", ("e", entity))).ShouldBe(projected.MalformedFields);
        (await ReadTagsAsync(connectionString, entity)).ShouldBe(projected.Tags);
    }

    [Fact]
    public async Task TaskList_materialization_round_trips_field_by_field_against_the_hydrated_projection()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var actor = Guid.NewGuid();
        var entity = Guid.NewGuid();

        await app.SyncAsync(actor, Wire.PushNoPull(actor, Wire.Op(Guid.CreateVersion7(), actor, entity, actor, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("Groceries", Wire.Hlc(actor, 1)) },
            entityType: "TaskList")));
        await app.SyncAsync(actor, Wire.PushNoPull(actor, OrderOp(actor, entity, "TaskList", "pos", "m1", 2)));

        var hydrated = await app.HydrateAsync("TaskList", entity);
        var projected = TaskListProjection.From(entity, hydrated);

        (await Db.ScalarAsync<string>(connectionString, "SELECT name FROM task_lists WHERE entity_id = @e", ("e", entity))).ShouldBe(projected.Name);
        (await Db.ScalarAsync<bool>(connectionString, "SELECT is_deleted FROM task_lists WHERE entity_id = @e", ("e", entity))).ShouldBe(projected.IsDeleted);
        (await Db.ScalarAsync<string>(connectionString, "SELECT pos FROM task_lists WHERE entity_id = @e", ("e", entity))).ShouldBe(projected.Pos);
        (await Db.ScalarAsync<bool>(connectionString, "SELECT has_delete_edit_conflict FROM task_lists WHERE entity_id = @e", ("e", entity))).ShouldBe(projected.HasDeleteEditConflict);
        (await Db.ScalarAsync<string[]>(connectionString, "SELECT malformed_fields FROM task_lists WHERE entity_id = @e", ("e", entity))).ShouldBe(projected.MalformedFields);
    }

    /// <summary>
    /// IS-EMRI-o85-B D2+D3 (birlesik test): (1) Project op (name+isDeleted) -> "projects" satiri,
    /// alan alan hidratlanmis projeksiyona karsi dogrulanir; (2) name guncellemesi satiri YERINDE
    /// gunceller (ayni entity_id, ikinci satir DOGMAZ); (3) owner_id DEGISMEZ -- ikinci bir aktorun
    /// (ownerB) ayni entity'ye yazdigi op KABUL edilir (LWW alan degisikligi uygulanir) ama
    /// EntityMaterializer'in DO UPDATE SET'i owner_id'yi ATLADIGI icin sahiplik ownerA'da kalir (F2).
    /// (4) KANAL TESTI (mutant-16'nin muadili): `pos` YALNIZ Orders'tan gelen bir op'ta materyalize
    /// satirda DOLU olmali -- ProjectProjection.Pos'u Fields'ten okumaya ceviren mutant burada OLUR.
    /// </summary>
    [Fact]
    public async Task Project_materialization_round_trips_field_by_field_and_preserves_first_writer_ownership()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();
        var entity = Guid.NewGuid();

        // 1) ownerA yaratir: name + pos (Orders kanalindan).
        await app.SyncAsync(ownerA, Wire.PushNoPull(ownerA, Wire.Op(Guid.CreateVersion7(), ownerA, entity, ownerA, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("İş", Wire.Hlc(ownerA, 1)) },
            entityType: "Project")));
        await app.SyncAsync(ownerA, Wire.PushNoPull(ownerA, OrderOp(ownerA, entity, "Project", "pos", "m1", 2)));

        var hydrated1 = await app.HydrateAsync("Project", entity);
        var projected1 = ProjectProjection.From(entity, hydrated1);

        (await Db.ScalarAsync<string>(connectionString, "SELECT name FROM projects WHERE entity_id = @e", ("e", entity))).ShouldBe("İş");
        (await Db.ScalarAsync<string>(connectionString, "SELECT name FROM projects WHERE entity_id = @e", ("e", entity))).ShouldBe(projected1.Name);
        (await Db.ScalarAsync<bool>(connectionString, "SELECT is_deleted FROM projects WHERE entity_id = @e", ("e", entity))).ShouldBe(false);
        // D3 KANAL KANITI: pos, Orders'tan gelen op'ta DOLU (Fields'e cevrilirse bu satir kirmizi olur).
        (await Db.ScalarAsync<string>(connectionString, "SELECT pos FROM projects WHERE entity_id = @e", ("e", entity))).ShouldBe("m1");
        (await Db.ScalarAsync<string>(connectionString, "SELECT pos FROM projects WHERE entity_id = @e", ("e", entity))).ShouldBe(projected1.Pos);
        (await Db.ScalarAsync<Guid>(connectionString, "SELECT owner_id FROM projects WHERE entity_id = @e", ("e", entity))).ShouldBe(ownerA);

        // 1b) IS-EMRI-o86-A §E: ownerB'nin yazabilmesi icin ownerA onu UYE yapar (members, sahip-yalniz kural).
        await app.SyncAsync(ownerA, Wire.PushNoPull(ownerA, Wire.Op(Guid.CreateVersion7(), ownerA, entity, ownerA, 3,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new([new WireSetAdd(ownerB.ToString(), Guid.CreateVersion7(), Wire.Hlc(ownerA, 3))], null),
            },
            entityType: "Project")));

        // 2) UYE ownerB ayni entity'ye yazar: name gunceller + isDeleted=true isaretler. Kabul edilir
        // (yetkili -- uye), ama sahiplik CALINAMAZ (F2, §E'den BAGIMSIZ bir kural).
        await app.SyncAsync(ownerB, Wire.PushNoPull(ownerB, Wire.Op(Guid.CreateVersion7(), ownerB, entity, ownerB, 3,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal)
            {
                ["name"] = new("İş 2", Wire.Hlc(ownerB, 3)),
                ["isDeleted"] = new("true", Wire.Hlc(ownerB, 3)),
            },
            entityType: "Project")));

        var hydrated2 = await app.HydrateAsync("Project", entity);
        var projected2 = ProjectProjection.From(entity, hydrated2);

        // name AYNI SATIRDA guncellendi (yerinde) -- tek satir kaldigi asagida ayrica dogrulanir.
        (await Db.ScalarAsync<string>(connectionString, "SELECT name FROM projects WHERE entity_id = @e", ("e", entity))).ShouldBe("İş 2");
        (await Db.ScalarAsync<string>(connectionString, "SELECT name FROM projects WHERE entity_id = @e", ("e", entity))).ShouldBe(projected2.Name);
        (await Db.ScalarAsync<bool>(connectionString, "SELECT is_deleted FROM projects WHERE entity_id = @e", ("e", entity))).ShouldBe(true);
        (await Db.ScalarAsync<bool>(connectionString, "SELECT is_deleted FROM projects WHERE entity_id = @e", ("e", entity))).ShouldBe(projected2.IsDeleted);

        // owner_id DEGISMEDI -- ikinci yazan sahiplik ALAMAZ (F2, EntityMaterializer DO UPDATE SET'te owner_id YOK).
        (await Db.ScalarAsync<Guid>(connectionString, "SELECT owner_id FROM projects WHERE entity_id = @e", ("e", entity))).ShouldBe(ownerA);

        // TAM-SATIR UPSERT: ikinci yazim yeni satir DOGURMADI, ayni entity_id icin tam olarak bir satir var.
        (await Db.ScalarAsync<long>(connectionString, "SELECT count(*) FROM projects WHERE entity_id = @e", ("e", entity))).ShouldBe(1L);
    }

    /// <summary>
    /// IS-EMRI-o86-A G2: members ekleme -> project_members satiri DOGAR (POZITIF); kaldirma -> satir
    /// GIDER (NEGATIF). ReplaceTagsAsync/task_tags'in birebir mutant-sinifi.
    /// </summary>
    [Fact]
    public async Task Project_members_add_materializes_row_remove_deletes_it()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var entity = Guid.NewGuid();
        var tag = Guid.CreateVersion7();

        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, entity, owner, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(owner, 1)) },
            entityType: "Project")));

        // Ekleme -- POZITIF: satir DOGAR.
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, entity, owner, 2,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new([new WireSetAdd(member.ToString(), tag, Wire.Hlc(owner, 2))], null),
            },
            entityType: "Project")));

        (await Db.ScalarAsync<long>(connectionString,
            "SELECT count(*) FROM project_members WHERE project_id = @p AND user_id = @m", ("p", entity), ("m", member)))
            .ShouldBe(1L);

        // Kaldirma -- NEGATIF: satir GIDER (bos liste her iddiayi gecirir -- pozitif kontrol yukarida zaten var).
        await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, entity, owner, 3,
            sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
            {
                ["members"] = new(null, [new WireSetRemove(member.ToString(), [tag], Wire.Hlc(owner, 3))]),
            },
            entityType: "Project")));

        (await Db.ScalarAsync<long>(connectionString,
            "SELECT count(*) FROM project_members WHERE project_id = @p AND user_id = @m", ("p", entity), ("m", member)))
            .ShouldBe(0L);
    }

    /// <summary>ORDER KANALI PINI: WireOp built INLINE (Wire has no Order helper).</summary>
    private static WireOp OrderOp(Guid client, Guid entity, string entityType, string field, string value, uint counter) =>
        new(Guid.CreateVersion7(), client, entity, client, entityType, Wire.Hlc(client, counter),
            Fields: null, Sets: null, Groups: null,
            Order: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { [field] = new(value, Wire.Hlc(client, counter)) });

    /// <summary>
    /// Npgsql's ExecuteScalarAsync returns a bare DateTime (Kind=Utc) for timestamptz via Db.ScalarAsync's
    /// generic cast (unlike a reader's GetFieldValue&lt;DateTimeOffset&gt;, which TaskReadStore uses) --
    /// this reads it as DateTime? and converts, so the comparison is apples-to-apples with the projection.
    /// </summary>
    private static async Task<DateTimeOffset?> ReadTimestampAsync(string connectionString, string sql, Guid entityId)
    {
        var value = await Db.ScalarAsync<DateTime?>(connectionString, sql, ("e", entityId));
        return value is { } dt ? new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)) : null;
    }

    private static async Task<List<string>> ReadTagsAsync(string connectionString, Guid taskId)
    {
        await using var connection = await Db.OpenAsync(connectionString);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT tag FROM task_tags WHERE task_id = @id ORDER BY tag";
        command.Parameters.AddWithValue("id", taskId);

        var tags = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tags.Add(reader.GetString(0));
        }

        return tags;
    }
}

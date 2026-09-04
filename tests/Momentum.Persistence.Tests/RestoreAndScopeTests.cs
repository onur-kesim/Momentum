using Momentum.Application.Features.Sync;
using Shouldly;
using Xunit;

namespace Momentum.Persistence.Tests;

[Collection(PostgresCollection.Name)]
public sealed class RestoreAndScopeTests(PostgresFixture fixture)
{
    /// <summary>
    /// H10 (ADR 0002 K2-H10): after "restart" (TRUNCATE sync_client_clock), the clock is restored
    /// PER-CLIENT from that client's OWN max outbox hlc (via the codec's fixed ClientId segment), NOT
    /// the global max. Client1 has the globally-largest hlc; querying client2 must return client2's.
    /// </summary>
    [Fact]
    public async Task H10_restart_restore_is_per_client_not_global()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var client1 = Guid.NewGuid();
        var client2 = Guid.NewGuid();

        await app.SyncAsync(client1, Wire.PushNoPull(client1,
            Wire.TaskField(Guid.CreateVersion7(), client1, Guid.NewGuid(), client1, "title", "a", wallOffset: 1_000_000))); // biggest
        await app.SyncAsync(client2, Wire.PushNoPull(client2,
            Wire.TaskField(Guid.CreateVersion7(), client2, Guid.NewGuid(), client2, "title", "b")));

        await Db.ExecuteAsync(connectionString, "TRUNCATE sync_client_clock");

        var restored = await app.GetClientClockAsync(client2);
        var expected = await Db.ScalarAsync<string>(connectionString,
            "SELECT max(hlc) FROM outbox_messages WHERE substring(hlc from 24 for 32) = @c", ("c", client2.ToString("N")));

        restored.ShouldNotBeNull();
        restored.Value.Encode().ShouldBe(expected); // client2's own max, not client1's global max
    }

    [Fact]
    public async Task H11_projectId_change_records_scope_and_old_scope_columns()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var client = Guid.NewGuid();
        var entity = Guid.NewGuid();
        var project1 = Guid.NewGuid();
        var project2 = Guid.NewGuid();
        var op2 = Guid.CreateVersion7();

        // IS-EMRI-o86-A §E: Task'in hedef scope'u sahip/uye gerektirir -- client'in GERCEK sahibi
        // olmasi icin once iki Project yaratilir.
        await app.SyncAsync(client, Wire.PushNoPull(client, Wire.Op(Guid.CreateVersion7(), client, project1, client, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P1", Wire.Hlc(client, 1)) },
            entityType: "Project")));
        await app.SyncAsync(client, Wire.PushNoPull(client, Wire.Op(Guid.CreateVersion7(), client, project2, client, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P2", Wire.Hlc(client, 1)) },
            entityType: "Project")));

        await app.SyncAsync(client, Wire.PushNoPull(client,
            Wire.TaskField(Guid.CreateVersion7(), client, entity, client, "projectId", project1.ToString(), counter: 1)));
        await app.SyncAsync(client, Wire.PushNoPull(client,
            Wire.TaskField(op2, client, entity, client, "projectId", project2.ToString(), counter: 2)));

        (await Db.ScalarAsync<Guid>(connectionString, "SELECT scope_id FROM outbox_messages WHERE operation_id = @o", ("o", op2)))
            .ShouldBe(project2);
        (await Db.ScalarAsync<Guid>(connectionString, "SELECT old_scope_id FROM outbox_messages WHERE operation_id = @o", ("o", op2)))
            .ShouldBe(project1);
    }

    [Fact]
    public async Task H11_parentless_child_is_accepted_soft_ref()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var client = Guid.NewGuid();
        var project = Guid.NewGuid();

        // IS-EMRI-o86-A2: bu testin ORIJINAL iddiasi ("projectId TAMAMEN var olmayan/erisimsiz bir
        // Projeye isaret eder -> FK yok -> kabul") ARTIK dogru DEGIL -- tam da bu senaryo (yabancinin
        // TAHMIN ETTIGI bir projectId ile yeni gorev dogurmasi) bilerek KAPATILDI (D9OwnerIdVisibilityTests.
        // Stranger_cannot_inject_new_task_into_guessed_project_member_can_H5, ayni sinifin bir uyesi --
        // authorization ve schema-FK-yoklugu AYRI katmanlardir). Bu test artik SADECE schema iddiasini
        // (tasks.project_id -> projects.entity_id foreign key YOKTUR, K2-E5 soft-ref deseni) client'in
        // KENDI (gercek, erisimi olan) projesiyle sinar -- authorization ayrica gerekli, kapanmadi.
        await app.SyncAsync(client, Wire.PushNoPull(client, Wire.Op(Guid.CreateVersion7(), client, project, client, 1,
            fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(client, 1)) },
            entityType: "Project")));

        var response = await app.SyncAsync(client, Wire.PushNoPull(client,
            Wire.TaskField(Guid.CreateVersion7(), client, Guid.NewGuid(), client, "projectId", project.ToString())));

        response.Applied.ShouldHaveSingleItem().Code.ShouldBe("Applied");
    }

    [Fact]
    public async Task H11_channel_jumping_field_is_rejected_and_leaves_state_and_outbox_unchanged()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        await using var app = new SyncTestApp(connectionString);
        var client = Guid.NewGuid();
        var entity = Guid.NewGuid();

        // "status" is a group member -> sending it on the scalar Fields channel is a registry violation.
        var response = await app.SyncAsync(client, Wire.PushNoPull(client,
            Wire.TaskField(Guid.CreateVersion7(), client, entity, client, "status", "done")));

        response.Applied.ShouldHaveSingleItem().Code.ShouldBe("RejectedRegistryViolation");
        (await Db.ScalarAsync<long>(connectionString, "SELECT count(*) FROM sync_scalar_meta WHERE entity_id = @e", ("e", entity))).ShouldBe(0);
        (await Db.ScalarAsync<long>(connectionString, "SELECT count(*) FROM outbox_messages")).ShouldBe(0);
        (await Db.ScalarAsync<long>(connectionString, "SELECT count(*) FROM processed_operations WHERE client_id = @c", ("c", client))).ShouldBe(1); // reject IS recorded
    }
}

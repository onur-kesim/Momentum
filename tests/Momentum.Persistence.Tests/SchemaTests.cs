using Microsoft.EntityFrameworkCore;
using Momentum.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Momentum.Persistence.Tests;

/// <summary>DB-less model validation (ADR 0001 K-F1): building the model must succeed.</summary>
public sealed class ModelValidationTests
{
    [Fact]
    public void Model_is_valid_and_has_fourteen_tables()
    {
        var options = new DbContextOptionsBuilder<SyncDbContext>()
            .UseNpgsql("Host=localhost;Database=x;Username=x;Password=x")
            .Options;

        using var db = new SyncDbContext(options);

        // GOREV slice-3a kriter 2/12 (measured, not assumed): 7 sync tables + tasks/task_lists/task_tags
        // + IS-EMRI-o83 D1'in users/refresh_tokens'i (10 -> 12) + IS-EMRI-o85-B'nin projects'i (12 -> 13)
        // + IS-EMRI-o86-A'nin project_members'i (13 -> 14) + IS-EMRI-o86-D'nin user_resync_horizon'i
        // (14 -> 15, taban BILEREK guncellendi). malformed_fields (List<string> -> text[]) is an EF
        // primitive collection, NOT a separate entity type.
        db.Model.GetEntityTypes().Count().ShouldBe(15); // building IModel throws if invalid
    }
}

[Collection(PostgresCollection.Name)]
public sealed class SchemaTests(PostgresFixture fixture)
{
    [Fact]
    public async Task UuidV7_preserves_time_order_under_order_by()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        var generated = Enumerable.Range(0, 50)
            .Select(i => Guid.CreateVersion7(DateTimeOffset.FromUnixTimeMilliseconds(Wire.BaseWall + i)))
            .ToList();

        await using var connection = await Db.OpenAsync(connectionString);
        await Db.ExecuteAsync(connection, null, "CREATE TEMP TABLE u (id uuid)");
        foreach (var id in generated)
        {
            await Db.ExecuteAsync(connection, null, "INSERT INTO u VALUES (@i)", ("i", id));
        }

        var ordered = new List<Guid>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id FROM u ORDER BY id";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                ordered.Add(reader.GetGuid(0));
            }
        }

        ordered.ShouldBe(generated); // uuid ORDER BY == generation order (index-friendly, K-E1)
    }

    [Fact]
    public async Task Outbox_has_generated_cursor_columns_and_hlc_is_collation_c()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);

        (await Db.ScalarAsync<long>(connectionString,
            "SELECT count(*) FROM information_schema.columns WHERE table_name = 'outbox_messages' AND column_name IN ('commit_xid','server_seq')"))
            .ShouldBe(2);
        (await Db.ScalarAsync<string>(connectionString,
            "SELECT is_identity FROM information_schema.columns WHERE table_name = 'outbox_messages' AND column_name = 'server_seq'"))
            .ShouldBe("YES");
        (await Db.ScalarAsync<string>(connectionString,
            "SELECT collation_name FROM information_schema.columns WHERE table_name = 'outbox_messages' AND column_name = 'hlc'"))
            .ShouldBe("C");
    }

    /// <summary>
    /// D3b (GOREV slice-2c): E-1b's correctness (text GREATEST == Hlc.CompareTo) depends on
    /// COLLATE "C" on the columns it runs against; only <c>outbox_messages.hlc</c> was ever asserted.
    /// A NEW [Fact] (existing test bodies are unchanged, per kabul kriteri 2) covers the other two.
    /// </summary>
    [Fact]
    public async Task Orset_tag_and_client_clock_hlc_columns_are_collation_c()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);

        (await Db.ScalarAsync<string>(connectionString,
            "SELECT collation_name FROM information_schema.columns WHERE table_name = 'sync_orset_tags' AND column_name = 'hlc'"))
            .ShouldBe("C");
        (await Db.ScalarAsync<string>(connectionString,
            "SELECT collation_name FROM information_schema.columns WHERE table_name = 'sync_client_clock' AND column_name = 'hlc'"))
            .ShouldBe("C");
    }

    /// <summary>
    /// D3b (GOREV slice-3a): collation SCHEMA-DECLARATION gate for the new position/tag columns. This
    /// pins the DECLARATION, not runtime behavior -- postgres:17-alpine uses musl libc, whose strcoll
    /// approximates strcmp, so the default collation MAY behave identically to "C" here (see the
    /// behavioral measurement in KANIT/slice-3a for mutant-6's honest bite-surface declaration).
    /// </summary>
    [Fact]
    public async Task Task_position_and_tag_columns_are_collation_c()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);

        foreach (var (table, column) in new[] { ("tasks", "list_pos"), ("tasks", "board_pos"), ("task_lists", "pos"), ("task_tags", "tag") })
        {
            (await Db.ScalarAsync<string>(connectionString,
                "SELECT collation_name FROM information_schema.columns WHERE table_name = @t AND column_name = @c", ("t", table), ("c", column)))
                .ShouldBe("C");
        }
    }
}

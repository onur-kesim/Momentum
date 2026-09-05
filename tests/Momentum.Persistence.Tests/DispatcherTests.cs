using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Momentum.Application.Features.Sync;
using Momentum.Infrastructure.Sync;
using Npgsql;
using Shouldly;
using Xunit;

namespace Momentum.Persistence.Tests;

/// <summary>
/// slice-2b2 D8: the <see cref="OutboxDispatcher"/> gates. All tests use <see cref="RecordingSignalPublisher"/>
/// (ADR 0002 K2-F2 "dispatcher SignalR'ı bilmez") so the dispatcher's behavior is measured deterministically,
/// without a real SignalR transport.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DispatcherTests(PostgresFixture fixture)
{
    /// <summary>
    /// H9 (ADR 0002 K2-H9, v1's kör kapı — YENİDEN TASARLANDI). 25 rows / 25 distinct owners so
    /// per-group coverage is literally countable. A test-held txn locks the first 10 rows (SKIP LOCKED
    /// order) BEFORE the first pump: the pump must return WITHOUT blocking and its claimed set must not
    /// intersect the locked set (checked via <c>attempts</c>, which only a claim bumps). The whole drain
    /// runs in a HARD-BOUNDED loop -- exceeding the bound is a clean assertion FAILURE, not a hang.
    /// </summary>
    [Fact]
    public async Task H9_two_instances_drain_all_rows_without_blocking_or_overlap()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        const int rowCount = 25;
        var owners = Enumerable.Range(0, rowCount).Select(_ => Guid.NewGuid()).ToArray();

        await using (var app = new SyncTestApp(connectionString))
        {
            foreach (var owner in owners)
            {
                await app.SyncAsync(owner, Wire.PushNoPull(owner,
                    Wire.TaskField(Guid.CreateVersion7(), owner, Guid.NewGuid(), owner, "title", "x")));
            }
        }

        (await Db.ScalarAsync<long>(connectionString, "SELECT count(*) FROM outbox_messages WHERE signaled_at IS NULL"))
            .ShouldBe((long)rowCount);

        var publisher = new RecordingSignalPublisher();
        var options = new OutboxDispatcherOptions { BatchSize = 10 };
        var d1 = DispatcherHarness.Create(connectionString, publisher, options, TimeProvider.System);
        var d2 = DispatcherHarness.Create(connectionString, publisher, options, TimeProvider.System);

        // Determinist overlap (no sleep-based timing): our OWN txn locks the first 10 eligible rows.
        await using var lockConn = await Db.OpenAsync(connectionString);
        var lockTxn = await lockConn.BeginTransactionAsync();
        var lockedIds = await LockTenRowsAsync(lockConn, lockTxn);
        lockedIds.Count.ShouldBe(10);

        var claimed1 = await d1.PumpOnceAsync(CancellationToken.None); // must return WITHOUT blocking
        claimed1.ShouldBe(10); // 15 unlocked rows remain, batch=10 -> exactly 10 claimed

        // kesişim ∅: a claim bumps `attempts`; the locked rows must be untouched.
        (await Db.ScalarAsync<long>(connectionString,
            "SELECT count(*) FROM outbox_messages WHERE id = ANY(@ids) AND attempts > 0", ("ids", lockedIds.ToArray())))
            .ShouldBe(0L);

        await lockTxn.RollbackAsync(); // release the 10 held rows back into the eligible pool

        var total = claimed1;
        var useD1 = false;
        var maxPumpCalls = (int)Math.Ceiling(rowCount / (double)options.BatchSize) + 3; // bound, not a timeout
        for (var i = 1; i < maxPumpCalls && total < rowCount; i++)
        {
            total += await (useD1 ? d1 : d2).PumpOnceAsync(CancellationToken.None);
            useD1 = !useD1;
        }

        total.ShouldBe(rowCount); // fails cleanly (not a hang) if the drain did not converge
        (await d1.PumpOnceAsync(CancellationToken.None)).ShouldBe(0); // fully drained
        (await Db.ScalarAsync<long>(connectionString, "SELECT count(*) FROM outbox_messages WHERE signaled_at IS NULL"))
            .ShouldBe(0L);

        var publishedGroups = publisher.Published.Select(p => p.Group).ToHashSet(StringComparer.Ordinal);
        var expectedGroups = owners.Select(o => $"user:{o}").ToHashSet(StringComparer.Ordinal);
        publishedGroups.SetEquals(expectedGroups).ShouldBeTrue(
            $"published groups did not match expected owners.\nMissing: {string.Join(", ", expectedGroups.Except(publishedGroups))}\nExtra: {string.Join(", ", publishedGroups.Except(expectedGroups))}");
    }

    /// <summary>
    /// H9's atlanan yarısı (D8-i-b): a SEPARATE fixture (single owner -- the 25-owner fixture above makes
    /// per-actor pull coverage unmeasurable, since <c>PullIncrementalAsync</c> filters by <c>owner_id</c>).
    /// Two dispatchers pump concurrently with /v1/sync activity; the incremental cursor loop must still
    /// return every change, in order, exactly once -- dispatcher churn (attempts/available_at/signaled_at)
    /// never touches the columns pull correctness depends on (commit_xid/server_seq).
    /// </summary>
    [Fact]
    public async Task Cursor_correctness_is_unaffected_by_concurrent_dispatch_single_owner()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        const int rowCount = 25;
        var owner = Guid.NewGuid();

        await using (var app = new SyncTestApp(connectionString))
        {
            for (var i = 0; i < rowCount; i++)
            {
                await app.SyncAsync(owner, Wire.PushNoPull(owner,
                    Wire.TaskField(Guid.CreateVersion7(), owner, Guid.NewGuid(), owner, "title", $"v{i}")));
            }
        }

        var publisher = new RecordingSignalPublisher();
        var options = new OutboxDispatcherOptions { BatchSize = 10 };
        var d1 = DispatcherHarness.Create(connectionString, publisher, options, TimeProvider.System);
        var d2 = DispatcherHarness.Create(connectionString, publisher, options, TimeProvider.System);

        var pumped = 0;
        for (var i = 0; i < 10 && pumped < rowCount; i++)
        {
            pumped += await d1.PumpOnceAsync(CancellationToken.None);
            pumped += await d2.PumpOnceAsync(CancellationToken.None);
        }

        await using var puller = new SyncTestApp(connectionString);
        var since = new Momentum.Domain.Sync.SyncCursor(0, 0);
        var seen = new List<string>();
        for (var i = 0; i < rowCount + 1; i++)
        {
            var page = await puller.PullAsync(owner, since);
            seen.AddRange(page.Changes.Select(c => c.PayloadJson));
            since = page.NextCursor;
            if (!page.HasMore)
            {
                break;
            }
        }

        seen.Count.ShouldBe(rowCount);
        for (var i = 0; i < rowCount; i++)
        {
            seen[i].ShouldContain($"\"v{i}\"");
        }
    }

    /// <summary>D8-ii (K2-G1): the DTO shape ITSELF has no room for entity content -- asserted on the
    /// serialized property-name set, which is what actually makes mutant-5 bite (adding ANY property
    /// changes the set regardless of what value it carries).</summary>
    [Fact]
    public async Task Dispatched_signal_carries_only_group_and_cursor_hint()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        var owner = Guid.NewGuid();
        const string secretMarker = "SUPER-SECRET-TITLE-CONTENT";

        await using (var app = new SyncTestApp(connectionString))
        {
            await app.SyncAsync(owner, Wire.PushNoPull(owner,
                Wire.TaskField(Guid.CreateVersion7(), owner, Guid.NewGuid(), owner, "title", secretMarker)));
        }

        var publisher = new RecordingSignalPublisher();
        var dispatcher = DispatcherHarness.Create(connectionString, publisher, new OutboxDispatcherOptions { BatchSize = 10 }, TimeProvider.System);
        (await dispatcher.PumpOnceAsync(CancellationToken.None)).ShouldBe(1);

        var envelope = publisher.Published.ShouldHaveSingleItem();
        var json = JsonSerializer.Serialize(envelope, WireMapping.Json);

        var lowered = json.ToLowerInvariant();
        lowered.ShouldNotContain(secretMarker.ToLowerInvariant());
        lowered.ShouldNotContain("payload");
        lowered.ShouldNotContain("entitytype");
        lowered.ShouldNotContain("aggregateid");
        lowered.ShouldNotContain("fields");

        using var document = JsonDocument.Parse(json);
        var propertyNames = document.RootElement.EnumerateObject().Select(p => p.Name.ToLowerInvariant()).ToHashSet();
        propertyNames.SetEquals(new[] { "group", "cursorhint" }).ShouldBeTrue(
            $"unexpected shape: [{string.Join(", ", propertyNames)}]");
    }

    /// <summary>D8-iii (D2-a): a targeted publish failure leaves the row OPEN (not closed); the NEXT pump,
    /// after the lease/backoff window elapses (FakeTimeProvider -- deterministic, no real sleep), reclaims
    /// and successfully publishes it.</summary>
    [Fact]
    public async Task Publish_failure_reopens_the_row_and_the_next_pump_retries_it()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        var owner = Guid.NewGuid();

        // The row's available_at is written by the handler from TimeProvider (D6-1: no SQL now()) -- the
        // SAME fake clock must seed the write AND the dispatcher, or the row is born "in the future"
        // relative to the dispatcher's fake now and the very first claim silently returns 0. The fake
        // clock is anchored at Wire.BaseWall (NOT an arbitrary epoch like DateTimeOffset.UnixEpoch): the
        // op's HLC wall-clock is Wire.BaseWall too, and an "as-of-now" that is ~53 years away from the
        // op's HLC trips ADR K2-A4's absurd-forward-skew rejection (422) -- unrelated to anything this
        // test is trying to measure, and it silently produces zero outbox rows.
        var time = new FakeTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(Wire.BaseWall));
        await using (var app = new SyncTestApp(connectionString, time))
        {
            await app.SyncAsync(owner, Wire.PushNoPull(owner,
                Wire.TaskField(Guid.CreateVersion7(), owner, Guid.NewGuid(), owner, "title", "x")));
        }

        var publisher = new RecordingSignalPublisher();
        publisher.FailGroupsOnce.Add($"user:{owner}");
        var dispatcher = DispatcherHarness.Create(connectionString, publisher, new OutboxDispatcherOptions { BatchSize = 10 }, time);

        (await dispatcher.PumpOnceAsync(CancellationToken.None)).ShouldBe(1); // claimed, publish for its one group failed
        (await Db.ScalarAsync<bool>(connectionString, "SELECT signaled_at IS NULL FROM outbox_messages WHERE owner_id = @o", ("o", owner)))
            .ShouldBeTrue(); // NOT closed (D1 YB-1: any failed group keeps the row open)
        publisher.Published.ShouldBeEmpty();

        time.Advance(TimeSpan.FromSeconds(3)); // backoff(attempts=1) = 2s < 3s

        (await dispatcher.PumpOnceAsync(CancellationToken.None)).ShouldBe(1); // re-claimed after backoff
        (await Db.ScalarAsync<bool>(connectionString, "SELECT signaled_at IS NULL FROM outbox_messages WHERE owner_id = @o", ("o", owner)))
            .ShouldBeFalse(); // now closed
        publisher.Published.Count.ShouldBe(1);
    }

    /// <summary>D8-viii (K2-D3's bildirim yarısı): 2b1 already guarantees exactly one outbox row for a
    /// duplicate push, so the dispatcher publishes exactly one signal.</summary>
    [Fact]
    public async Task Duplicate_push_dispatches_exactly_one_signal()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        var owner = Guid.NewGuid();
        var op = Wire.TaskField(Guid.CreateVersion7(), owner, Guid.NewGuid(), owner, "title", "x");

        await using (var app = new SyncTestApp(connectionString))
        {
            await app.SyncAsync(owner, Wire.PushOnly(owner, op));
            await app.SyncAsync(owner, Wire.PushOnly(owner, op)); // duplicate
        }

        var publisher = new RecordingSignalPublisher();
        var dispatcher = DispatcherHarness.Create(connectionString, publisher, new OutboxDispatcherOptions { BatchSize = 10 }, TimeProvider.System);
        (await dispatcher.PumpOnceAsync(CancellationToken.None)).ShouldBe(1);

        publisher.Published.ShouldHaveSingleItem();
    }

    /// <summary>D8-ix (D3): 3 rows in ONE pump for the SAME group collapse into one signal carrying the
    /// LARGEST cursor. mutant-10 (max -> min) fails this.</summary>
    [Fact]
    public async Task Pump_internal_reduction_publishes_the_largest_cursor_for_a_shared_group()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        var owner = Guid.NewGuid();

        await using (var app = new SyncTestApp(connectionString))
        {
            for (var i = 0; i < 3; i++)
            {
                await app.SyncAsync(owner, Wire.PushNoPull(owner,
                    Wire.TaskField(Guid.CreateVersion7(), owner, Guid.NewGuid(), owner, "title", $"v{i}")));
            }
        }

        var expectedMaxSeq = await Db.ScalarAsync<long>(connectionString,
            "SELECT server_seq FROM outbox_messages WHERE owner_id = @o ORDER BY commit_xid DESC, server_seq DESC LIMIT 1", ("o", owner));

        var publisher = new RecordingSignalPublisher();
        var dispatcher = DispatcherHarness.Create(connectionString, publisher, new OutboxDispatcherOptions { BatchSize = 10 }, TimeProvider.System);
        (await dispatcher.PumpOnceAsync(CancellationToken.None)).ShouldBe(3);

        var envelope = publisher.Published.ShouldHaveSingleItem();
        envelope.Group.ShouldBe($"user:{owner}");
        envelope.CursorHint.Seq.ShouldBe(expectedMaxSeq);
    }

    /// <summary>
    /// D7/C7 signal leg (ADR K2-C7 -- signal leg ONLY, no pull leg in this slice). IS-EMRI-o86-C D-B2
    /// (SECOND and LAST exception to the "don't touch a test that catches a real regression" rule):
    /// the ORIGINAL version of this test used ONE actor (<c>client</c>) as the owner of BOTH projects,
    /// so the "old scope" and "new scope" audiences were the SAME person -- after §A removed the
    /// <c>scope:</c> group (D-A1 PAZARLIKSIZ), every path collapsed onto the single <c>user:{client}</c>
    /// envelope (D3's <c>maxByGroup</c> reduction) and the test could no longer distinguish "old_scope
    /// column present" from "old_scope column removed" (mutant-6 would go undetected either way) --
    /// it was testing the GROUP-NAME STRING, never the AUDIENCE. Rewritten with a THIRD identity
    /// (<c>v</c>, a real <c>project_access</c> member of A ONLY, added via the SAME wire op shape
    /// IS-EMRI-o86-B §A's client <c>uyeEkle</c> produces) so the old-scope audience and the new-scope
    /// audience are genuinely different people again. An entity moving from scope A to scope B must
    /// still signal BOTH the A-audience (the "it left" signal, reachable only via <c>OldScopeId</c>)
    /// and the B-audience (the "it arrived" signal, via <c>ScopeId</c>).
    /// </summary>
    [Fact]
    public async Task Scope_transition_dispatches_to_both_old_and_new_scope_groups()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        var client = Guid.NewGuid(); // Task actor + sahibi HEM projectA HEM projectB'nin.
        var v = Guid.NewGuid();      // projectA'nin UYESI, projectB'de DEGIL -- "eski kitle" (client'tan FARKLI kimlik).
        var x = Guid.NewGuid();      // hicbir projede degil -- NEGATIF ES.
        var entity = Guid.NewGuid();
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        // IS-EMRI-o86-A §E: bir Task'in HEDEF scope'u sahip/uye gerektirir (varlik yeni DEGILSE) --
        // client'in ikisinin de GERCEK sahibi olmasi icin once Project'ler yaratilir, SONRA V
        // projectA'ya GERCEK bir wire op ile UYE eklenir (IS-EMRI-o86-B §A `uyeEkle`nin AYNI deseni:
        // kanal YALNIZ `sets.members`) -- hepsi kendi dispatch turunda drenilir, asagidaki "tek satir"
        // sayimlarini KIRLETMEZ.
        await using (var app = new SyncTestApp(connectionString))
        {
            await app.SyncAsync(client, Wire.PushNoPull(client, Wire.Op(Guid.CreateVersion7(), client, projectA, client, 1,
                fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("A", Wire.Hlc(client, 1)) },
                entityType: "Project")));
            await app.SyncAsync(client, Wire.PushNoPull(client, Wire.Op(Guid.CreateVersion7(), client, projectB, client, 1,
                fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("B", Wire.Hlc(client, 1)) },
                entityType: "Project")));
            await app.SyncAsync(client, Wire.PushNoPull(client, Wire.Op(Guid.CreateVersion7(), client, projectA, client, 2,
                sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
                {
                    ["members"] = new([new WireSetAdd(v.ToString(), Guid.NewGuid(), Wire.Hlc(client, 2))], null),
                },
                entityType: "Project")));
        }

        var priorPublisher = new RecordingSignalPublisher();
        var priorDispatcher = DispatcherHarness.Create(connectionString, priorPublisher, new OutboxDispatcherOptions { BatchSize = 10 }, TimeProvider.System);
        (await priorDispatcher.PumpOnceAsync(CancellationToken.None)).ShouldBe(3);

        await using (var app = new SyncTestApp(connectionString))
        {
            await app.SyncAsync(client, Wire.PushNoPull(client,
                Wire.TaskField(Guid.CreateVersion7(), client, entity, client, "projectId", projectA.ToString(), counter: 1)));
        }

        // Drain + close row1 (the entity's CREATION into scope A) BEFORE the transition. Otherwise
        // the new-scope publish from row1's OWN creation would already put V/client's envelopes in the
        // observed set, and removing ONLY the old-scope yield (mutant-6) would go unnoticed.
        var setupPublisher = new RecordingSignalPublisher();
        var setupDispatcher = DispatcherHarness.Create(connectionString, setupPublisher, new OutboxDispatcherOptions { BatchSize = 10 }, TimeProvider.System);
        (await setupDispatcher.PumpOnceAsync(CancellationToken.None)).ShouldBe(1);

        await using (var app = new SyncTestApp(connectionString))
        {
            await app.SyncAsync(client, Wire.PushNoPull(client,
                Wire.TaskField(Guid.CreateVersion7(), client, entity, client, "projectId", projectB.ToString(), counter: 2)));
        }

        var publisher = new RecordingSignalPublisher();
        var dispatcher = DispatcherHarness.Create(connectionString, publisher, new OutboxDispatcherOptions { BatchSize = 10 }, TimeProvider.System);
        (await dispatcher.PumpOnceAsync(CancellationToken.None)).ShouldBe(1);

        var groups = publisher.Published.Select(p => p.Group).ToHashSet(StringComparer.Ordinal);
        groups.ShouldContain($"user:{v}",
            "eski kitle (V, SADECE projectA uyesi): gecisin 'gitti' bilgisini almali -- YALNIZ OldScopeId kolundan ulasilabilir");
        groups.ShouldContain($"user:{client}",
            "yeni kitle (client, projectB'nin sahibi): gecisin 'geldi' bilgisini almali");
        groups.ShouldNotContain($"user:{x}",
            "negatif es: hicbir projede olmayan X sinyal ALMAMALI");
    }

    private static async Task<List<Guid>> LockTenRowsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        var ids = new List<Guid>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT id FROM outbox_messages WHERE signaled_at IS NULL ORDER BY commit_xid, server_seq LIMIT 10 FOR UPDATE SKIP LOCKED";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }
}

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Momentum.Api.Realtime;
using Momentum.Application.Abstractions;
using Momentum.Application.Abstractions.Sync;
using Momentum.Application.Features.Sync;
using Momentum.Domain.Sync;
using Momentum.Infrastructure;
using Momentum.Infrastructure.Sync;
using Shouldly;
using Xunit;

namespace Momentum.Persistence.Tests;

/// <summary>slice-2b2 D8-v (G2 bağlantıda yeniden-hesap) + D8-vi (G3 gerçek HubConnection).</summary>
[Collection(PostgresCollection.Name)]
public sealed class RealtimeMembershipTests(PostgresFixture fixture)
{
    /// <summary>
    /// D8-v: <c>SyncHub</c> is instantiated directly (no mock library -- Hub exposes <c>Groups</c>/
    /// <c>Context</c> as public settable properties for exactly this). IS-EMRI-o86-A §D3: membership is
    /// now `project_members`, not outbox -- fixture arranges rows DIRECTLY. KB-C: V's membership in a
    /// DIFFERENT scope T is REQUIRED in the fixture -- without a foreign-scope row in the table,
    /// mutant-7 (dropping the <c>user_id</c> filter) is unobservable, since the table would otherwise
    /// hold only U's own row. KB-B: withdrawing U's membership in S is an explicit DELETE (a test-arrange
    /// action, not a dispatcher behavior).
    /// </summary>
    [Fact]
    public async Task Hub_recomputes_group_membership_on_each_connect()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        var u = Guid.NewGuid();
        var v = Guid.NewGuid();
        var s = Guid.NewGuid();
        var t = Guid.NewGuid();

        await Db.ExecuteAsync(connectionString, "INSERT INTO project_members (project_id, user_id) VALUES (@s, @u)", ("s", s), ("u", u));
        await Db.ExecuteAsync(connectionString, "INSERT INTO project_members (project_id, user_id) VALUES (@t, @v)", ("t", t), ("v", v));

        var services = new ServiceCollection();
        services.AddSyncInfrastructure(connectionString);
        await using var provider = services.BuildServiceProvider();

        // --- Phase 1: U connects -> exactly {user:U, scope:S} (T must NOT leak in). ---
        await using (var scope1 = provider.CreateAsyncScope())
        {
            var membership = scope1.ServiceProvider.GetRequiredService<IScopeMembershipSource>();
            var groups = new RecordingGroupManager();
            var context = new FakeHubCallerContext(Guid.NewGuid().ToString());
            var hub = new SyncHub(new FakeCurrentUser(u), membership) { Groups = groups, Context = context };

            await hub.OnConnectedAsync();

            var actual = groups.GroupsFor(context.ConnectionId).ToHashSet(StringComparer.Ordinal);
            actual.SetEquals(new[] { $"user:{u}", $"scope:{s}" }).ShouldBeTrue(
                $"unexpected group set: [{string.Join(", ", actual)}]");
        }

        // --- Phase 2: U's membership in S withdrawn (test-arrange DELETE, KB-B) -> reconnect recomputes. ---
        await Db.ExecuteAsync(connectionString, "DELETE FROM project_members WHERE user_id = @u AND project_id = @s", ("u", u), ("s", s));

        await using (var scope2 = provider.CreateAsyncScope())
        {
            var membership = scope2.ServiceProvider.GetRequiredService<IScopeMembershipSource>();
            var groups = new RecordingGroupManager();
            var context = new FakeHubCallerContext(Guid.NewGuid().ToString());
            var hub = new SyncHub(new FakeCurrentUser(u), membership) { Groups = groups, Context = context };

            await hub.OnConnectedAsync();

            var actual = groups.GroupsFor(context.ConnectionId).ToHashSet(StringComparer.Ordinal);
            actual.SetEquals(new[] { $"user:{u}" }).ShouldBeTrue($"unexpected group set: [{string.Join(", ", actual)}]");
        }
    }

    /// <summary>
    /// D8-vi (K2-G3): a REAL <see cref="HubConnection"/> against a real SignalR pipeline (TestServer +
    /// WebSockets, D10-b pin). Disconnect -> N ops processed + dispatcher pumped -> reconnect must NOT
    /// replay anything (0 <c>Changed</c> calls -- mutant-11 adds one), while <c>/v1/sync</c> (the real
    /// cursor, called directly here, bypassing HTTP -- only the hub leg needs the live pipeline) returns
    /// every change. Proving "nothing arrives" needs SOME bounded wait; a 2s bound is generous for CI and
    /// is not the kind of hazard-timing sleep the D9 orchestration ban targets (that rule polices forcing
    /// DB transaction interleaving with sleeps, not detecting the absence of an async event).
    /// </summary>
    [Fact]
    public async Task Reconnect_does_not_replay_while_sync_returns_every_change()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        var user = Guid.NewGuid();

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Momentum", connectionString);
            builder.ConfigureTestServices(services =>
                services.AddScoped<ICurrentUser>(_ => new FakeCurrentUser(user)));
        });
        var server = factory.Server;

        HubConnection BuildConnection() => new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/sync", options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.WebSocketFactory = async (context, ct) => await server.CreateWebSocketClient().ConnectAsync(context.Uri, ct);
            })
            .Build();

        await using (var first = BuildConnection())
        {
            await first.StartAsync();
            await first.StopAsync();
        }

        const int changeCount = 3;
        await using (var app = new SyncTestApp(connectionString))
        {
            for (var i = 0; i < changeCount; i++)
            {
                await app.SyncAsync(user, Wire.PushNoPull(user,
                    Wire.TaskField(Guid.CreateVersion7(), user, Guid.NewGuid(), user, "title", $"v{i}")));
            }
        }

        var publisher = new RecordingSignalPublisher();
        var dispatcher = DispatcherHarness.Create(connectionString, publisher, new OutboxDispatcherOptions { BatchSize = 10 }, TimeProvider.System);
        for (var i = 0; i < 3; i++)
        {
            if (await dispatcher.PumpOnceAsync(CancellationToken.None) == 0)
            {
                break;
            }
        }

        var received = new List<object>();
        var receivedOne = new TaskCompletionSource();
        await using (var reconnected = BuildConnection())
        {
            reconnected.On<object>("Changed", payload =>
            {
                received.Add(payload);
                receivedOne.TrySetResult();
            });

            await reconnected.StartAsync();
            var winner = await Task.WhenAny(receivedOne.Task, Task.Delay(TimeSpan.FromSeconds(2)));
            winner.ShouldNotBe(receivedOne.Task); // nothing arrived within the bounded wait -- no replay
            await reconnected.StopAsync();
        }

        received.ShouldBeEmpty();

        await using var puller = new SyncTestApp(connectionString);
        var page = await puller.PullAsync(user, new SyncCursor(0, 0));
        page.Changes.Count.ShouldBe(changeCount); // the real cursor, unaffected by the hub, returns everything
    }

    /// <summary>
    /// IS-EMRI-o86-A2 §E H2: SAHIP A, kendi projesine baglaninca `scope:{P}` grubuna KATILIR --
    /// sahip `project_members`e YAZILMAZ (§C3), ama `project_access` GORUNUMU onu `projects.owner_id`
    /// uzerinden zaten kapsar.
    /// </summary>
    [Fact]
    public async Task Owner_joins_own_scope_group_on_connect_H2()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        var owner = Guid.NewGuid();
        var project = Guid.NewGuid();

        await using (var app = new SyncTestApp(connectionString))
        {
            await app.SyncAsync(owner, Wire.PushNoPull(owner, Wire.Op(Guid.CreateVersion7(), owner, project, owner, 1,
                fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(owner, 1)) },
                entityType: "Project")));
        }

        var services = new ServiceCollection();
        services.AddSyncInfrastructure(connectionString);
        await using var provider = services.BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var membership = scope.ServiceProvider.GetRequiredService<IScopeMembershipSource>();
        var groups = new RecordingGroupManager();
        var context = new FakeHubCallerContext(Guid.NewGuid().ToString());
        var hub = new SyncHub(new FakeCurrentUser(owner), membership) { Groups = groups, Context = context };

        await hub.OnConnectedAsync();

        var actual = groups.GroupsFor(context.ConnectionId).ToHashSet(StringComparer.Ordinal);
        actual.ShouldContain($"scope:{project}", "H2: sahip project_members'e YAZILMAZ ama project_access uzerinden scope grubuna KATILMALI");
    }
}

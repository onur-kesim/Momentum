using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
    /// IS-EMRI-o86-C §B D-B1 (md.6 "olu kod borctur"): `scope:` gruplarina baglanti aninda katilim
    /// KALKTI -- <c>SyncHub</c> artik `IScopeMembershipSource`'a HIC ihtiyac duymuyor (constructor'dan
    /// da kalkti). Bu test, ONCEKI sozlesmenin (bagli oldugu <c>scope:</c> grubunun uyelik degisince
    /// yeniden hesaplanmasi) YERINE, YENI sozlesmeyi kanitlar: her baglanti, uyelikten TAMAMEN bagimsiz,
    /// YALNIZ kendi `user:{self}` grubuna katilir -- baska hicbir grup yok.
    /// D8-v deseni (mock kutuphanesi GEREKMEZ) korunur: <c>SyncHub</c> dogrudan instantiate edilir.
    /// </summary>
    [Fact]
    public async Task Hub_joins_only_the_users_own_group_on_connect()
    {
        var u = Guid.NewGuid();
        var groups = new RecordingGroupManager();
        var context = new FakeHubCallerContext(Guid.NewGuid().ToString());
        var hub = new SyncHub(new FakeCurrentUser(u)) { Groups = groups, Context = context };

        await hub.OnConnectedAsync();

        var actual = groups.GroupsFor(context.ConnectionId).ToHashSet(StringComparer.Ordinal);
        actual.SetEquals(new[] { $"user:{u}" }).ShouldBeTrue($"unexpected group set: [{string.Join(", ", actual)}]");
    }

    /// <summary>
    /// IS-EMRI-o86-C §C H9 (sinir 38'in altinci isirigi kapanisi -- PAZARLIKSIZ): B, P projesine
    /// DAVETTEN ONCE "baglanir" (D8-v: dogrudan Hub instantiate, sadece kendi user: grubuna katilir --
    /// yukaridaki test zaten bunu kanitliyor). SONRA A, B'yi P'ye ekler ve P'ye bir gorev op'u yazar.
    /// Zayiflatma: gercek bir WebSocket/HubConnection GEREKMEZ -- <see cref="RecordingSignalPublisher"/>
    /// zaten HANGI GRUBA sinyal gittigini kaydeder, ve B'nin kendi grubuna (user:B) HER ZAMAN katildigi
    /// (yukaridaki test) AYRICA kanitlanmis oldugundan, "user:B grubuna sinyal yayinlandi" = "B sinyali
    /// ALIR" (Reconnect_does_not_replay testi zaten ayni gruba GERCEK HubConnection'in ulastigini
    /// kanitliyor -- iki test farkli katmanlari kapatir).
    /// POZITIF KONTROLUN ESI (zorunlu, madde eksikse H9 bos kumeyle gecer): projede uye OLMAYAN C
    /// icin "user:C" HICBIR ZAMAN yayinlanmaz.
    /// Mutant: `GetMembersAsync` cagrisini connect-time uyelikle (B'nin baglandigi anki -- HENUZ uye
    /// degil) degistir ⇒ "user:B" hic yayinlanmaz ⇒ test KIRMIZI olmali.
    /// </summary>
    [Fact]
    public async Task Member_added_after_connect_receives_signal_without_reconnect_H9()
    {
        var connectionString = await TestDatabase.CreateAsync(fixture);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid(); // uye DEGIL, hic olmayacak -- pozitif kontrolun esi.
        var project = Guid.NewGuid();

        await using (var app = new SyncTestApp(connectionString))
        {
            // A projeyi olusturur -- B henuz UYE DEGIL (B'nin "baglantisi" bu ANDAN SONRA kurulmus olur).
            await app.SyncAsync(a, Wire.PushNoPull(a, Wire.Op(Guid.CreateVersion7(), a, project, a, 1,
                fields: new Dictionary<string, WireFieldWrite>(StringComparer.Ordinal) { ["name"] = new("P", Wire.Hlc(a, 1)) },
                entityType: "Project")));

            // A, SONRA B'yi P'ye ekler -- `uyeEkle`nin (client, IS-EMRI-o86-B §A) backend esdegeri:
            // ayni desen, kanal YALNIZ `sets.members`.
            await app.SyncAsync(a, Wire.PushNoPull(a, Wire.Op(Guid.CreateVersion7(), a, project, a, 2,
                sets: new Dictionary<string, WireSetDelta>(StringComparer.Ordinal)
                {
                    ["members"] = new([new WireSetAdd(b.ToString(), Guid.NewGuid(), Wire.Hlc(a, 2))], null),
                },
                entityType: "Project")));

            // P'ye bir gorev op'u yazilir (§D adim 6/7'nin sunucu karsiligi).
            await app.SyncAsync(a, Wire.PushNoPull(a, Wire.TaskField(Guid.CreateVersion7(), a, Guid.NewGuid(), a, "title", "gorev-1")));
        }

        var publisher = new RecordingSignalPublisher();
        var services = new ServiceCollection();
        services.AddSyncInfrastructure(connectionString); // IScopeMembershipSource GERCEK ScopeMembershipSource'tan gelir.
        services.AddScoped(_ => new OutboxClaimStore(connectionString));
        services.AddSingleton<ISignalPublisher>(publisher);
        await using var provider = services.BuildServiceProvider();
        var dispatcher = new OutboxDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
            new OutboxDispatcherOptions { BatchSize = 10 }, NullLogger<OutboxDispatcher>.Instance);

        for (var i = 0; i < 5; i++)
        {
            if (await dispatcher.PumpOnceAsync(CancellationToken.None) == 0)
            {
                break;
            }
        }

        var publishedGroups = publisher.Published.Select(p => p.Group).ToHashSet(StringComparer.Ordinal);
        publishedGroups.ShouldContain($"user:{b}",
            "H9: baglantidan SONRA davet edilen uye, YENIDEN BAGLANMADAN sinyal almali");
        publishedGroups.ShouldNotContain($"user:{c}",
            "pozitif kontrolun esi: projede uye OLMAYAN C, ayni op icin sinyal ALMAMALI");
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

    // IS-EMRI-o86-C §B D-B2 (gerekce, tek cumle): "Owner_joins_own_scope_group_on_connect_H2" (o86-A2
    // H2) SILINDI -- sinadigi invaryant ("sahip baglanti aninda scope:{P} grubuna katilir") kilitle
    // (K-o88/6, D-A1) kalkti; sahibin GetMembersAsync sonucuna project_access uzerinden dahil olmasi
    // artik H9'un (yukarida) dolayli sonucu, ve sahip zaten kendi user: grubundan HER ZAMAN Gelen
    // Kutusu sinyalini alir (OutboxDispatcher.GroupsFor'un owner_id kolu D-A3'te DEGISMEDI).
}

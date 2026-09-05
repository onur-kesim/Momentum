using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Momentum.Application.Abstractions.Sync;
using Momentum.Application.Features.Sync;
using Momentum.Infrastructure;
using Momentum.Infrastructure.Sync;

namespace Momentum.Persistence.Tests;

/// <summary>
/// A fake <see cref="HubCallerContext"/> for direct <c>Hub</c> instantiation (GOREV slice-2b2 D8-v pin:
/// "mock kütüphanesi GEREKMEZ" -- SignalR's Hub base class exposes <c>Context</c>/<c>Groups</c> as
/// public settable properties precisely to support this style of test).
/// </summary>
public sealed class FakeHubCallerContext(string connectionId) : HubCallerContext
{
    public bool Aborted { get; private set; }

    public override string ConnectionId { get; } = connectionId;

    public override string? UserIdentifier => null;

    public override ClaimsPrincipal? User => null;

    public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

    public override IFeatureCollection Features { get; } = new FeatureCollection();

    public override CancellationToken ConnectionAborted => CancellationToken.None;

    public override void Abort() => Aborted = true;
}

/// <summary>Records every <c>AddToGroupAsync</c>/<c>RemoveFromGroupAsync</c> call (GOREV slice-2b2 D8-v).</summary>
public sealed class RecordingGroupManager : IGroupManager
{
    private readonly List<(string ConnectionId, string GroupName)> _added = [];

    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        _added.Add((connectionId, groupName));
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        _added.RemoveAll(x => x.ConnectionId == connectionId && x.GroupName == groupName);
        return Task.CompletedTask;
    }

    public IReadOnlyCollection<string> GroupsFor(string connectionId) =>
        _added.Where(x => x.ConnectionId == connectionId).Select(x => x.GroupName).ToList();
}

/// <summary>
/// A deterministic <see cref="ISignalPublisher"/> fake (GOREV slice-2b2 D2 "kaydeden sahte publisher").
/// <see cref="FailGroupsOnce"/> fails a targeted group exactly once (D8-iii) without a real transport.
/// </summary>
public sealed class RecordingSignalPublisher : ISignalPublisher
{
    private readonly object _gate = new();
    private readonly List<SignalEnvelope> _published = [];

    public HashSet<string> FailGroupsOnce { get; } = new(StringComparer.Ordinal);

    public IReadOnlyList<SignalEnvelope> Published
    {
        get { lock (_gate) { return _published.ToList(); } }
    }

    public Task<IReadOnlyCollection<PublishFailure>> PublishAsync(
        IReadOnlyCollection<SignalEnvelope> signals, CancellationToken cancellationToken)
    {
        var failures = new List<PublishFailure>();
        lock (_gate)
        {
            foreach (var signal in signals)
            {
                if (FailGroupsOnce.Remove(signal.Group))
                {
                    failures.Add(new PublishFailure(signal.Group));
                }
                else
                {
                    _published.Add(signal);
                }
            }
        }

        return Task.FromResult<IReadOnlyCollection<PublishFailure>>(failures);
    }
}

/// <summary>
/// Builds an <see cref="OutboxDispatcher"/> with its OWN <see cref="IServiceScopeFactory"/> (GOREV
/// slice-2b2 D8-i: "her biri kendi scope'u/NpgsqlConnection'ı ile"). <see cref="OutboxClaimStore"/> opens
/// a fresh physical connection per call regardless, but a separate root provider per dispatcher instance
/// makes the independence explicit and matches the spec's framing literally.
/// </summary>
public static class DispatcherHarness
{
    public static OutboxDispatcher Create(
        string connectionString, ISignalPublisher publisher, OutboxDispatcherOptions options, TimeProvider timeProvider)
    {
        var services = new ServiceCollection();
        // IS-EMRI-o86-C K-o88/8/9: GERCEK production DI grafigi -- AddSyncInfrastructure zaten
        // OutboxClaimStore + IScopeMembershipSource (ve SyncDbContext'i, ScopeMembershipSource'in
        // bagimliligi) AYNI connectionString ile kaydeder (K-o88/9 sart 3: TEK kaynak, ikinci bir
        // literal/baglanti dizesi YOK). OutboxDispatcher'in kendisi burada AddHostedService
        // EDILMEZ (Program.cs'de ayrica kayitli, K-o88/9 sart 2 ile OLCULDU) -- bu harness onu hep
        // manuel `PumpOnceAsync` ile tek tek kosturur, iki kez calismasi soz konusu degil.
        services.AddSyncInfrastructure(connectionString);
        services.AddSingleton(publisher); // TService inferred as ISignalPublisher (the parameter's static type)
        var provider = services.BuildServiceProvider();
        return new OutboxDispatcher(provider.GetRequiredService<IServiceScopeFactory>(), timeProvider, options, NullLogger<OutboxDispatcher>.Instance);
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Momentum.Application.Abstractions.Sync;
using Momentum.Application.Features.Sync;

namespace Momentum.Infrastructure.Sync;

/// <summary>
/// Signal publisher (ADR 0002 K2-F2, GOREV slice-2b2 D2). A polling <see cref="BackgroundService"/>:
/// claim a lease-protected batch (<see cref="OutboxClaimStore"/>) -> resolve EACH scope's CURRENT
/// members (<see cref="IScopeMembershipSource"/>, IS-EMRI-o86-C §A -- publish-time, not connect-time)
/// -> publish payload-less signals (<see cref="ISignalPublisher"/>, resolved OUTSIDE any op
/// transaction, D2-a) -> close only the rows whose groups ALL succeeded. Order-independent,
/// multi-instance safe (SKIP LOCKED); crash between claim and close just lets the lease expire and the
/// row resurface (real at-least-once).
/// <para>
/// Both <see cref="OutboxClaimStore"/> and <see cref="ISignalPublisher"/> are DI-scoped ports -- direct
/// singleton injection is forbidden, so a fresh <see cref="IServiceScope"/> is opened on EVERY pump
/// (D2-e). <see cref="PumpOnceAsync"/> and <see cref="ExecuteAsync"/> have DELIBERATELY different
/// exception contracts (D2-f): the former LEAKS (mutant-8's only kill path), the latter NEVER does
/// (otherwise <c>BackgroundServiceExceptionBehavior.StopHost</c> kills the whole DB-less host).
/// </para>
/// </summary>
public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly OutboxDispatcherOptions _options;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        OutboxDispatcherOptions options,
        ILogger<OutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// One pump: returns the number of rows TALEP EDİLEN (claimed in Txn-1) this turn -- NOT the number of
    /// signals published. 0 means nothing was eligible. Exceptions from the claim (e.g. a lock_timeout
    /// abort), the membership lookup or the publish step are NOT caught here -- see the type doc.
    /// </summary>
    public async Task<int> PumpOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var claimStore = scope.ServiceProvider.GetRequiredService<OutboxClaimStore>();

        var now = _timeProvider.GetUtcNow();
        var rows = await claimStore.ClaimAsync(_options.BatchSize, now, _options.Lease, cancellationToken);
        if (rows.Count == 0)
        {
            return 0;
        }

        // IS-EMRI-o86-C D-A3: pump'taki TUM farkli ScopeId/OldScopeId'ler once toplanir, TEK
        // GetMembersAsync cagrisiyla cozulur (D-A2 PAZARLIKSIZ -- satir basina sorgu YOK).
        var membershipSource = scope.ServiceProvider.GetRequiredService<IScopeMembershipSource>();
        var projectIds = CollectScopeIds(rows);
        var membersByProject = await membershipSource.GetMembersAsync(projectIds, cancellationToken);

        var publisher = scope.ServiceProvider.GetRequiredService<ISignalPublisher>();
        var envelopes = BuildEnvelopes(rows, membersByProject);
        var failures = await publisher.PublishAsync(envelopes, cancellationToken);
        var failedGroups = failures.Select(f => f.Group).ToHashSet(StringComparer.Ordinal);

        var okIds = new List<Guid>();
        var reopenRows = new List<(Guid Id, int BackoffSeconds)>();
        foreach (var row in rows)
        {
            // D1 YB-1: a row is closed only if EVERY group it belongs to succeeded.
            if (GroupsFor(row, membersByProject).Any(failedGroups.Contains))
            {
                reopenRows.Add((row.Id, Backoff(row.Attempts)));
            }
            else
            {
                okIds.Add(row.Id);
            }
        }

        await claimStore.CloseAsync(okIds, reopenRows, now, cancellationToken);
        return rows.Count;
    }

    private static IReadOnlyCollection<Guid> CollectScopeIds(IReadOnlyList<ClaimedOutboxRow> rows)
    {
        var ids = new HashSet<Guid>();
        foreach (var row in rows)
        {
            if (row.ScopeId is { } scope)
            {
                ids.Add(scope);
            }

            if (row.OldScopeId is { } oldScope)
            {
                ids.Add(oldScope);
            }
        }

        return ids;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var idle = true;
            try
            {
                idle = await PumpOnceAsync(stoppingToken) == 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // D2-e: ExecuteAsync must NEVER let an exception escape (BackgroundServiceExceptionBehavior
                // .StopHost is the default and would kill the whole host, including a DB-less one).
                _logger.LogWarning(ex, "Outbox dispatcher pump failed; backing off.");
            }

            if (idle)
            {
                try
                {
                    await Task.Delay(PollDelay(), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private static TimeSpan PollDelay() =>
        TimeSpan.FromSeconds(1) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));

    /// <summary>
    /// <c>attempts</c> is a REQUEST counter (bumped every claim, D2-c) -- backoff uses the post-increment
    /// value the row was just claimed with, not a separate failure count.
    /// </summary>
    private static int Backoff(int attemptsAfterClaim) =>
        (int)Math.Min(Math.Pow(2, attemptsAfterClaim), 60);

    /// <summary>Pump-internal reduction (D3): rows sharing a group in ONE pump collapse to one signal
    /// carrying the group's LARGEST cursor. Across separate pumps a group may re-signal -- harmless,
    /// since a hint is not a cursor.</summary>
    private static IReadOnlyList<SignalEnvelope> BuildEnvelopes(
        IReadOnlyList<ClaimedOutboxRow> rows,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> membersByProject)
    {
        var maxByGroup = new Dictionary<string, WireCursor>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            foreach (var group in GroupsFor(row, membersByProject))
            {
                if (!maxByGroup.TryGetValue(group, out var existing) || IsGreater(row.Cursor, existing))
                {
                    maxByGroup[group] = row.Cursor;
                }
            }
        }

        return maxByGroup.Select(kv => new SignalEnvelope(kv.Key, kv.Value)).ToList();
    }

    private static bool IsGreater(WireCursor candidate, WireCursor current) =>
        candidate.Xid > current.Xid || (candidate.Xid == current.Xid && candidate.Seq > current.Seq);

    /// <summary>
    /// IS-EMRI-o86-C D-A1/D-A3 PAZARLIKSIZ: <c>scope:{X}</c> KALKTI -- owner always (<c>user:</c>,
    /// unchanged, Gelen Kutusu yolu), then <c>X</c>'s CURRENT <c>project_access</c> members (resolved at
    /// PUBLISH time, not the row's own connect-time declaration) as individual <c>user:{memberId}</c>
    /// envelopes, for both the scope and (D7 double-publish) the old_scope. D-A5 fail-closed: a scope
    /// missing from <paramref name="membersByProject"/> (or mapped to zero members) yields NO envelope
    /// for it -- never guessed, never silently skipped-as-empty-but-still-attempted.
    /// </summary>
    private static IEnumerable<string> GroupsFor(
        ClaimedOutboxRow row, IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> membersByProject)
    {
        yield return $"user:{row.OwnerId}";
        if (row.ScopeId is { } scope && membersByProject.TryGetValue(scope, out var members))
        {
            foreach (var memberId in members)
            {
                yield return $"user:{memberId}";
            }
        }

        if (row.OldScopeId is { } oldScope && membersByProject.TryGetValue(oldScope, out var oldMembers))
        {
            foreach (var memberId in oldMembers)
            {
                yield return $"user:{memberId}";
            }
        }
    }
}

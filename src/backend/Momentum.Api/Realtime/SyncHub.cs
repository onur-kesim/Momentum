using Microsoft.AspNetCore.SignalR;
using Momentum.Application.Abstractions;

namespace Momentum.Api.Realtime;

/// <summary>
/// Payload-less realtime hub (ADR 0002 K2-G1/G2/G3, GOREV slice-2b2 D4/D5; IS-EMRI-o86-C §B D-B1: md.6
/// "olu kod borctur"). The REAL deny-by-default gate is the <c>/hubs/sync</c> negotiate-401 middleware
/// (Program.cs, D4) -- SignalR's own handshake completes before <see cref="OnConnectedAsync"/> ever
/// runs, so <see cref="HubCallerContext.Abort"/> here is defense-in-depth ONLY, never reported as an
/// independent gate (2b1's GREATEST lesson: only what a mutant can be shown to bite is a "gate").
/// <para>
/// IS-EMRI-o86-C §A moved scope routing from connect-time (this hub joining a <c>scope:{P}</c> group)
/// to PUBLISH-time (the outbox dispatcher resolving <c>P</c>'s CURRENT members into <c>user:{memberId}</c>
/// envelopes, <see cref="Momentum.Infrastructure.Sync.OutboxDispatcher"/>) -- sinir 38'in altinci
/// isirigi kapanisi: a connection's group set no longer freezes visibility as of the moment it dialed
/// in. The ONLY group left here is the always-fresh, membership-independent <c>user:{self}</c> one.
/// </para>
/// </summary>
public sealed class SyncHub(ICurrentUser currentUser) : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (currentUser.UserId is { } userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}", Context.ConnectionAborted);
        }
        else
        {
            Context.Abort();
        }

        await base.OnConnectedAsync();
    }
}

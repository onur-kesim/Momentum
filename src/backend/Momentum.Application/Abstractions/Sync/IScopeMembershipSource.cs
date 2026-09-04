namespace Momentum.Application.Abstractions.Sync;

/// <summary>
/// Scope membership for realtime group assignment (ADR 0002 K2-G2 half, GOREV slice-2b2 D5). Consulted
/// on EVERY hub connect (never cached) so a connection's group set reflects current visibility, not a
/// stale snapshot. IS-EMRI-o86-A §D3: backed by the real `project_members` table now -- the "named gap"
/// (read-only collaborators who never wrote couldn't join their scope group) is closed.
/// </summary>
public interface IScopeMembershipSource
{
    Task<IReadOnlyCollection<Guid>> GetScopesAsync(Guid userId, CancellationToken cancellationToken);
}

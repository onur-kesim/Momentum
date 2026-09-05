namespace Momentum.Application.Abstractions.Sync;

/// <summary>
/// Scope membership for realtime group routing (ADR 0002 K2-G2 half, GOREV slice-2b2 D5;
/// IS-EMRI-o86-C §A: publish-time resolution replaces connect-time -- sinir 38'in altinci isirigi).
/// <see cref="GetMembersAsync"/> is consulted at PUBLISH time (by the outbox dispatcher), never at
/// connect time and never cached, so a scope born or shared AFTER a connection was established still
/// reaches it -- the decision looks at the entity's CURRENT context, not the publishing row's own
/// declaration (the closing pattern of sinir 38). IS-EMRI-o86-A §D3: backed by the real
/// `project_access` view (owner ∪ member) -- the "named gap" (read-only collaborators who never wrote
/// couldn't join their scope group) stays closed.
/// </summary>
public interface IScopeMembershipSource
{
    /// <summary>
    /// TOPLU (batched) lookup: every project id in <paramref name="projectIds"/> is resolved in ONE
    /// call (IS-EMRI-o86-C D-A2 PAZARLIKSIZ -- no per-row N+1 query). A project absent from the
    /// returned dictionary, or mapped to an empty collection, means it currently has NO resolvable
    /// members (D-A5 fail-closed: the caller must publish NO envelope for that scope, never guess).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetMembersAsync(
        IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);
}

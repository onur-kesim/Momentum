using Mediator;
using Momentum.Application.Abstractions.Auth;

namespace Momentum.Application.Features.Auth;

/// <summary>IS-EMRI-o86-A §F: POST /v1/users/lookup (e-posta -> userId). Yanit YALNIZ userId tasir.</summary>
public sealed record LookupUserQuery(string Email) : IQuery<Guid?>;

public sealed class LookupUserQueryHandler(IUserStore userStore) : IQueryHandler<LookupUserQuery, Guid?>
{
    public async ValueTask<Guid?> Handle(LookupUserQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var normalized = EmailNormalizer.Normalize(query.Email);
        var user = await userStore.FindByNormalizedEmailAsync(normalized, cancellationToken);
        return user?.Id;
    }
}

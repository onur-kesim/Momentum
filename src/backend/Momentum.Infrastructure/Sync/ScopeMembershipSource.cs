using Momentum.Application.Abstractions.Sync;
using Momentum.Infrastructure.Persistence;

namespace Momentum.Infrastructure.Sync;

/// <summary>
/// <see cref="IScopeMembershipSource"/> -- IS-EMRI-o86-A §D3: artik outbox'tan degil GERCEK
/// `project_members` uyelik tablosundan okur ("named gap" KAPANDI). Bir kullanicinin gorunurlugu
/// GERI ALINDIGINDA `project_members`teki satiri silinir ve bu sorgu ANINDA yansitir -- eski
/// outbox-tabanli yaklasimin "hic yazmamis salt-okunur uye hic katilamaz" kusuru da kalkti.
/// </summary>
public sealed class ScopeMembershipSource(SyncDbContext db) : IScopeMembershipSource
{
    public async Task<IReadOnlyCollection<Guid>> GetScopesAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var command = await db.CreateRawCommandAsync(
            "SELECT project_id FROM project_members WHERE user_id = @userId",
            cancellationToken);
        command.Parameters.AddWithValue("userId", userId);

        var scopes = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            scopes.Add(reader.GetGuid(0));
        }

        return scopes;
    }
}

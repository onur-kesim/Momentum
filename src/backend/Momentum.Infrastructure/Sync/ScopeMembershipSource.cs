using Momentum.Application.Abstractions.Sync;
using Momentum.Infrastructure.Persistence;

namespace Momentum.Infrastructure.Sync;

/// <summary>
/// <see cref="IScopeMembershipSource"/> -- IS-EMRI-o86-C §A: PUBLISH-time membership, not connect-time
/// (sinir 38'in altinci isirigi kapanisi). Okur GERCEK `project_access` GORUNUMUNDEN ("named gap"
/// KAPALI KALIR). Bir kullanicinin gorunurlugu GERI ALINDIGINDA uyelik tablosundaki satiri silinir ve
/// bu sorgu ANINDA yansitir -- eski outbox-tabanli yaklasimin "hic yazmamis salt-okunur uye hic
/// katilamaz" kusuru da kalkti. IS-EMRI-o86-A2 §B/H2: uyelik tablosu degil `project_access` sorgulanir
/// -- sahip de `projects.owner_id`den kapsandigi icin donen kumeye dahildir (sahip uyelik tablosuna
/// YAZILMAZ, §C3). §G: bu dosyada uyelik tablosunun adi GECMEZ (mekanik kapi).
/// Onur kilidi (4. bulgu dogrulamasi, o86-A2): "ANINDA yansitir" iddiasi YALNIZ BU sorgunun (realtime
/// yayin karari) davranisidir -- `project_access` GUNCEL uyelikten okundugu icin dogru. PULL'un
/// (snapshot/artimli) KENDI gorunurlugu bu dosyanin disindadir; o kanaldaki "eski uye kendi eski
/// yazisiyla sonsuza dek gorur" sizintisi SyncPuller.cs'de ayrica bulunup kapatildi (bkz. o dosyadaki
/// "4. bulgu" yorumlari) -- bu yorum PULL icin bir garanti vermez, yalniz kendi sorgusu icindir.
/// </summary>
public sealed class ScopeMembershipSource(SyncDbContext db) : IScopeMembershipSource
{
    /// <summary>
    /// IS-EMRI-o86-C D-A2 PAZARLIKSIZ: TEK toplu sorgu -- `projectIds` bos ise DB'ye hic gidilmez ve
    /// bos sozluk doner (D-A5 fail-closed: cagiran hicbir zarf uretmemelidir, sessizce varsaymaz).
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetMembersAsync(
        IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, IReadOnlyCollection<Guid>>();
        if (projectIds.Count == 0)
        {
            return result;
        }

        var byProject = new Dictionary<Guid, List<Guid>>();
        await using var command = await db.CreateRawCommandAsync(
            "SELECT project_id, user_id FROM project_access WHERE project_id = ANY(@projectIds)",
            cancellationToken);
        command.Parameters.AddWithValue("projectIds", projectIds.ToArray());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var projectId = reader.GetGuid(0);
            var userId = reader.GetGuid(1);
            if (!byProject.TryGetValue(projectId, out var members))
            {
                members = [];
                byProject[projectId] = members;
            }

            members.Add(userId);
        }

        foreach (var (projectId, members) in byProject)
        {
            result[projectId] = members;
        }

        return result;
    }
}

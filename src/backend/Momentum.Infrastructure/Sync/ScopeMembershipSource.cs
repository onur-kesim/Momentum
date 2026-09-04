using Momentum.Application.Abstractions.Sync;
using Momentum.Infrastructure.Persistence;

namespace Momentum.Infrastructure.Sync;

/// <summary>
/// <see cref="IScopeMembershipSource"/> -- IS-EMRI-o86-A §D3: artik outbox'tan degil GERCEK
/// `project_access` GORUNUMUNDEN okur ("named gap" KAPANDI). Bir kullanicinin gorunurlugu
/// GERI ALINDIGINDA uyelik tablosundaki satiri silinir ve bu sorgu ANINDA yansitir -- eski
/// outbox-tabanli yaklasimin "hic yazmamis salt-okunur uye hic katilamaz" kusuru da kalkti.
/// IS-EMRI-o86-A2 §B/H2: uyelik tablosu degil `project_access` sorgulanir -- sahip de kendi
/// projesinin `scope:{P}` grubuna KATILIR (sahip uyelik tablosuna YAZILMAZ, §C3; gorunum onu
/// `projects.owner_id`den kapsar). §G: bu dosyada uyelik tablosunun adi GECMEZ (mekanik kapi).
/// Onur kilidi (4. bulgu dogrulamasi): "ANINDA yansitir" iddiasi YALNIZ BU sorgunun (realtime hub
/// grubu) davranisidir -- `project_access` GUNCEL uyelikten okundugu icin dogru. PULL'un (snapshot/
/// artimli) KENDI gorunurlugu bu dosyanin disindadir; o kanaldaki "eski uye kendi eski yazisiyla
/// sonsuza dek gorur" sizintisi SyncPuller.cs'de ayrica bulunup kapatildi (bkz. o dosyadaki
/// "4. bulgu" yorumlari) -- bu yorum PULL icin bir garanti vermez, yalniz kendi sorgusu icindir.
/// </summary>
public sealed class ScopeMembershipSource(SyncDbContext db) : IScopeMembershipSource
{
    public async Task<IReadOnlyCollection<Guid>> GetScopesAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var command = await db.CreateRawCommandAsync(
            "SELECT project_id FROM project_access WHERE user_id = @userId",
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

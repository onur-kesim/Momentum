# İŞ EMRİ o86-C — SINIR 38'in ALTINCI ISIRIĞI: REALTIME'DA BAĞLANTI-ANI FOTOĞRAFI

`MOD: KRİTİK` · kutu **5-6 Eyl 2026** · yazan: **Cowork** (oturum 88) · koşacak: **Claude Code**
Öncül: o86-B'nin §F canlı turu. **§F adım 6/7 DÜŞTÜ** — bu emir onu kaldırır.

🔴 **Neden bu emir var:** görünürlük kararı **yanlış anda alınmış bir fotoğrafa** bakıyor. Sınıf,
o86-A'da beş kez ısıran sınıfın **aynısı** (DURUM.md sınır 38). Doğruluk tablosu senkron yolunu
kapattı; **realtime yolu hiç kapsanmadı** ve altıncı ısırık orada çıktı.

---

## 0. KİLİTLER (Onur, 4 Eyl 2026)

- **K-o88/5 — MAYIN 1 KALKTI, SINIRLI.** `src/backend` yalnız §A ve §B'de **adı geçen dosyalarda**
  değişir. Başka hiçbir sunucu dosyasına dokunulmaz; bu bir açık çek değildir.
- **K-o88/6 — Restart'lı geçiş REDDEDİLDİ.** Force-stop + yeniden açma, yasaklanan "Yenile"
  düğmesinden **daha büyük** bir kullanıcı müdahalesidir. §F/6-7 ancak **hiçbir müdahale olmadan**
  yeşile dönerse geçer.
- **K-o88/7 — İŞLEYİŞ md.6 gereği ŞİMDİ YAP.** Bulguyu "sınır" diye yazıp devam etmek borç
  tutmaktır; borç defteri yasak.

---

## 1. DEMİR KURALLAR

1. 🔴 Değişebilecek dosyalar **tam liste**: `OutboxDispatcher.cs` · `IScopeMembershipSource.cs` ·
   `ScopeMembershipSource.cs` · `SyncHub.cs` · `RealtimeMembershipTests`. **Başkası YASAK.**
2. 🔴 **ADR/spec YAZILMAZ** (İŞLEYİŞ md.4). Tasarım bu emirdedir.
3. **Yeni kapı DOSYASI açılmaz** (CLAUDE.md §4/10). Testler mevcut dosyalara girer.
4. `SignalRSignalPublisher.cs` **DEĞİŞMEZ** — sinyali kimin alacağını o belirlemiyor.
5. İstemci (`src/client/**`) **DEĞİŞMEZ.** o86-B'nin §A-§E'si olduğu gibi kalır.
6. `verify.ps1` · `DURUM.md` · `CLAUDE.md` · `arsiv/` · `.github/workflows/*`: dokunma.
   `git add` yol belirterek; `git add -A` YASAK. **PUSH ONUR'DA.**

---

## 2. ÖLÇÜLMÜŞ ZEMİN (oturum 88'de kaynaktan doğrulandı)

| # | Ölçüm | Yer |
|---|---|---|
| Z1 | 🔴 `GroupsFor(row)` **statik ve saf** — grubu **satırın KENDİ alanlarından** üretir: `user:{OwnerId}` · `scope:{ScopeId}` · `scope:{OldScopeId}` | `OutboxDispatcher.cs` `GroupsFor` |
| Z2 | 🟢 **`user:{userId}` grubuna HER bağlantı koşulsuz katılıyor** — üyelikten bağımsız, asla bayatlamaz. Düzeltmenin tesisatı **kurulu** | `SyncHub.cs:20` |
| Z3 | 🔴 `scope:{...}` grupları **yalnız bağlantı anında** dolduruluyor ⇒ bağlantıdan sonra doğan/paylaşılan proje için grup **boş kalır** | `SyncHub.cs:22-27` |
| Z4 | Yayıncı, zarfın taşıdığı gruba körlemesine gönderiyor ⇒ **hedefi dispatcher seçiyor** | `SignalRSignalPublisher.cs:23` |
| Z5 | `project_access` görünümü **güncel** üyeliği veriyor (sahip ∪ üye) ve zaten sorgulanıyor | `ScopeMembershipSource.cs` |
| Z6 | Zarf **yüksüz**: `SignalEnvelope(Group, CursorHint)`, içerik taşımaz (K77/6) | `SyncContracts.cs:75` |

**Z1+Z3 birlikte:** A da Proje X'i **bağlandıktan sonra** yarattı ⇒ A'nın bağlantısı da
`scope:ProjeX`e girmedi. Kusur **tek taraflı değil**: bağlantı kurulduktan sonra yaratılan ya da
paylaşılan hiçbir liste **iki yönde de** canlı yayılmıyor.

---

## 3. §A — Düzeltme: üyelik YAYIN ANINDA çözülür

**D-A1 PAZARLIKSIZ:** `scope:{X}` grubuna yayın **KALKAR**. Yerine, `X`in **o an** `project_access`te
görünen her üyesi için `user:{üyeId}` zarfı üretilir. Karar artık satırın beyanına değil,
**varlığın yayın anındaki bağlamına** bakar — sınır 38'in kapanış kalıbının aynısı.

**D-A2:** Porta ters yön eklenir:
`IScopeMembershipSource.GetMembersAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken)`
→ `IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>`.
SQL: `SELECT project_id, user_id FROM project_access WHERE project_id = ANY(@ids)`.
🔴 **TOPLU sorgu** — satır başına sorgu (N+1) YASAK; bir pump'ta **tek** çağrı.
🔴 `project_members` adı bu dosyada **GEÇMEZ** (mevcut mekanik kapı, `ScopeMembershipSource` §G).

**D-A3:** `GroupsFor` statik olmaktan çıkar; `BuildEnvelopes` async olur. Sıra: pump'taki **tüm**
farklı `ScopeId`/`OldScopeId`'ler toplanır → **tek** `GetMembersAsync` → gruplar üretilir.
`user:{row.OwnerId}` **AYNEN KALIR** (Gelen Kutusu yolu değişmez).
`maxByGroup` daraltması (D3) **aynen korunur** — artık `user:` grupları üzerinde çalışır.

**D-A4 — `old_scope` davranış değişikliği, BEYAN EDİLECEK:** projeden **çıkarılmış** kullanıcı
`project_access`te olmadığı için artık `old_scope` yayınını **almaz**. Bugün alıyor (bağlantı anında
girdiği bayat gruptan). Bu **daha doğru** (erişimi yok) ama **davranış değişikliğidir**: mevcut bir
testi kırarsa **DUR ve Onur'a bildir**, testi kendi başına değiştirme.

**D-A5 — fail-closed:** `GetMembersAsync` bir proje için **boş** dönerse o scope'a **hiç** zarf
üretilmez (yayın sızdırmaz). Sorgu **hata verirse** pump o turda başarısız sayılır ve mevcut
backoff'a düşer — sessizce boş küme **DÖNÜLMEZ**.

---

## 4. §B — `SyncHub` temizliği (md.6: ölü kod borçtur)

**D-B1:** §A sonrası `scope:` gruplarına **kimse yayın yapmaz** ⇒ `SyncHub.cs:22-27`'deki
`GetScopesAsync` + `AddToGroupAsync($"scope:{...}")` döngüsü **ölü koddur ve SİLİNİR**.
`user:{userId}` katılımı (satır 20) ve `Context.Abort()` dalı **AYNEN KALIR**.
Port artık yalnız `GetMembersAsync` taşıyorsa `GetScopesAsync` da silinir; başka çağıranı varsa **DUR**.

**D-B2 — `RealtimeMembershipTests` DEĞİŞEBİLİR, ama açıkta:** bu test **kilitle değişen bir
sözleşmeyi** sınıyor (üyelik bağlantı anında değil yayın anında çözülür). Bu, o86-B'deki
`liste_baglam_test.dart` kuralının istisnası **DEĞİL, farklı bir durumdur**: orada test gerçek bir
regresyonu yakalıyordu ve düzenlemek kanaryayı susturmak olurdu; burada sınanan **invaryantın
kendisi** Onur kilidiyle değişti. 🔴 Bu yüzden diff **CEVAP'ta birebir gösterilir**
(`git diff -- <test yolu>`), gerekçesi tek cümleyle yazılır. Başka hiçbir testte bu gerekçe geçerli
değildir.

---

## 5. §C — MEKANİK KAPI (İŞLEYİŞ md.8: altıncı ısırık, kural CI'da koşar)

Mevcut test dosyasına, **mutantla öldürülebilir** tek iddia:

> **H9 — Bağlantıdan SONRA davet edilen üye, YENİDEN BAĞLANMADAN sinyal alır.**
> Kurulum: B bağlanır → *sonra* A, P projesine B'yi ekler → P'ye bir op yazılır.
> İddia: B'nin bağlantısı `Changed` **alır**.
> **Mutant:** `GetMembersAsync` çağrısını bağlantı anındaki üyelikle değiştir ⇒ test **KIRMIZI** olmalı.
> Mutant kırmızı olmuyorsa test **iddiayı sınamıyordur**, yeniden yaz.

🔴 **POZİTİF KONTROLÜN EŞİ (zorunlu):** projede **üye olmayan** C, aynı op için `Changed` **ALMAZ**.
Bu iddia yoksa H9 boş kümeyle geçer ve hiçbir şey ispatlamaz.

---

## 6. §D — §F'nin yeniden koşumu (o86-B'den devralınır)

Yalnız **adım 6 ve 7** yeniden koşulur; 1-5 ve 8-9 zaten ölçüldüyse tekrarlanmaz (kanıtları durur).

- **6.** B'nin Drawer'ında Proje X **kendiliğinden** belirir — **force-stop YOK, Yenile YOK,
  arka plana alma YOK**. ⏱ süre yazılır.
- **7.** B "görev-2" ekler ⇒ **A'nın** ekranında kendiliğinden belirir. ⏱ süre yazılır.
  🔴 Bu adım o86-B'de hiç ölçülmedi; A da `scope:ProjeX`te değildi (Z1+Z3). **Ayrı ölç.**
- Ulaşım o86-B'deki gibi **adb reverse** (firewall/admin yok).

---

## 7. §E — CEVAP

- Değişen dosyalar + satır sayıları (liste §1/1'deki beşi geçemez).
- `git diff -- <RealtimeMembershipTests yolu>` **birebir** + tek cümlelik gerekçe (D-B2).
- H9 mutantının **KIRMIZI** çıktısı (ham).
- `verify.ps1` EXIT — backend kapalıyken (mayın 6).
- §D/6 ve §D/7'nin ham çıktısı + ⏱ süreler ⇒ `KANIT/o86C/`.
- D-A4'ün (old_scope) mevcut testlere etkisi: kırdı mı, kırmadı mı — **ölç, yaz**.
- DURUM.md'ye önerilen **tek satır**: sınır 38'in realtime üyesi ve kapanışı (yazma, **öner**).

**Kutu:** 5-6 Eyl. Sığmazsa İŞLEYİŞ md.1 — süre uzamaz, **madde kesilir**; sıradaki kesme **tekrar**.

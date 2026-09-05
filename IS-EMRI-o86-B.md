# İŞ EMRİ o86-B — DİLİM 3 / İŞBİRLİĞİ · İSTEMCİ AYAĞI + VİTRİN

`MOD: NORMAL` · kutu **4-6 Eyl 2026** · yazan: **Cowork** (oturum 88) · koşacak: **Claude Code**
Öncül: **`fa372ff`** (o87 kapanışı, push'lu). Sunucu ayağı `o86-A3` ile KABUL edildi ve **kapandı**.

🔴 **Neden bu emir var:** CLAUDE.md §2'nin açık maddesi — *"İki kullanıcı bir listeyi paylaşır;
birinin yazdığı ötekinin ekranında belirir"* — sunucuda hazır, **istemcide hiç yok**. Bu emir o
maddeyi ✅'ye çevirir ve dilimin vitrinini **canlı** ölçer.

---

## 0. KİLİTLER (Onur, 4 Eyl 2026 — oturum 88)

- **K-o88/1 — Kapsam:** yalnız **davet + paylaşım**. Üye listesini gösterme · üye çıkarma ·
  realtime'ı web'de açma: **KAPSAM DIŞI** (İŞLEYİŞ md.7 "tek vitrin").
- **K-o88/2 — Vitrin:** iddia **iki Android emülatöründe** ölçülür (realtime'ın AÇIK olduğu tek
  hedef). Protokol seviyesi ölçüm bu dilimde **yeterli değildir** — DURUM.md sınır 34 kapanır.
- **K-o88/3 — Sessizlik yasak:** reddedilen davet kullanıcıya **görünmek zorunda**. Gerekçe §C'de
  ölçüldü. İŞLEYİŞ md.6 gereği bu bir borç değil, **ŞİMDİ YAP**tır.

---

## 1. DEMİR KURALLAR

1. 🔴 **SUNUCU DEĞİŞMEZ** (`src/backend/**`). Sunucu ayağı kapandı; **tele yeni alan EKLENMEZ**.
2. 🔴 **ADR/spec YAZILMAZ** (İŞLEYİŞ md.4). Tasarım **bu emirdedir**, başka kâğıt üretilmez.
3. **Yeni kapı DOSYASI açılmaz** (CLAUDE.md §4/10). Testler `paylasim_dilimi_test.dart`e girer —
   bu bir **ürün testidir**, kapı değil, %10 oranına **girmez** (İŞLEYİŞ md.3).
4. 🔴 **`rozetDikisi` DEĞİŞMEZ** (`gorev_deposu.dart`). D1/D2 invaryantları ve
   `g10_rozet_kapsami_test.dart` bu emirde **açılmaz**.
5. `verify.ps1` · `DURUM.md` · `CLAUDE.md` · `arsiv/` · `.github/workflows/*`: **dokunma**.
6. `dart format lib/` **YASAK** (sınır 27) — yalnız **dokunduğun** dosyada.
7. Flutter komutları **`src/client`'tan** (sınır 1). PowerShell 5.1'de `&&` yok, `;` yaz.
8. `git add` **yol belirterek**; `git add -A` **YASAK**. **PUSH ONUR'DA.**

---

## 2. ÖLÇÜLMÜŞ ZEMİN (oturum 88'de kaynaktan doğrulandı — varsayma, GÜVEN)

| # | Ölçüm | Yer |
|---|---|---|
| Z1 | `WireOp.sets` + `WireSetDelta`/`WireSetAdd` **var** (etiket OrSet şablonu) | `veri/wire_op.dart:31-41, 65-121` |
| Z2 | `POST /v1/users/lookup` **sunucuda hazır**; yanıt YALNIZ `userId` taşır | `UserLookupEndpoints.cs:22`, `LookupUserQuery.cs:6` |
| Z3 | `members` elemanı **userId**'dir, e-posta **DEĞİL** | `UserLookupEndpoints.cs:14` |
| Z4 | `members` yazımını **YALNIZ proje sahibi** yapabilir | `SyncCommandHandler.cs:244, 266` |
| Z5 | Pull'da yeni `Project` için **INSERT edilir** | `senkron/uzak_degisiklik_uygulayici.dart:794+` |
| Z6 | `listelerGorunur()` **sahip süzgeci taşımaz** ⇒ paylaşılan liste Drawer'da kendiliğinden belirir | `veri/gorev_deposu.dart:943` |
| Z7 | 🔴 `_rozetYaz` rozeti **`Gorevler`** tablosuna yazar ⇒ `Project` op'unda **0 satır**, red **görünmez** | `veri/senkron_dongusu.dart` (`_rozetYaz`) |
| Z8 | 🔴 Zehirli satır bekleyen sorgusundan **dışlanır** ⇒ ne yeniden denenir ne görünür | `veri/senkron_dongusu.dart:156` |
| Z9 | 🔴 `SnapshotEntity` **owner_id taşımaz** ⇒ istemci sahipliği **öğrenemez** | `SyncPuller.cs:189` |
| Z10 | İstemci `sets`i **yalnız `Task`** için okur ⇒ `members` istemciye ulaşsa da **atılır** | `uzak_degisiklik_uygulayici.dart:256, 542` |
| Z11 | Realtime **web'de KAPALI** (`kIsWeb` erken dönüş) | `ag/signalr_json_sinyal.dart:100` |
| Z12 | Drawer liste satırının `trailing`inde **zaten iki** IconButton var | `sunum/gorev_listesi_ekrani.dart:268-297` |

**Z7+Z8+Z9 birlikte:** davet, normal kullanımda **reddedilebilen ilk `Project` op'udur** — bu yol
bugüne kadar hiç ısırmadı. Düğmeyi "yalnız sahip" diye kapatmak Z9 yüzünden **imkânsız** (sunucu
ayağını yeniden açardı). Bu yüzden çözüm §C'dir.

---

## 3. §A — Davet op'u (depo katmanı)

**Yeni metot** `GorevDeposu.uyeEkle(String projeId, String userId)`:

- **D-A1 PAZARLIKSIZ:** `listeEkle` (`gorev_deposu.dart:969`) ile **AYNI desen** — `entityType:
  'Project'`, **TEK** `WireOp`, **TEK** `transaction()`.
- **D-A2 PAZARLIKSIZ:** kanal yalnız `sets`:
  `sets: {'members': WireSetDelta(adds: [WireSetAdd(el: userId, tag: <üretilen>, hlc: opHlc)])}`
  `fields` ve `groups` **BOŞ** — `WireOp` D2 şartı (`en az bir kanal`) `sets` ile sağlanır.
- **D-A3 PAZARLIKSIZ:** `WireSetAdd.tag` üretimi ve `hlc` ataması **etiket yolundan BİREBİR
  kopyalanır** (`gorev_deposu.dart` ~620-640 ve ~800-820) — **yeniden icat edilmez**.
- **D-A4:** **yerel projeksiyon YAZILMAZ.** `Projeler` tablosunda `members` sütunu yok ve K-o88/1
  gereği açılmıyor ⇒ metot yalnız kuyruğa yazar. (`listeEkle`den **tek farkı** budur; sebebi
  emirde geçmezse sonraki oturum "eksik" sanır.)

---

## 4. §B — Lookup portu (ağ katmanı)

**Yeni dosya** `lib/ag/kullanici_arama_agi.dart`:

```dart
sealed class KullaniciAramaSonucu {}
class KullaniciBulundu   extends KullaniciAramaSonucu { final String userId; }
class KullaniciBulunamadi extends KullaniciAramaSonucu {}
class KullaniciAramaHatasi extends KullaniciAramaSonucu {}   // ağ/5xx/401-sonrası
abstract class KullaniciAramaAgi { Future<KullaniciAramaSonucu> ara(String eposta); }
```

Üretim uygulaması `lib/ag/http_kullanici_arama_agi.dart` → `POST {taban}/v1/users/lookup`.

- **D-B1:** jeton işleme **`HttpSenkronAgi`nin deseniyle aynı** (`erisimJetonuAl` + `jetonuYenile`
  enjekte edilir; 401'de sessiz yenileme). `AuthAgi`ye **DOKUNULMAZ** — kimlik dilimi kapalı ve
  onun metotları `AuthSonucu` döndürür, bu uç `userId` döndürür.
- **D-B2:** yanıtta `userId` yoksa/`null` ise ⇒ `KullaniciBulunamadi`. **Ham gövde loglanmaz**
  (e-posta PII).
- **D-B3:** üç sonuç **ayrı sınıflardır** — `null` ile "bulunamadı"yı ayırt etmek çağıranın işi
  olmaz (o84'ün gölgelenme sınıfı).

---

## 5. §C — Diyalog gerçek sonucu söyler (K-o88/3)

🔴 **Davet ÇEVRİMİÇİ bir eylemdir.** Gerekçe Z3: `members` elemanı userId'dir; lookup başarmadan
op **üretilemez**. Bu yüzden "çevrimdışı sıraya alındı" dalı **YOKTUR** ve yazılmayacaktır.

**PAZARLIKSIZ akış (sıra dâhil):**

1. E-posta yerel doğrulama (boş / `@` yok) ⇒ yerel hata, **ağ ÇAĞRILMAZ**.
2. `KullaniciAramaAgi.ara(eposta)`:
   - `KullaniciBulunamadi` ⇒ *"Bu e-postayla kayıtlı kullanıcı yok."* · **op ÜRETİLMEZ** · diyalog **açık kalır**.
   - `KullaniciAramaHatasi` ⇒ *"Bağlantı yok, davet gönderilemedi."* · **op ÜRETİLMEZ** · diyalog **açık kalır**.
3. `KullaniciBulundu(userId)` ve `userId == actorId` ⇒ *"Kendini davet edemezsin."* · **op ÜRETİLMEZ**.
4. `§A.uyeEkle(projeId, userId)` — `operationId` **saklanır**.
5. `await dongu.turCalistir()` (`senkron_dongusu.dart:104`).
6. Kuyruk satırı **o `operationId` ile** okunur:
   - **satır YOK** ⇒ ✅ *"&lt;e-posta&gt; listeye eklendi."* (Applied/Duplicate satırı siler,
     `senkron_dongusu.dart:344-347`) · diyalog **kapanır**.
   - **satır VAR, `durum=='zehirli'`** ⇒ ❌ *"Bu listeyi paylaşma yetkin yok."* + parantez içinde
     **ham `sonHataKodu`** · diyalog **açık kalır**.
   - **satır VAR, `durum!='zehirli'`** ⇒ ⚠ *"Gönderilemedi, yeniden dene."* · op kuyrukta
     **KALIR** (silinmez) · diyalog **açık kalır**.
- **D-C1:** ham hata kodu metinde **görünür** — destek/denetim onu ölçebilsin.
- **D-C2 PAZARLIKSIZ:** `_rozetYaz`'a **dokunulmaz**. Z7 bu emirde **düzeltilmez**, diyalogla
  **çevrelenir**; sınır §G'de DURUM.md'ye yazılır.

---

## 6. §D — UI (Drawer)

- **D-D1:** liste satırının `trailing` Row'una **üçüncü** IconButton (`Icons.person_add_outlined`,
  `key: ValueKey('liste_paylas_${proje.id}')`), mevcut ikisinin **birebir** kalıbıyla
  (`MOlcu.ikon`, `MOlcu.dokunmaHedefi`, `padding: EdgeInsets.zero`).
- **D-D2 🔴 ÖLÇ, VARSAYMA (Z12):** üçüncü düğmeyi ekledikten sonra `a11y_*` ve
  `gorev_satiri_rozet_genislik_test.dart` sınıfını **koş**. **KIRMIZI ise DUR** ve Onur'a bildir —
  düğmeleri menüye indirmek kapsam kararıdır, kendi başına yapma.
- **D-D3:** yeni dizgeler `design/metinler.dart`e `listePaylas*` önekiyle, satır 175-186 bloğunun
  **hemen ardına**. Gömülü dize **YASAK**.
- **D-D4:** paylaş düğmesi **her listede görünür** (Z9: sahiplik bilinemiyor). Üye basarsa §C/6'nın
  red dalı çalışır — bu **kabul edilen** davranıştır ve §F/9'da canlı ölçülür.

---

## 7. §E — Testler (`src/client/test/paylasim_dilimi_test.dart`, YENİ ürün testi)

Her iddianın bir **"olmamalı"** eşi vardır (DURUM.md "pozitif kontrol" dersi):

1. `Bulundu` ⇒ **tek** WireOp; `entityType=='Project'`; `sets['members'].adds` **tek eleman** =
   userId; `fields` **boş**. · **Negatif:** `Bulunamadi` ⇒ kuyruk **boş**.
2. `KullaniciAramaHatasi` ⇒ **hiç** op üretilmez.
3. `userId == actorId` ⇒ **hiç** op üretilmez.
4. Tur sonrası: satır yok ⇒ başarı metni · `zehirli` ⇒ red metni **ve ham kod** ekranda ·
   `bekliyor` ⇒ "yeniden dene". · **Negatif:** `zehirli` durumunda **başarı metni EKRANDA OLMAMALI**.
5. Üye tarafı: `entityType:'Project'` bir change/snapshot gelince `Projeler`e satır **doğar** ve
   `listelerGorunur()` onu **yayınlar**. · **Negatif:** `silindi=true` gelen proje **yayınlanmaz**.

---

## 8. §F — VİTRİN: iki Android emülatörü (K-o88/2)

Her adımın kanıtı `KANIT/o86B/` altına düşer.

1. Backend + Postgres ayağa (`docker-compose.gelistirme.yml`). `/health/ready` **200** (mayın 7).
2. İki emülatör; APK **ikisine de** kurulur.
3. Emülatör-1'de hesap **A**, emülatör-2'de hesap **B** kaydı.
   🔴 **ÖLÇÜM UYARISI (sınır 29):** define'sız APK `DEV_USER_ID`yi **rastgele** üretir. Gerçek
   giriş (JWT) kullanılıyorsa `X-Momentum-Dev-User` yolunun **devre dışı** olduğu
   `POST /v1/sync` **başlıklarından ÖLÇÜLÜR**, varsayılmaz. Ham çıktı CEVAP'a girer.
4. A: "Proje X" listesi + içine "görev-1".
5. A → **Paylaş** → B'nin e-postası ⇒ "eklendi" (ekran görüntüsü).
6. B'nin Drawer'ında "Proje X" **kendiliğinden** belirir — **elle yenileme YOK**. ⏱ süre yaz.
7. B "görev-2" ekler ⇒ **A'nın** ekranında belirir — **elle yenileme YOK**. ⏱ süre yaz.
8. 🔴 **POZİTİF KONTROL:** üçüncü hesap **C** (davet edilmemiş) "Proje X"i **GÖRMEZ**.
   **Bu adım atlanırsa vitrin GEÇERSİZDİR** (boş liste her iddiayı geçirir).
9. **NEGATİF CANLI:** B (üye) → Paylaş dener ⇒ *"yetkin yok"* metni **görünür** (§C/6 red dalı,
   Z7'nin çevrelendiğinin canlı kanıtı).

---

## 9. §G — CEVAP ve kanıt

- Değişen dosyalar + satır sayıları.
- `flutter analyze` — **yeni uyarı 0** (ham çıktı).
- `flutter test` özeti: **175** testten kaça çıktı.
- `verify.ps1` EXIT — **backend kapalıyken** (mayın 6: `netstat -ano | findstr :5298` **boş**).
- §F'nin **dokuz** adımının ham çıktısı/ekran görüntüleri ⇒ `KANIT/o86B/`.
- DURUM.md'ye önerilen **iki satır** (yazma, **öner** — Cowork yazar):
  (a) reddedilen `Project` op'unun rozetsizliği (Z7/Z8) · (b) builder beyanı örneklemesi (md.4).
- 🔴 `git add` **yol belirterek**. `KANIT/slice-3c/02-G2/*.json` **commit'e GİRMEZ** (sınır 19).
  **PUSH YOK.**

**Kutu:** 4-6 Eyl. Sığmazsa İŞLEYİŞ md.1 — süre uzamaz, **madde kesilir**; kesme sırası kilitli
(sıradaki: **tekrar**).

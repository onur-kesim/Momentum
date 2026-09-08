# ADR 0004 — Web Çapraz-Köken İzolasyonu (COOP/COEP) ve İstemcinin Aynı Kökenden Sunulması

- **Durum:** 📝 **GÖVDE YAZILDI — KİLİT BEKLİYOR.** `K127` gereği bağımsız denetim kilitten **önce** koşar.
- **Tarih:** 2026-08-05 (iskelet, o59) · **2026-08-08 (gövde, o66)**
- **Önceki iki gövde denetimde DÜŞTÜ:** `K164` (o64, iki denetçi: 6 bloker + 13 major) · `K165` (o65, üç
  denetçi: A **GEÇMEZ** · B **GEÇMEZ** · C **ONARIM YETERSİZ**). Taslaklar `KANIT/W3/07`, `08`.
  **Üç denetçinin ortak teşhisi:** *"belge VAR OLMAYAN artefaktlar hakkında hüküm veriyor"*.
- 🔴 **BU SÜRÜMÜN TEK KURALI:** her sayı ve her davranış iddiası
  **`KANIT/W3b/07-ADR0004-OLCUM-TABANI.txt`**'ten gelir — o dosya bu gövde yazılmadan **hemen önce**,
  cihazda, betikle üretildi. **Ezberden ya da başka belgeden taşınmış tek sayı yoktur.**
  Ölçülemeyen hiçbir şey karara girmedi; ölçülemeyenler **§8**'de.

---

## 1. BAĞLAM — iskeletin sorusu ve bugün ölçülen cevabı

İskelet (o59) tek bir soru sabitlemişti: *"Sunucu COOP/COEP başlıklarını nasıl ve ne bedelle ekler?"*
Ölçülmüş bağlam: drift'in `opfsLocks` yolu **çapraz-köken izolasyonu** ister; izolasyon yoksa istemci
`sharedIndexedDb`'ye düşer. `W2` bunu **görünür kıldı** ama onarmadı (kapsam dışıydı).

O günden bugüne **fiilen inen** artefaktlar (üçü de bu ADR yazılırken diskte ölçüldü — §6):
`Web/IzolasyonBasliklari.cs` · `Web/IstemciServisi.cs` · `wwwroot/` + `_BUILD.json` ·
`araclar/yayin-kapisi.py`. **İskeletin “açık soru”su artık açık değil — ölçüldü.**

---

## 2. KARAR (üç madde; hepsi bugün ÜRÜNDE canlı)

**D1 — İzolasyon başlıkları HER YANITTA, `OnStarting` ile yazılır.**
`Cross-Origin-Opener-Policy: same-origin` + `Cross-Origin-Embedder-Policy: require-corp`
(`IzolasyonBasliklari.cs`:32-36). Yazım **doğrudan değil `OnStarting`** ile yapılır (a.g.e.:58);
ölçülmüş gerekçe a.g.e.:43-45'te: `UseExceptionHandler` hata yolunda yanıt başlıklarını **temizler**,
doğrudan yazım o yolda **düşer**. Ara katman **en üstte** durur (`Program.cs`:117) ⇒ `UseCors`,
`UseExceptionHandler` ve tüm uç noktalar dâhil **her** yanıt izole olur.

**D2 — İstemci AYNI KÖKENDEN sunulur; kök dizin YAPILANDIRMADAN okunur.**
`Istemci:KokDizin` (`IstemciServisi.cs`:43) `appsettings.json`'da **göreli `"wwwroot"`**tur (§6/[5]).
Değer boş/tanımsızsa ara katman **hiç kurulmaz** (a.g.e.:85) ⇒ **kill switch bedavaya gelir**.
Sıra zorunludur ve ölçüldü: izolasyon başlıkları (`:117`) → statik servis (`:124`) → **açık**
`app.UseRouting()` (`:132`) → `UseCors()` (`:139`).

**D3 — Web build'i `--no-web-resources-cdn` ile üretilir ve `wwwroot`'a YAYINA ALINIR; kapı bunu ölçer.**
Build `araclar/web-yayina-al.py` ile koşar, `_BUILD.json` yazar; `araclar/yayin-kapisi.py`
(`W3b/G48`–`G51`) yapılandırmayı, `.gitignore`'u, build bütünlüğünü ve **bayrak izini** ölçer.
`wwwroot` **build çıktısıdır, repoya girmez** (`.gitignore`:31, tam yol — çıplak `wwwroot` deseni
deponun başka bir yerindeki aynı adlı dizini sessizce yutardı).

🔴 **Bu üç madde YENİ KARAR DEĞİL, ölçülmüş DURUMUN sabitlenmesidir.** Kilitleri `K159` (istemci
izolasyonu), `K159-b` (bayrak), `K167`/`K168` (`W3b`) taşıyor; bu ADR onları **tek yerde ve
gerekçesiyle** kaydeder.

---

## 3. GEREKÇE — yalnız ölçülmüş olan

| iddia | ölçüm | kanıt | tarih | güven |
|---|---|---|---|---|
| İzolasyon **fiilen** açık; gerçek tarayıcıda `crossOriginIsolated = true` | `04` §2'nin HTTP tablosunda **16 satırın 16'sı** `COOP: same-origin` + `COEP: require-corp` taşıyor (bu ADR yazılırken **betikle sayıldı**); dört API yüzeyi (`/v1/**`, `/health/**`, `/hubs/sync`, `/scalar/v1`) gölgelenmiyor; `04` §3 `crossOriginIsolated : True` | `KANIT/W3/04` §2-3 | 7 Ağu 2026 | **KESİN** *(sayı sınırı: §8/8)* |
| İstemci aynı kökenden **sunuluyor** | `GET /` → **200**, gövdede `flutter_bootstrap.js`; `GET /_BUILD.json` → **200** | `KANIT/W3b/02` | 8 Ağu 2026 | KESİN |
| Göreli kök dizinin **bedeli gerçek** (`N4`) | sunucu **başka** çalışma dizininden kalkınca `GET /` → **404** | `KANIT/W3b/02` §② | 8 Ağu 2026 | KESİN *(sınır: §8/1)* |
| CDN'e bağımlılık **kalktı** | `wwwroot/canvaskit/` altında **4** `.wasm`; `useLocalCanvasKit` değeri **`true`** | `KANIT/W3b/07` §7-8 | 8 Ağu 2026 | KESİN |
| Emülatör adresi web build'ine **sızmadı** | `wwwroot/**` içindeki `.js/.mjs/.html/.json` dosyalarında `10.0.2.2` → **0** | `KANIT/W3b/07` §9 | 8 Ağu 2026 | KESİN |
| Kapı **ısırıyor** | altın küme **29/29**, gerçek depoda **YEŞİL**; 20 mutantın **20'si** hüküm verdi, ölü **YOK** | `KANIT/W3b/03`,`04`,`06` | 8 Ağu 2026 | KESİN |
| Backend **bozulmadı** | `verify.ps1` **EXIT 0** · build 0 uyarı/0 hata · test **120/120** · CVE 0 | `KANIT/W3b/01` | 8 Ağu 2026 | KESİN |

🔴 **`--no-web-resources-cdn`'in gerekçesi COEP BLOKLAMASI DEĞİLDİR.** `gstatic` **8 Ağu 2026 09:50
UTC'de de** `cross-origin-resource-policy: cross-origin` gönderiyordu (bağımsız denetçi `curl -I` ile
ölçtü; `KANIT/W3/05` §4'ün 7 Ağu ölçümü de aynı). Yani bayrak olmasa da CanvasKit **muhtemelen
yüklenirdi**. Bayrağın **ölçülmüş** değeri başkadır: ① üçüncü-taraf CORP politikasına bağımlılığın
kalkması (o politikayı Google belirler, bu depo değil) ② çevrimdışı çalışabilirlik ③ tedarik zinciri
yüzeyinin daralması. 🔴 **Bu satır, o65 gövdesinin en pahalı hatasının düzeltmesidir:** o gövde bu
başlığı *"ÖLÇÜLEMEDİ"* ilan etmişti, denetçi **aynı konteynerden fiilen ölçtü.**
**Ders: ölçemediğini SANMAK, ölçmemekten pahalıdır.**

---

## 4. REDDEDİLEN ALTERNATİFLER (her biri ölçülmüş gerekçeyle)

| alternatif | red gerekçesi | kanıt |
|---|---|---|
| `COEP: credentialless` | `opfsLocks`'un istediği izolasyonu **vermez**; kapı bunu **bilerek** reddeder | `izolasyon-olc.py` sözleşmesi |
| Kök dizini `ContentRootPath`'e demirlemek | **ürün kodu değişir** ⇒ denetimden geçmiş `IstemciServisi.cs`'in kimliği ve mutantı yeniden ölçülmelidir; bedeli §7/1'de **beyan edildi** | `D-W3b-1` |
| Başlıkları doğrudan (`OnStarting`'siz) yazmak | `UseExceptionHandler` hata yolunda başlıkları **temizler** ⇒ hata yanıtı izolasyonu **düşer** | `IzolasyonBasliklari.cs`:43-45 |
| Başlıkları yalnız `Development`'ta açmak | izolasyon **her ortamda** açıktır (`K159`); ortama bağlı izolasyon, üretimde sessizce kaybolan bir güvenlik özelliğidir | `K159` |
| `wwwroot`'u repoya almak | build çıktısıdır; `CLAUDE.md` kırmızı çizgi 5 | `.gitignore`:31 |

---

## 5. KAPILAR — bu ADR'nin iddialarını hangi KOŞAN kod tutuyor

| kapı | ne ölçer | durum |
|---|---|---|
| `W3b/G48` | `Istemci:KokDizin` **var ve** `"wwwroot"` | 🟢 koşuyor |
| `W3b/G49` | `.gitignore`'da **tam yol** | 🟢 koşuyor |
| `W3b/G50` | build bütünlüğü: `index.html` · `flutter_bootstrap.js` · `canvaskit/*.wasm` · `_BUILD.json`'un `kaynakSha`'sı kapının **kendi hesabıyla TUTUYOR** · `10.0.2.2` **yok** · `wwwroot` yoksa **ORTAM HATASI** | 🟢 koşuyor |
| `W3b/G51` | **bayrak izi**: `useLocalCanvasKit` **`true`** · `canvasKitBaseUrl` çapraz-kökene **atanmamış** (üç yazım) · taban pini · gstatic sayan-raporlayan · pozitif kontrol · ölçülemezse **çıkış ≠ 0** | 🟢 koşuyor |

🔴 **`W3/G43`–`G47` kapılarının implementasyonu YOKTUR** — bu ADR yazılırken ölçüldü:
`grep -rlE 'G4[3-7]' araclar/` → **boş** (`KANIT/W3b/07` §12). Bu ADR **onlara dayanmaz.**

---

## 6. KANONİK SAYILAR — `ADR 0003 §1-K` (`K19-a`) deseni

> 🔴 Bu bölümün **tek kaynağı** `KANIT/W3b/07-ADR0004-OLCUM-TABANI.txt`'tir (8 Ağu 2026, cihazda,
> betikle). Aşağıdaki bir sayı değişirse **bu tablo ve ölçüm tabanı BİRLİKTE** yenilenir; belgenin
> başka hiçbir yerinde bu sayıların kopyası yoktur. *(`K19-a`'nın doğduğu kusur: kardeş kalem
> güncellendi, kendisi unutuldu.)*

| kalem | bayt | satır | sha256/16 |
|---|---|---|---|
| `Web/IzolasyonBasliklari.cs` | 3.107 | 69 | `374e029243e5d1b4` |
| `Web/IstemciServisi.cs` | 6.671 | 128 | `eeddf193542b53b7` |
| `Program.cs` | 10.195 | 209 | `0cd45daf474d7812` |
| `appsettings.json` | 188 | 12 | `1ae6d8a4a9706a75` |
| `araclar/yayin-kapisi.py` | 31.084 | 748 | `5b745d0a6ed3f1de` |
| `araclar/web-yayina-al.py` | 6.679 | 195 | `73eac946ed458808` |

**Sayısal ölçümler:** `Program.cs`'te `IsDevelopment()` → **4** · `wwwroot` üst düzey girdi → **15** ·
`canvaskit/*.wasm` → **4** · `flutter_bootstrap.js` `canvasKitBaseUrl` → **2**, `useLocalCanvasKit` → **2**,
gstatic/flutter-canvaskit → **1** · `flutter.js` gstatic → **1** (toplam **2**, `G51/c` pini) ·
`wwwroot/**` içinde `10.0.2.2` → **0** · CI'da `subosito/flutter-action` → **2**,
`--no-web-resources-cdn` → **0**, `yayin-kapisi` → **0**.

🔴 **`IstemciServisi.cs` için 113 satır / 5.753 b yazan her metin BAYATTIR** — o değerler
`/v{version}` onarımından **öncesine** aittir ve o65 gövdesinin en ağır bulgusu tam buydu.

---

## 7. SONUÇLAR ve BEYAN EDİLMİŞ BEDELLER

1. 🔴 **Göreli kök dizin, çalışma dizinine bağımlılık yaratır.** Sunucu `src/backend/Momentum.Api`
   dışından kaldırılırsa istemci **sunulmaz**; ölçüldü (`GET /` → 404). Bu bedel **kabul edildi**;
   alternatifi (`ContentRootPath`) §4'te ölçülmüş gerekçeyle reddedildi.
2. 🔴 **`ServeUnknownFileTypes = true` ÜRÜNDE CANLIDIR** (`IstemciServisi.cs`:107) ve bu bir
   **beyan edilmiş bedeldir**: uzantısı bilinmeyen dosyalar (ör. `assets/NOTICES`) sunulur.
   *(o65 gövdesi bu satırı yanlışlıkla "reddedildi, koşumda ölü" diye yazmıştı — düzeltildi.)*
3. 🔴 **CORP başlığı bu sunucuda YOKTUR** — `IzolasyonBasliklari.cs`:15 bunu **kendi içinde**
   beyan ediyor. İzolasyon COOP+COEP ile sağlanır; kaynak-tarafı CORP ayrı bir karardır ve
   **bu turda alınmadı.**
4. 🔴 **`_BUILD.json` statik kökte durur ⇒ `/_BUILD.json` olarak FİİLEN SUNULUR** (ölçüldü: 200).
   Build sha'sı, zamanı ve bayrakları **dışarı açılır**. Gizlemek **ayrı bir karardır**, alınmadı.
5. 🔴 **CI bu kararların HİÇBİRİNİ zorlamıyor** — ölçüldü: `.github/workflows/ci.yml`'de
   `--no-web-resources-cdn` **0** kez, `yayin-kapisi` **0** kez geçiyor (Flutter SDK **2** işte kurulu).
   ⇒ `B-O63-2` **AÇIK**; bağlanması `D-A13-4` turuna aittir. Bu ADR o turu **karara bağlamaz**.

---

## 8. NE ÖLÇÜLEMEDİ (boş olamaz)

1. 🔴 **Negatif kontrol `N4`'ün TAM mekanizmasını göstermedi.** `GET /` **404** döndü (bedel gerçek),
   ama patlayan katman `Path.GetFullPath("wwwroot")`'un yanlış çözümü **değil**, content-root'ta
   `appsettings.json`'un bulunamaması ⇒ `Istemci:KokDizin`'in **hiç okunamamasıydı**. Temiz izolasyon
   (`Istemci__KokDizin` ortam değişkeniyle verilir + yanlış CWD) **koşulmadı** ⇒ `B-W3b-6`.
2. 🔴 **`opfsLocks`'un izolasyon açıkken FİİLEN seçildiği ÖLÇÜLMEDİ.** Bu ADR izolasyonun *var
   olduğunu* ölçtü; drift'in depo seçimini **değiştirdiğini ölçmedi**. İskeletin 4. alt sorusu
   (`SharedArrayBuffer` / worker mimarisi) **hâlâ açıktır**.
3. 🔴 **Gerçek `flutter-canvaskit/<engineRevision>/canvaskit.wasm` yolunun CORP'u ölçülmedi** —
   ölçülen, o kökenin **404 yanıtındaki** politika + `fonts.gstatic.com`'un temiz 200'üdür.
4. 🔴 **Tarayıcı ölçümü bu turda YAPILMADI** — `crossOriginIsolated` iddiası **o63'ün** ölçümüdür
   (7 Ağu); bugünkü build üzerinde tekrarlanmadı. Playwright Onur'un makinesinde **yok** (`B-O62-3`).
5. 🔴 **`--no-web-resources-cdn`'in CI'da zorlanması ölçülmedi** çünkü **yok** (§7/5).
6. 🔴 **`G51/b2` yorum satırını gövde kodundan ayırt etmiyor** — `MW23` ölçtü (`B-W3b-7`).
7. 🔴 **`_BUILD.json` CRLF taşıyor** (`web-yayina-al.py` Windows'ta metin modunda yazıyor) — `B-W3b-8`.
8. 🔴 **`KANIT/W3/04`'ün *"21 vaka"* sayısı, sunduğu tabloyla TUTMUYOR — ÇÖZÜLEMEDİ.** Belgenin §2
   başlığı ve §8 koşucu tablosu *"21 vaka"* diyor; §2'nin HTTP tablosunda **16 veri satırı** var
   (bu ADR yazılırken betikle sayıldı; 16'sının 16'sı COOP+COEP taşıyor). Kalan 5 vakanın nerede
   olduğu artefakttan **okunamıyor** — `_http_olc.py` 21 koşup 16'sını mı tablolamış, yoksa sayı mı
   bayat, **ÖLÇÜLEMEDİ**. ⇒ Bu ADR **yalnız tabloda GÖRÜLEN 16'yı** iddia eder.
   🔴 Bu satır bir öz-eleştiridir: gövdenin ilk yazımı *"21/21"*i `DURUM.md`'den **taşımıştı** —
   o65 gövdesini düşüren kusur sınıfının ta kendisi. Doğrulama sırasında yakalandı ve düzeltildi.
   *(`K148-b`: elle sayılan her sayı yanlıştır.)*

---

## 9. KİLİT

🔴 **Bu gövde KİLİTLİ DEĞİLDİR.** `K127`: kilit öncesi **bağımsız** denetim koşar ve kilit
**Onur'dan** gelir (`K40`). Bu belge hiçbir şıkkı **kendi seçmez** ve yürürlükteki hiçbir kilidin
metnini **yeniden yazmaz**.

---

> **ERRATA — 9 Eyl 2026 [o93].** `araclar/yayin-kapisi.py` ve `araclar/web-yayina-al.py`
> `arsiv/araclar/` altına alındı (kapı bütçesi budaması, İŞLEYİŞ md.3). §1, §2, §6 ve §8'deki
> `araclar/...` yolları **o günkü** hâli belgeler. §6 tablosundaki bayt/satır/sha pinleri
> taşımadan **önceki** dosyalara aittir; dosya içerikleri **değişmedi**, yalnız yerleri değişti.
> Bu ADR'nin kararı yürürlüktedir.

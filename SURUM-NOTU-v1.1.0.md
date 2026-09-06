# Momentum v1.1.0 — teslim paketi

> **Bu dosya release gövdesinin TASLAĞIDIR.** `[ÖLÇÜLECEK]` yazan her yer, paket üretildikten sonra
> ölçülen gerçek değerle doldurulur. **Doldurulmamış bir `[ÖLÇÜLECEK]` ile release YAYINLANMAZ.**
> Kural v1.0.1'den devralındı: *aşağıdaki her sayı ölçülmüştür; ölçülmeyen şey "ölçülmedi" diye yazılıdır.*

Çok platformlu görev yönetimi (to-do): Flutter istemci (Android + Web) · N-katmanlı .NET 10 /
ASP.NET Core · PostgreSQL. Vitrin: çevrimdışı-öncelikli senkron, çakışma çözümü ve **çok kullanıcılı
paylaşım**.

Paket iki parçadır: çalışan sistem (docker imajı — API ve web istemcisi aynı kökende) ve Android APK.

## v1.0.1'den farkı — üç dilim

**① Kimlik (19 Ağu).** Hesap açılır, giriş yapılır: `POST /v1/auth/register` · `/login` · `/refresh` ·
`/logout`; `users` + `refresh_tokens` tabloları, parola özeti, JWT bearer (HS256). `ICurrentUser`
**her ortamda önce JWT'den** okur; geçerli jeton yoksa uç **401** döner (deny-by-default korunur —
eski `NullCurrentUser` artık kayıtlı değildir). `Development`'ta `X-Momentum-Dev-User` başlığı
**ikincil** ölçüm yolu olarak kalır. `Jwt:Secret` her ortamda zorunludur; eksikse uygulama sessizce
herkesi 401'e düşürmek yerine **açılışta patlar**.

**② Listeler (20 Ağu).** Görevler listelere ayrılır, görev listeye taşınır; `projectId == null` =
**Gelen Kutusu**; liste silinince görevleri Gelen Kutusu'na **düşer**. Üründe "Liste" denen kap,
kodda ve senkron telinde `Project` entity'sidir.

**③ İşbirliği (4-6 Eyl).** İki kullanıcı bir listeyi paylaşır: e-posta ile davet, `project_members`
OrSet üyeliği, erişim `project_access` görünümünden (sahip ∪ üye). Rol kademesi YOKTUR.
**Canlı vitrin koşuldu (6 Eyl 2026, gerçek telefon + emülatör + JWT'li üçüncü hesap —
`KANIT/o86F`): dokuz adımın DOKUZU da geçti.**

- **Paylaşım üyenin ekranına ≤10 sn'de düştü** — üyede tek bir dokunuş ya da ikinci tetik olmadan
  (Drawer zaten açıktı, yalnız okundu): davet **11:33:51.094**, üyenin Drawer'ında "Proje Y"
  **11:34:01.216** (`KANIT/o86F/IKINCI-TUR-ADIM6.md`, arayüz dökümü
  `ui-T2-B2-04-DAVETTEN-SONRA-10sn.xml`).
- Üye, davete kadar var olmayan geçmişi de görüyor: davetten **önce** yaratılan `gorev-y1`, üyenin
  ekranında beliriyor (katılım-sonrası geçmiş — `IS-EMRI-o86-D2`).
- Üyenin yazdığı görev sahibin ekranında **≤12 sn** içinde, elle yenileme olmadan belirdi.
- **Pozitif kontrol:** davet edilmemiş üçüncü hesabın snapshot'ı **boş** döndü.
- **Negatif dal:** üye paylaşmayı denediğinde *"Bu listeyi paylaşma yetkin yok.
  (RejectedForbidden)"* göründü ve üyelik tablosu değişmedi.

**Birinci tur 8/9 düşmüştü ve düzeltilmeden yazılmadı.** Davet sunucuya işleniyor ve sunucu doğru
cevabı veriyordu (`resyncRequired`), ama istemcinin senkron turu o **boş** yanıtta duruyordu:
imleç siliniyor, ikinci istek bir sonraki dış tetiği bekliyordu ⇒ liste tek başına belirmedi
(8+ dk beklendi). Kök neden ölçüldü, `IS-EMRI-o86-E` ile kapandı
(`devamGerekli = resyncRequired || (hasMore && …)`, tur başına **en fazla bir** resync zinciri) ve
vitrin **taze APK, temiz kurulum ve taze hesap çiftiyle yeniden koşuldu**. Düzeltmeden önce/sonra:
*belirmedi* → **≤10 sn**, gereken ikinci tetik: *A'nın başka bir değişikliği* → **YOK**.

**Ayrıca — paket sürüm damgası düzeltildi.** `v1.0.1` olarak yayınlanan APK'nın manifesti içeride
`versionName 1.0.0` / `versionCode 1` diyordu (ölçüldü: `aapt dump badging`; dosya, release
sayfasındaki sha256 `ee3b4e0b…41746b46` ve 59.953.218 bayt ile birebir). `versionCode` artmadığı
için o paketin v1.0.0 üstüne kurulumu **yükseltme sayılmıyordu**. Bu sürümün APK'sı `1.1.0` / `2`
taşır — bu da APK'nın kendisinden `aapt dump badging` ile okundu (§2 tablosu).

## 1. Tek komutla çalıştır

```
git clone https://github.com/onur-kesim/Momentum.git
cd Momentum
docker compose up --build
```

Sonra tarayıcıda: `http://localhost:5298`

Sıra otomatiktir: postgres → migrator (şemayı EF bundle ile kurar) → api (aynı kökenden web
istemcisini de servis eder). Şemayı api kurmaz; ayrı bir migrator servisi kurar.

⏱ **İlk derleme (soğuk, önbelleksiz): 149 sn.** GitHub `ubuntu-latest` koşucusunda ölçüldü —
`paket #17` run kaydının **kendi adım zaman damgalarından**: `docker compose up --build -d`
`08:40:39Z → 08:43:08Z`. İş akışında hiçbir derleme önbelleği tanımlı değildir ve koşucu
temizdir, yani bu sayı gerçekten soğuktur. Aynı adım v1.0.1'de geliştirme makinesinde
**1639,7 sn** sürmüştü; fark makine ve ağdır — kendi makinenizde bu iki sayı arasında bir yerde
olmasını bekleyin.

🔴 **Yerel soğuk ölçüm bu sürümde YAPILAMADI:** Docker Desktop'ın Linux motoru derlemenin
ortasında düştü (`target migrator: … rpc error: code = Unavailable … EOF`, 218 sn sonra) — makine
belleği yetmedi. Aynı komut sıcak önbellekle **443,6 sn**'de EXIT 0 ile bitti (6 Eyl 2026).

Sağlık ucu: `GET /health/ready` → **200** (paketlenmiş yığında ölçüldü).

Hiçbir şey kurmadan bakmak isterseniz: https://onur-kesim.github.io/Momentum/ — aynı istemci,
ama **backend yoktur**: veriler yalnız tarayıcıda kalır ve yazılan satır kuyrukta *"↑ Gönderiliyor"*da
asılı durur. Senkron, çakışma ve paylaşım vitrini **yalnız** yukarıdaki docker paketinde görülür.
Ölçüldü (6 Eyl 2026): canlı demoda **`crossOriginIsolated === false`** — pakettekinin (`true`)
ölçülmüş karşı-örneğidir; izolasyon bayrağa değil, sunucunun gönderdiği başlıklara bağlıdır.

## 2. Android APK (varlık: `momentum-v1.1.0-emulator.apk`)

| alan | değer |
|---|---|
| Boyut | **60.953.726** bayt |
| sha256 | `57c668b02fd776855c97783fd897dee5e844bddb120afb53e147af67220fb6cf` |
| versionName / versionCode | **1.1.0 / 2** (`aapt dump badging` ile APK'dan okundu) |
| ABI | `arm64-v8a` · `armeabi-v7a` · `x86_64` (tek fat APK, `aapt` ile teyit) |
| Uygulama adı | `Momentum` |
| Derleme hedefi | `SENKRON_SUNUCU_URL=http://10.0.2.2:5298` |
| Uygulama kimliği | `com.momentum.client` · `minSdk 24` · `targetSdk 36` |
| Derlendiği ağaç | Etiketlenen commit'in `src/`'si — derleme anında `git status --porcelain -- src` **boştu** |

🟢 **APK bu ağaçta yeniden derlendi ve BAYT BAYT AYNI çıktı.** `03690c2`'de üretilen dosya ile
teslim commit'inin ağacında yeniden derlenen dosyanın sha256'ları **birebir aynı**
(`57c668b0…20fb6cf`, 60.953.726 bayt). Aradaki commit'ler yalnız belge değiştirdiği için ürün biti
değişmiyor; yani yukarıdaki sha256, etiketlenen kaynağın ürünüdür.

🔴 **Derleme hedefi VARSAYILMADI, APK'nın içinden ölçüldü:** üç ABI'nin `libapp.so`'sunda
`http://10.0.2.2:5298` dizesi dörder kez geçiyor, `http://localhost:5298` **hiç geçmiyor**.
(Bu ölçüm gereksiz değildi: aynı kaynaktan `localhost` hedefiyle derlenen vitrin APK'sı **bayta
kadar aynı boyutta** çıkmıştı — boyut karşılaştırması tek başına yanıltıcıdır.)

🟢 **`DEV_USER_ID` artık GEREKMİYOR — v1.0.1'in talimatı bu sürümde geçersizdir.** v1.0.1'de iki
istemcinin birbirini görmesi tek bir sabit demo kimliğine bağlıydı. Kimlik dilimi geldikten sonra
her istemci **kendi hesabını açar ve giriş yapar**; kimlik JWT ile taşınır.

Ölçüldü (6 Eyl 2026, paketlenmiş yığın ayaktayken — `KANIT/o86F`): `DEV_USER_ID` define'ı
**VERİLMEDEN** derlenen tek bir APK, iki ayrı cihazda iki **ayrı** hesapla (`a@` ve `b@`) sorunsuz
çalıştı. Üçüncü bir hesap `POST /v1/auth/register` ile açıldı (**201**, gövdede `accessToken`) ve
`Authorization: Bearer <jeton>` ile çektiği snapshot **`[]`** döndü — yani yetkilendirme jetondan
geliyor, sabit kimlikten değil. `X-Momentum-Dev-User` yalnızca `Development` profilinde **ikincil**
ölçüm yolu olarak durur; değerlendiricinin ona ihtiyacı yoktur.

🔴 **Aynısı paketin WEB istemcisinde de ölçüldü ve orada daha da nettir.** `docker-compose.yml`
hâlâ `DEV_USER_ID=deadbeef-…-000000000001` yapı argümanını veriyor ve `Dockerfile:100` bunu
`--dart-define` olarak web derlemesine geçiriyor; **buna rağmen** paketin servis ettiği
`main.dart.js` (3.552.484 bayt) içinde bu GUID **hiç geçmiyor** — `deadbeef` dizesi **0 kez**
(`flutter.js`, `flutter_bootstrap.js`, `index.html`'de de 0). **Pozitif kontrol aynı dosyada:**
öteki define'ın değeri (`localhost:5298`) **4 kez** geçiyor, yani tarama kör değil ve define'lar
çıktıya iniyor — inmeyen yalnız bu. Aynı dosyada `Authorization` **2**, `Bearer` **2**,
`/v1/auth/` **4** kez geçiyor. Sabit demo kimliği web istemcisinde **ölü koddur**.

🔴 **Gerçek telefonda bu APK çalışmaz.** Telefon `10.0.2.2`'ye ulaşamaz; kendi ağınız için yeniden
derlemeniz gerekir (`src/client` dizininden):

```
flutter build apk --release --dart-define=SENKRON_SUNUCU_URL=http://<backend-LAN-IP>:5298
```

`localhost` yazmayın — telefon `localhost` dediğinde kendini kasteder.

🔴 **APK debug anahtarıyla imzalıdır.** `android/app/build.gradle.kts` içinde Flutter'ın varsayılan
`signingConfig = signingConfigs.getByName("debug")` satırı duruyor; üretim imza zinciri kurulmadı.
Kurulumda "bilinmeyen kaynak" onayı isteyecektir. Gözden kaçma değil, **yazılı kapsam kararıdır**.
İmza bloğu bu pakette de söküldü (`apksigner verify --print-certs`): tek imzacı,
**`C=US, O=Android, CN=Android Debug`**, sertifika SHA-256 `ee35229e…0d47b45`.

## 3. Ne ölçüldü

- İstemci testleri: **767** yeşil (`flutter test`, EXIT **0**, 128,4 sn) · `flutter analyze`
  **0** uyarı (*"No issues found!"*, 12,9 sn) — 6 Eyl 2026, `src/client` dizininden koşuldu
- Backend testleri: **177** başarılı / **0** başarısız / **0** atlanan (mimari 6 · SyncCore 44 ·
  Api 22 · kalıcılık 105); `verify` zinciri EXIT **0** (240,7 sn) — `build -warnaserror`
  **0 uyarı, 0 hata** · CVE kapısı **0 zafiyetli paket**
- CI kapıları: **`ci` · `paket` · `pages` — üçü de etiketlenen commit'te yeşil.** Ölçülmüş sha'lar
  **release sayfasının gövdesindedir**; bu dosya kendi commit'inin sha'sını içeremeyeceği için
  sayılar orada durur, burada kural durur.
  🔴 Kural: üçü **aynı sha'da** yeşil olmadan release YAYINLANMAZ; sha'lar run kayıtlarının
  KENDİ sayfasından okunur (liste satırından DEĞİL — o81 dersi). `pages` yalnız
  `workflow_dispatch` ile koşar ⇒ **her yeni commit'ten sonra elle tetiklenir.**
- **`crossOriginIsolated === true`**, `SharedArrayBuffer` kullanılabilir — paketlenmiş web
  istemcisinde tarayıcıdan ölçüldü (6 Eyl 2026). İzolasyon başlıkları belgeye iniyor:
  `Cross-Origin-Opener-Policy: same-origin` · `Cross-Origin-Embedder-Policy: require-corp` ·
  `Cross-Origin-Resource-Policy: same-origin`.
- **Drift kalıcılık kipi: OPFS — yani KALICI, geri düşüş değil.** İki uçtan birden ölçüldü
  (6 Eyl 2026, giriş yapılmış oturum): tarayıcıda OPFS kökünde **`drift_db/momentum/database`**
  duruyor ve IndexedDB **boş**; **ürün ucunda** ise depolama şeridi **çizilmiyor** — şerit yalnız
  geri düşüşte çizilir (`w2_depolama_seridi_test` G40/a: `kaliciOpfs ⇒ YOK`, `geriDüşüş ⇒ VAR`).
  Şeridin yokluğu ile OPFS dosyasının varlığı aynı sonucu veriyor.
- Çalışma imajı pini: **`Microsoft.AspNetCore.App 10.0.11`** · **`Microsoft.NETCore.App 10.0.11`**
  — koşan `momentum-api` konteynerinden `dotnet --list-runtimes` ile okundu (yüzen `10.0` etiketi
  değil, sabitlenmiş yama sürümü)
- Çok kullanıcılı paylaşım canlı turu (6 Eyl 2026, `KANIT/o86F`): gerçek telefon + emülatör +
  JWT'li üçüncü hesap · **9/9 GEÇTİ** · paylaşım üyenin ekranına **≤10 sn**'de, ikinci tetik
  olmadan düştü · katılım-sonrası geçmiş üye ekranında doğrulandı · üyeden sahibe yayılma
  **≤12 sn** · davetsiz hesabın snapshot'ı **boş** · üyenin paylaşma denemesi
  *"yetkin yok (RejectedForbidden)"* ile reddedildi
- Backend hazırlık dikişleri (paketlenmiş yığın ayaktayken, 6 Eyl 2026): `momentum-migrator`
  **exited 0** · `/health/ready` **200** · `POST /v1/sync` başlıksız **401**,
  `X-Momentum-Dev-User` başlığıyla **200** (varsayılan-ret ayakta)

## 🔴 NE ÖLÇÜLMEDİ

- **Gerçek arm64 donanımda paket kapısı koşulmadı** (kırılma yalnız manifestle gösterildi; arm64
  makine yok) — v1.0.1'den devreden açık bulgu.
- **Erişilebilirlik duyuruları gerçek ekran okuyucuyla (TalkBack) dinlenmedi.**
- **iOS hiçbir cihazda koşmadı** (Mac donanımı yok; yalnız CI'da derlenir).
- **İşbirliği dilimi 6 Eyl'de gerçek cihazlarda koşuldu** (`KANIT/o86F`) ⇒ bu madde DARALDI.
  **Liste dilimi** için canlı tur yalnız kısmen yapıldı: liste yaratma, listeye girme ve göreve
  ekleme gerçek arayüzde ölçüldü, ama listenin **silinmesi** ve görevlerin Gelen Kutusu'na düşmesi,
  görevin listeler arası **taşınması** canlı koşulmadı — bunlar hâlâ yalnız ekran widget
  testleriyle ölçülüdür.
- 🔴 **Yerel soğuk derleme süresi ölçülemedi:** Docker Desktop'ın Linux motoru derlemenin ortasında
  düştü (bellek). Soğuk sayı temiz CI koşucusundan alındı (§1); geliştirme makinesinde yalnız sıcak
  koşum ölçülebildi.
- **Drift kalıcılık kipi tarayıcı-üstü genellenmedi:** OPFS ölçümü tek bir Chrome oturumunda
  yapıldı; Firefox/Safari'de OPFS eşiği farklıdır ve o tarayıcılarda geri düşüş **ölçülmedi**.

## 4. Kapsam dışı — eksik değil, kesilmiş

Ödevin kapsam otoritesi `docs/ODEV.md`; kesilenler README ve `CLAUDE.md` §5'te de yazılıdır.

- **Tekrar eden görev (RRULE)** — **[5 Eyl 2026 KESİLDİ]**, takvim kutusu gereği; kesme sırası
  18 Ağu'da kilitliydi (hatırlatıcı → tekrar) ve bununla tükendi.
- **Hatırlatıcı / bildirim** — **[4 Eyl 2026 KESİLDİ]**, aynı gerekçe.
- **Proje/liste klasörü** (üst kap) — [o85 KESİLDİ].
- **Parola sıfırlama · e-posta doğrulama · OAuth · 2FA · RBAC** — kimlik dilimi bilerek incedir.
- **Windows masaüstü `.exe` yok** — `src/client/` yalnız `android`, `ios`, `web` taşır; Windows'ta
  uygulama tarayıcıdan çalışır, aynı tek komut.
- **iOS cihazda koşmadı** (Mac donanımı yok).
- Doğal dil ayrıştırıcısı her şeyi yutarsa (`#iş !p1 yarın`) hata metni gösterilmez; metin alanda
  kalır. Sessiz kayıp yok, geri bildirim de yok.

## 5. Mimari nereden okunur

`README.md` — zorunlu şartlar, ölçülmüş sınırlar, beyan edilmiş kısıtlar · `docs/ODEV.md` — kapsam
otoritesi · `docs/ADR/` — karar kayıtları · `src/backend/` dört katman (Domain · Application ·
Infrastructure · Api), testler `tests/` altında · `src/client/lib/` — sunum · vitrin · veri ayrımı ·
`KANIT/` — **1622** izlenen dosya, ham ölçüm; **düşmüş denetimler dahil, temizlenmemiş**.

---

# ⚠ AŞAĞISI RELEASE GÖVDESİNE **GİRMEZ** — hazırlık adımları (5 Eyl 2026'da ölçüldü)

## H1 🟢 KAPANDI (6 Eyl) — APK'DAN ÖLÇÜLDÜ

`src/client/pubspec.yaml:19` → **`version: 1.1.0+2`** (önceki: `1.0.0+1`), commit `1f247df`.
Release APK'sından `aapt dump badging` ile okundu: **`versionCode='2' versionName='1.1.0'`** ✅
`local.properties` git'te izlenmiyor, Flutter yeniden üretir — elle dokunulmadı.

🔴 **Bu varsayımsal bir risk değildi: BİR KEZ ZATEN KAPIDAN ÇIKTI.** Ölçüldü (6 Eyl 2026):
yayınlanmış `momentum-v1.0.1-emulator.apk` — sha256 `ee3b4e0b…41746b46`, 59.953.218 bayt, release
sayfasındaki değerlerle **birebir** — `aapt dump badging` altında **`versionName='1.0.0'`
`versionCode='1'`** diyor. Yani *"v1.0.1"* diye indirilen paketin içi 1.0.0'dır ve `versionCode`
artmadığı için v1.0.0 üstüne kurulum **yükseltme sayılmaz**. v1.1.0 bunu kapatır.

## H2 🟡 APK DERLENDİ — release'e YÜKLENMEDİ

Ölçülen artefakt `src/client/build/app/outputs/flutter-apk/momentum-v1.1.0-emulator.apk`
(kopya sha256 ile doğrulandı, §2'deki değerlerle birebir). **Kalan:** release oluşturulup bu
varlığın yüklenmesi — ikisi de Onur'da.

### (kapanmış kayıt) `paket.yml` APK ÜRETMİYOR — release varlığı ELLE derlenir

Ölçüldü: `.github/workflows/paket.yml` yalnız `docker compose up` tek-komut kapısını koşuyor
(5 ayak: istemci varlıkları · COOP/COEP · 401→200 + register/token · ürün ucu · 404). İçinde
`flutter build apk` **yok**, artefakt yüklemesi **yok**. Yani APK'yı CI vermez; `src/client`
dizininden elle derlenir ve release'e elle yüklenir:

    C:\src\flutter\bin\flutter.bat build apk --release --dart-define=SENKRON_SUNUCU_URL=http://10.0.2.2:5298

Sonra boyut + `sha256` ölçülüp §2 tablosuna yazılır.

## H3 🟢 KAPANDI (6 Eyl 2026) — `DEV_USER_ID` anlatısı ölçüldü ve §2'ye yazıldı

Sonuç: define **gerekmiyor**, kimlik gerçek JWT. Ayrıntı ve ham ölçüm §2'de ve `KANIT/o86F`'de.
Aşağıdaki asıl soru kaydı, nasıl ölçüldüğü görülsün diye bırakıldı.

### (kapanmış kayıt) `DEV_USER_ID` anlatısı v1.0.1'den DEVRALINAMAZ

`docker-compose.yml:31` hâlâ `DEV_USER_ID: deadbeef-0000-4000-8000-000000000001` ve
`ASPNETCORE_ENVIRONMENT: Development` (satır 80). v1.0.1'in release notu *"iki istemcinin
birbirini görmesi için ikisi de AYNI sabit demo kimliğini kullanır"* diyordu. **Kimlik dilimi
geldikten sonra bu anlatı ya yanlış ya eksiktir** ve paylaşım vitrini iki **AYRI** hesap ister —
tek sabit kimlik onu imkânsız kılar.

Ölçülecek (paket ayaktayken, `POST /v1/sync` **başlıklarından**, varsayarak değil):
1. Paketlenmiş web istemcisi `Authorization: Bearer` mı gönderiyor, `X-Momentum-Dev-User` mı?
2. İki ayrı hesapla açılan iki tarayıcı oturumu birbirinden **izole** mi?
3. `DEV_USER_ID` define'ı paket demosunda hâlâ bir işe yarıyor mu, yoksa ölü mü?

Cevaba göre §2'deki "Demo kimliği" satırı ya **silinir** ya **yeniden yazılır**. `IS-EMRI-o86-B`
§F/3'ün uyarısı birebir budur.

## H4 · Sıra

**BİTTİ:** o86-D kabul → §F vitrin 9/9 → H1 → H3 → H2 (APK derlendi) → §3'ün ölçülebilir satırları
dolduruldu → README "Teslim paketi" + "Kimlik" bölümleri v1.1.0'a çevrildi.

**KALAN — sıra bu, çünkü tetikler asimetriktir:**

1. **Teslim commit'i (C):** README · SURUM-NOTU · DURUM · `KANIT/o83D` · (Claude Code'un
   `docker-compose.yml` yorumu). **Push Onur'da.**
2. **APK'yı C'de yeniden derle** (`_o90_apk_release.cmd`), `aapt dump badging` + `sha256` ile ölç,
   §2 tablosunu ve README'yi güncelle → **damga commit'i (D)**, push Onur'da.
   🔴 D'nin ardından **`git diff --stat C..D -- src Dockerfile docker-compose.yml global.json`
   BOŞ olmalı** — boşsa APK, etiketlenen ağacın ürünüdür ve bu cümle sürüm notuna yazılır.
3. **Kapılar D'de:** `ci` push'ta kendiliğinden koşar; **`paket` ve `pages` ELLE tetiklenir**
   (`gh workflow run paket.yml --ref main` · `pages.yml`). `paket`, yalnız-belge commit'inde
   yol süzgecine takılmaz çünkü elle tetiklenir.
4. Üç run kaydının **kendi sayfasından** sha'ları oku, §3'teki CI satırını doldur.
5. `git tag v1.1.0` + release + APK varlığını yükle — **hepsi Onur'da.**

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
`KANIT/o86F`): dokuz adımın SEKİZİ geçti.**

- Üye, davete kadar var olmayan geçmişi de görüyor: davetten **önce** yaratılan `gorev-1`, üyenin
  ekranında beliriyor (katılım-sonrası geçmiş — `IS-EMRI-o86-D2`).
- Üyenin yazdığı görev sahibin ekranında **≤12 sn** içinde, elle yenileme olmadan belirdi.
- **Pozitif kontrol:** davet edilmemiş üçüncü hesabın snapshot'ı **boş** döndü.
- **Negatif dal:** üye paylaşmayı denediğinde *"Bu listeyi paylaşma yetkin yok.
  (RejectedForbidden)"* göründü ve üyelik tablosu değişmedi.

🔴 **Tek eksik — paylaşımın üyenin ekranına DÜŞME ANI.** Davet sunucuya işlenir ve sunucu doğru
cevabı verir (`resyncRequired`), sinyal de üyeye ulaşır; ancak istemcinin senkron turu o boş
yanıtta durduğu için liste **bir sonraki senkron tetiğini** bekler (ölçülen gecikme: sonraki
değişiklikle birlikte 12 sn). Düzeltme `IS-EMRI-o86-E`. **Bu madde düzeltilip vitrin yeniden
koşulmadan release'e "anında belirir" diye YAZILMAZ.**

**Ayrıca — paket sürüm damgası düzeltildi.** `v1.0.1` olarak yayınlanan APK'nın manifesti içeride
`versionName 1.0.0` / `versionCode 1` diyordu (ölçüldü: `aapt dump badging`; dosya, release
sayfasındaki sha256 `ee3b4e0b…41746b46` ve 59.953.218 bayt ile birebir). `versionCode` artmadığı
için o paketin v1.0.0 üstüne kurulumu **yükseltme sayılmıyordu**. Bu sürümün APK'sı `1.1.0` / `2`
taşır. *(Bu satır, APK derlenip `aapt` ile ölçüldükten sonra kesinleşir.)*

## 1. Tek komutla çalıştır

```
git clone https://github.com/tuzakavcisi1-cloud/Momentum.git
cd Momentum
docker compose up --build
```

Sonra tarayıcıda: `http://localhost:5298`

Sıra otomatiktir: postgres → migrator (şemayı EF bundle ile kurar) → api (aynı kökenden web
istemcisini de servis eder). Şemayı api kurmaz; ayrı bir migrator servisi kurar.

⏱ İlk derleme `[ÖLÇÜLECEK — v1.0.1'de 1639,7 sn / ~27 dk; soğuk önbellekle yeniden ölçülür]`.
Sağlık ucu: `GET /health/ready` → 200.

Hiçbir şey kurmadan bakmak isterseniz: https://tuzakavcisi1-cloud.github.io/Momentum/ — aynı istemci,
ama **backend yoktur**: veriler yalnız tarayıcıda kalır ve yazılan satır kuyrukta *"↑ Gönderiliyor"*da
asılı durur. Senkron, çakışma ve paylaşım vitrini **yalnız** yukarıdaki docker paketinde görülür.

## 2. Android APK (varlık: `[ÖLÇÜLECEK — momentum-v1.1.0-emulator.apk]`)

| alan | değer |
|---|---|
| Boyut | `[ÖLÇÜLECEK]` bayt |
| sha256 | `[ÖLÇÜLECEK]` |
| ABI | armeabi-v7a · arm64-v8a · x86_64 (tek fat APK) — `[ÖLÇÜLECEK: aapt ile teyit]` |
| Derleme hedefi | `SENKRON_SUNUCU_URL=http://10.0.2.2:5298` |
| Derlendiği commit | `[ÖLÇÜLECEK — etiketin ta kendisi olmalı]` |

🟢 **`DEV_USER_ID` artık GEREKMİYOR — v1.0.1'in talimatı bu sürümde geçersizdir.** v1.0.1'de iki
istemcinin birbirini görmesi tek bir sabit demo kimliğine bağlıydı. Kimlik dilimi geldikten sonra
her istemci **kendi hesabını açar ve giriş yapar**; kimlik JWT ile taşınır.

Ölçüldü (6 Eyl 2026, paketlenmiş yığın ayaktayken — `KANIT/o86F`): `DEV_USER_ID` define'ı
**VERİLMEDEN** derlenen tek bir APK, iki ayrı cihazda iki **ayrı** hesapla (`a@` ve `b@`) sorunsuz
çalıştı. Üçüncü bir hesap `POST /v1/auth/register` ile açıldı (**201**, gövdede `accessToken`) ve
`Authorization: Bearer <jeton>` ile çektiği snapshot **`[]`** döndü — yani yetkilendirme jetondan
geliyor, sabit kimlikten değil. `X-Momentum-Dev-User` yalnızca `Development` profilinde **ikincil**
ölçüm yolu olarak durur; değerlendiricinin ona ihtiyacı yoktur.

🔴 **Gerçek telefonda bu APK çalışmaz.** Telefon `10.0.2.2`'ye ulaşamaz; kendi ağınız için yeniden
derlemeniz gerekir (`src/client` dizininden):

```
flutter build apk --release --dart-define=SENKRON_SUNUCU_URL=http://<backend-LAN-IP>:5298
```

`localhost` yazmayın — telefon `localhost` dediğinde kendini kasteder.

🔴 **APK debug anahtarıyla imzalıdır.** `android/app/build.gradle.kts` içinde Flutter'ın varsayılan
`signingConfig = signingConfigs.getByName("debug")` satırı duruyor; üretim imza zinciri kurulmadı.
Kurulumda "bilinmeyen kaynak" onayı isteyecektir. Gözden kaçma değil, **yazılı kapsam kararıdır**.
`[ÖLÇÜLECEK: imza bloğu bu pakette de yeniden sökülür]`

## 3. Ne ölçüldü

`[ÖLÇÜLECEK — hepsi paket üretildikten SONRA doldurulur]`

- İstemci testleri: `[ÖLÇÜLECEK]` yeşil · `flutter analyze` `[ÖLÇÜLECEK]` uyarı
- Backend testleri: `[ÖLÇÜLECEK]` (`verify` zinciri EXIT `[ÖLÇÜLECEK]`)
- CI kapıları: `ci #[ÖLÇÜLECEK]` · `paket #[ÖLÇÜLECEK]` · `pages #[ÖLÇÜLECEK]` — **üçü de aynı sha**
  olmalı ve **her sha run kaydının KENDİ sayfasından** okunur, liste satırından DEĞİL (o81 dersi)
- `crossOriginIsolated = true` · Drift kalıcılık kipi `[ÖLÇÜLECEK]`
- Çalışma imajı pini: `[ÖLÇÜLECEK — koşan konteynerden dotnet --list-runtimes]`
- Çok kullanıcılı paylaşım canlı turu (6 Eyl 2026, `KANIT/o86F`): gerçek telefon + emülatör +
  JWT'li üçüncü hesap · **9 adımın 8'i geçti** · katılım-sonrası geçmiş üye ekranında doğrulandı ·
  üyeden sahibe yayılma **≤12 sn** · davetsiz hesabın snapshot'ı **boş** · üyenin paylaşma denemesi
  *"yetkin yok (RejectedForbidden)"* ile reddedildi. Kalan: paylaşımın üye ekranına düşme anı
  (`IS-EMRI-o86-E`) — **vitrin yeniden koşulunca bu satır tazelenir**
- Backend hazırlık dikişleri (aynı tur): `momentum-migrator` exited 0 · `/health/ready` **200** ·
  `POST /v1/sync` başlıksız **401**

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
- `[ÖLÇÜLECEK — paket turunda çıkan yeni ölçülemezler buraya]`

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
`KANIT/` — `[ÖLÇÜLECEK]` izlenen dosya, ham ölçüm; **düşmüş denetimler dahil, temizlenmemiş**.

---

# ⚠ AŞAĞISI RELEASE GÖVDESİNE **GİRMEZ** — hazırlık adımları (5 Eyl 2026'da ölçüldü)

## H1 🟡 KAYNAKTA DÜZELTİLDİ (6 Eyl) — APK'da ÖLÇÜLMEDİ

`src/client/pubspec.yaml:19` → **`version: 1.1.0+2`** yapıldı (önceki: `1.0.0+1`).
`local.properties` git'te izlenmiyor, Flutter yeniden üretir — elle dokunulmaz.

🔴 **Bu varsayımsal bir risk değildi: BİR KEZ ZATEN KAPIDAN ÇIKTI.** Ölçüldü (6 Eyl 2026):
yayınlanmış `momentum-v1.0.1-emulator.apk` — sha256 `ee3b4e0b…41746b46`, 59.953.218 bayt, release
sayfasındaki değerlerle **birebir** — `aapt dump badging` altında **`versionName='1.0.0'`
`versionCode='1'`** diyor. Yani *"v1.0.1"* diye indirilen paketin içi 1.0.0'dır ve `versionCode`
artmadığı için v1.0.0 üstüne kurulum **yükseltme sayılmaz**. v1.1.0 bunu kapatır.

⚠ **HÂLÂ AÇIK:** düzeltme yalnız KAYNAKTA. Release APK'sı derlendikten sonra
`aapt dump badging` ile `versionName=1.1.0` / `versionCode=2` **ÖLÇÜLECEK** — yazılmış olması
indiği anlamına gelmez (kör kapı dersi).

## H2 🔴 `paket.yml` APK ÜRETMİYOR — release varlığı ELLE derlenir

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

o86-D kabul → §F vitrin (adım 3-9, **8 ve 9 dahil**) → H1 → H3 ölçümü → H2 (APK) →
§3 ve "NE ÖLÇÜLMEDİ" doldurulur → `git tag v1.1.0` + release (**push ve yayın Onur'da**) →
`pages` elle tetiklenir → README'nin "Teslim paketi" bölümü v1.1.0'a çevrilir.

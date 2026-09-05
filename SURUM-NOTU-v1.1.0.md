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
🔴 **[VİTRİN ŞARTA BAĞLI]** Bu maddenin release'e "kanıtlandı" diye girebilmesi için `IS-EMRI-o86-B`
§F'nin **6, 7, 8 ve 9. adımlarının** kanıtı `KANIT/o86B/` altında bulunmalıdır (8 = davet edilmemiş
üçüncü hesabın projeyi GÖRMEMESİ — pozitif kontrol; 9 = üyenin paylaşamaması). Kanıt yoksa bu madde
**"kod ve testlerde var, canlı vitrini koşulmadı"** diye yazılır. Boş liste her iddiayı geçirir.

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

🔴 **Kimlik artık gerçek giriştir; `DEV_USER_ID` uyarısı DEĞİŞTİ.** v1.0.1'de iki istemcinin
birbirini görmesi sabit bir demo kimliğine bağlıydı. Bu sürümde hesap açılıp giriş yapılır.
`[ÖLÇÜLECEK]` — APK'nın `DEV_USER_ID` define'ıyla mı yoksa define'sız mı derlendiği ve
`X-Momentum-Dev-User` yolunun bu pakette **açık mı kapalı mı** olduğu, `POST /v1/sync`
**başlıklarından ölçülüp** buraya yazılır (sınır 29 + `IS-EMRI-o86-B` §F/3 uyarısı). Varsayılmaz.

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
- Çok kullanıcılı paylaşım canlı turu: `[ÖLÇÜLECEK — §F 6/7/8/9, KANIT/o86B]`

## 🔴 NE ÖLÇÜLMEDİ

- **Gerçek arm64 donanımda paket kapısı koşulmadı** (kırılma yalnız manifestle gösterildi; arm64
  makine yok) — v1.0.1'den devreden açık bulgu.
- **Erişilebilirlik duyuruları gerçek ekran okuyucuyla (TalkBack) dinlenmedi.**
- **iOS hiçbir cihazda koşmadı** (Mac donanımı yok; yalnız CI'da derlenir).
- **Liste ve işbirliği dilimlerinin canlı ölçümü protokol seviyesindedir** (`/v1/sync` HTTP betiği);
  Flutter UI'ın kendisi bu iki dilim için ekran widget testleriyle ölçüldü. `[§F vitrini koşarsa bu
  madde DARALIR — koşmazsa OLDUĞU GİBİ KALIR.]`
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

## H1 🔴 SÜRÜM NUMARASI HÂLÂ 1.0.0 — sessiz kör kapı

- `src/client/pubspec.yaml:19` → `version: 1.0.0+1`
- `src/client/android/local.properties` → `flutter.versionName=1.0.0` · `flutter.versionCode=1`

Bu hâliyle derlenirse **release "v1.1.0" der, APK'nın içi "1.0.0" der** ve `versionCode`
artmadığı için cihaza kurulum eski sürümün üstüne beklenmedik biçimde oturur. Etiketten ÖNCE
`pubspec.yaml` **`1.1.0+2`** yapılır; `local.properties` Flutter tarafından yeniden üretilir ama
**derleme sonrası APK'dan `aapt dump badging` ile versionName/versionCode ÖLÇÜLÜR**, varsayılmaz.

## H2 🔴 `paket.yml` APK ÜRETMİYOR — release varlığı ELLE derlenir

Ölçüldü: `.github/workflows/paket.yml` yalnız `docker compose up` tek-komut kapısını koşuyor
(5 ayak: istemci varlıkları · COOP/COEP · 401→200 + register/token · ürün ucu · 404). İçinde
`flutter build apk` **yok**, artefakt yüklemesi **yok**. Yani APK'yı CI vermez; `src/client`
dizininden elle derlenir ve release'e elle yüklenir:

    C:\src\flutter\bin\flutter.bat build apk --release --dart-define=SENKRON_SUNUCU_URL=http://10.0.2.2:5298

Sonra boyut + `sha256` ölçülüp §2 tablosuna yazılır.

## H3 🔴 `DEV_USER_ID` anlatısı v1.0.1'den DEVRALINAMAZ — ölçülmeden yazılmaz

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

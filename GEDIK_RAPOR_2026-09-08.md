# GEDİK RAPORU — Momentum · 8 Eyl 2026

**Hedef:** `C:\dev\Momentum`, HEAD `4e4292c`, ağaç temiz.
**Koşum:** 8 Eyl 2026 23:50 (cihaz yerel, UTC+03:00 — `Türkiye Yaz Saati`).
**Kapsam:** kaynak kod, yapılandırma, iş akışları, git geçmişi. Salt-okunur; düzeltme yapılmadı.
**Hüküm: 6 gedik açık** — 1 YÜKSEK · 2 ORTA · 2 DÜŞÜK · 1 BİLGİ. **KRİTİK yok.**

> Bu raporun kendi tarayıcıları K4 (kör kapı) protokolünden geçti: §6.

---

## 0. Triyaj — ölçüldü, sorulmadı

| # | Soru | Cevap | Kanıt |
|---|---|---|---|
| T1 | Ağ var mı | **EVET** | `http.` 36 · SignalR 28 · WebSocket 27 |
| T2 | Sunucu kodu | **EVET** | `Map{Post,Get,Put,Delete}` 11 uç · Minimal API |
| T3 | Veritabanı | **EVET** | Npgsql 72 · EF Core · 8 migration · `project_access` görünümü |
| T4 | Kimlik | **EVET** | JWT bearer · `X-Momentum-Dev-User` (yalnız Development) |
| T5 | Güvenilmeyen girdi | sync gövdesi, cursor, JWT, SignalR, `Uri.parse` 17 | envanter §2 |
| T6 | Sır malzemesi | git geçmişinde `.env/.jks/.pem` **YOK**; iki belgeli demo değeri var | G-5 |
| T7 | BaaS / RLS | **HAYIR** | `supabase\|firebase\|appwrite\|pocketbase` → 0 eşleşme |
| T8 | Yapay zekâ / LLM | **HAYIR** | `openai\|anthropic\|langchain\|ollama` → 0 eşleşme |

**Şerit A** (istemci) + **Şerit B** (sunucu/API/DB/kimlik) + kod incelemesi + CI/depo koştu.
**Şerit C (BaaS/RLS) ve Şerit D (LLM): mimari olarak yok** — madde madde N/A listesi yazılmadı.

---

## 1. GEDİKLER

### G-1 · YÜKSEK — Hız sınırı ve hesap kilidi YOK

**Nerede:** `src/backend/Momentum.Api/Program.cs` (tüm ardışık hat) ·
`Endpoints/AuthEndpoints.cs` → `/v1/auth/login`, `/v1/auth/register`, `/v1/auth/refresh`

**Ölçüm:** `src/backend` altında `AddRateLimiter` · `UseRateLimiter` · `RateLimiter` ·
`FixedWindow` · `AccessFailedCount` · `MaxFailedAccessAttempts` · `Lockout` desenleri
**sıfır** eşleşme verdi. `deneme|attempt` eşleşmelerinin tamamı outbox teslim denemesi
(`OutboxDispatcher`, `attempt_count`) — kimlik denemesi değil.

**Etki:** kimlik doğrulama uçları sınırsız. Parola deneme (brute-force), sızmış parola
listesiyle doldurma (credential stuffing), kayıt sırasında hesap/e-posta sayımı
(enumeration) ve kayıt spam'i hiçbir mekanik engelle karşılaşmaz. `AspNetPasswordHasher`
(PBKDF2) her denemeyi pahalı kıldığı için aynı uç aynı zamanda **CPU tüketim DoS**
yüzeyidir: saldırganın maliyeti bir HTTP isteği, sunucunun maliyeti bir KDF turu.

**Tekrar üretim (kavramsal, canlıya koşulmadı):** aynı e-postaya `/v1/auth/login`
istekleri, aralarında gecikme olmadan; her yanıt 401 döner, **hiçbir yanıt 429 dönmez**
ve N'inci denemeden sonra hesap kilitlenmez.

**Yama:** .NET yerleşik `AddRateLimiter` + `UseRateLimiter`; kimlik uçlarına IP+kullanıcı
anahtarlı sabit/kayan pencere (ör. 5 deneme / 5 dk), aşımda **429 + `Retry-After`**.
Ayrıca `users` tablosunda başarısız deneme sayacı ve geçici kilit. Kapı, mutantla
kanıtlanmadan yeşil sayılmaz: 6. istek 429 dönmeli, 5. dönmemeli.

---

### G-2 · ORTA — OpenAPI belgesi ve Scalar arayüzü HER ortamda açık

**Nerede:** `Program.cs:239-240`

```
app.MapOpenApi();            // -> /openapi/v1.json
app.MapScalarApiReference(); // -> /scalar/v1
```

**Ölçüm:** iki satır da hiçbir `IsDevelopment()` bloğunun içinde değil. Dosyadaki
`IsDevelopment()` blokları 58 (dev kimlik kalkanı), 147 ve 181 (CORS) satırlarında;
239-240 hiçbirinin kapsamında değil.

**Kusurun kökü bayat bir gerekçe:** hemen üstündeki yorum *"mapped in every environment
(no auth in this slice -> no leak)"* diyor. O cümle yazıldığında kimlik dilimi yoktu.
`IS-EMRI-o83` ile JWT, kullanıcılar ve refresh token'lar geldi; **"sızıntı yok" gerekçesi
o anda düştü, satır kalmaya devam etti.**

**Etki:** kimlik doğrulamalı uçların tam şeması, alan adları, sürüm bilgisi ve örnek
gövdeleri anonim olarak okunabilir. Tek başına yetki atlatmaz; keşif maliyetini düşürür
ve G-1 ile birleşince hangi ucun dövüleceğini hazır sunar.

**Yama:** iki satırı `if (app.Environment.IsDevelopment())` içine al; üretimde
gerekiyorsa kimlik doğrulamasının arkasına koy. Yorumu da düzelt — düşmüş bir gerekçe
kod kadar tehlikelidir.

---

### G-3 · ORTA — Sync gövdesinde SAYI sınırı var, BOYUT sınırı yok

**Nerede:** `Momentum.Application/Features/Sync/SyncRequestValidator.cs`

Doğrulayıcının tamamı üç kural: `Ops` null değil · `Ops.Count <= 100` · `SinceCursor.Seq >= 0`.

**Ölçüm:** `src/backend` genelinde `MaxRequestBodySize`, `RequestSizeLimit`, `MaxDepth`
ve alan bazlı `MaximumLength` **hiç** geçmiyor. Tek nicel sınır op **sayısıdır**.

**Etki:** 100 op'un her biri megabaytlarca alan taşıyabilir. Her op ayrı işlem kapsamında
(`BeginOpScopeAsync`) materialize edilir ve outbox'a yazılır — yani gövde büyüklüğü
doğrudan **veritabanı yazma yüküne** çevrilir. Saldırganın maliyeti tek istek, sunucunun
maliyeti bellek + 100 işlem + outbox şişmesi. Kestrel'in 30 MB varsayılanı vardır ama o
**varsayılandır, beyan edilmiş bir denetim değildir** ve eşzamanlı istekle çarpılır.
Ayrıca JSON derinliği sınırsız: derin iç içe gövde ayrıştırıcıyı zorlar.

**Yama:** op payload'ı için alan bazlı `MaximumLength`; `MaxRequestBodySize` açıkça
beyan; `JsonSerializerOptions.MaxDepth`. Sınırlar test edilebilir olmalı — 1 bayt fazlası
400 dönmeli.

---

### G-4 · DÜŞÜK — CI eylemleri değişebilir etiketle sabitlenmiş

**Ölçüm:** üç iş akışındaki **13 `uses:` satırının hepsi** hareketli etiket:
`actions/checkout@v4` · `subosito/flutter-action@v2` · `actions/setup-dotnet@v4` ·
`actions/setup-python@v5` · `actions/upload-pages-artifact@v5` · `actions/deploy-pages@v5`.
Commit sha ile sabitlenmiş **sıfır** eylem var.

**Etki:** tedarik zinciri. Bir eylem deposu ele geçirilir ya da etiket taşınırsa, kod
bir sonraki koşumda `GITHUB_TOKEN` yetkisiyle çalışır. `pages.yml` `pages: write` +
`id-token: write` taşır — yani ele geçirilen bir eylem canlı demoyu değiştirebilir.

**Hafifletici, ölçüldü:** `pull_request_target` **hiç kullanılmıyor** (fork'tan gelen
kodun sırlara eriştiği asıl tehlikeli desen yok) · `paket.yml` **hiçbir `secrets.*`
kullanmıyor** · `pages.yml` izinleri asgari ve yalnız `workflow_dispatch` ile tetikleniyor.
Bu yüzden DÜŞÜK, ORTA değil.

**Yama:** eylemleri tam commit sha'sına sabitle (`@<40 hex>`), Dependabot ile güncelle.

---

### G-5 · DÜŞÜK — Gerçek imzalama anahtarı halka açık depoda

**Nerede:** `src/backend/Momentum.Api/appsettings.Development.json`

`"Secret": "8YFLoB…"` (32 baytlik base64, tamami depoda — bu raporda bilerek kirpildi) — yer tutucu değil, geçerli
32 baytlık base64 bir HMAC anahtarı. Dosyada bilinçli olduğunu söyleyen bir yorum var
(*"DEMO signing key, Development ONLY"*) ve depo **public**.

**Etki tek başına yok, zincirde var.** `Program.cs`, `Jwt:Secret` eksikse açılışta
**patlar** (iyi bir karar). Ama tam bu yüzden, aceleyle üretime alan biri açılışı
geçmek için en kolay yolu seçer: bu dosyayı olduğu gibi taşımak ya da
`ASPNETCORE_ENVIRONMENT=Development` bırakmak. O anda anahtar **deponun tamamının
okuyabildiği** bir değerdir ve herkes istediği `sub` ile geçerli token imzalayabilir —
ve Development'ta `X-Momentum-Dev-User` kalkanı da açıktır. Yani "sıkı açılış kapısı"
kendi kaçış yolunu depoya koymuş oluyor.

**Ölçüldü:** git geçmişinde **hiç** `.env`, `.jks`, `.keystore`, `.p12`, `.pem`
eklenmemiş — bu tarafta temiz. `docker-compose.yml`'deki `POSTGRES_PASSWORD:
${POSTGRES_PASSWORD:-momentum_dev}` dışarıdan verilebilen bir varsayılan, sır değil.

**Yama:** anahtarı depodan çıkar, geliştiricide `dotnet user-secrets`'e al; dosyada
yalnız boş alan ve nasıl doldurulacağı kalsın. Üretimde `Jwt__Secret` ortam değişkeni
zorunlu olsun. **Anahtar depoda göründüğü an yanmıştır** — çıkarmak yetmez, döndürülür.

---

### G-6 · BİLGİ — Bayat yorum: "push-authz deferred"

`Endpoints/SyncEndpoints.cs:35` → `return Results.Unauthorized(); // deny-by-default (K2-E3 push-authz deferred)`

Push yetkilendirmesi **ertelenmiş değil, uygulanmış**: `SyncCommandHandler.IsAuthorizedAsync`
(satır 249+) ön/son scope kapısını koşuyor (`IzinPreAsync`/`IzinPostAsync`), `members`
yazımı yalnız proje sahibine açık. Yorum, kodun yaptığından daha zayıf bir şey söylüyor.

**Neden bildiriliyor:** G-2'nin kökü de böyle bir cümleydi. Bu projede bayat gerekçe
yorumları ölçülmüş bir kusur sınıfı — düşmüş gerekçe, gerçek bir kapıyı sildirebilir.

---

## 2. TEMİZ ÇIKANLAR — arama değil, ölçüm

Aşağıdakiler "desen bulunamadı" diye değil, **kod okunarak** temiz bulundu (K3).

| Sınıf | Ölçüm |
|---|---|
| **SQL enjeksiyonu** | 22 SQL taşıyan dosyanın tamamı parametreli (`@actorId`, `@ownerId`, `$1`). Tek string birleştirme `SyncPuller.cs:99` → `"... LIMIT " + PageSize`, ve `PageSize` `private const int = 500` — istek gövdesinden gelmiyor. |
| **IDOR — pull** | `SyncPuller`ın her sorgusu `@actorId` ile bağlı ve görünürlük `project_access` görünümünden okunuyor (satır 97-98, 162-172). `TaskReadStore`ın 4 sorgusu `WHERE owner_id = @ownerId`. |
| **IDOR — push** | `ownerId` ve `actorId` **kimlik doğrulamadan** geliyor, gövdenin `op.ActorId` iddiası açıkça yok sayılıyor (`SyncCommandHandler:172, 208-216`). |
| **Cursor kurcalama** | Cursor imzasız ama yalnız **konum** taşıyor (`{v,p,i}`); satır görünürlüğü her sorguda ayrıca `@actorId`+`project_access` ile süzülüyor. Forge edilen cursor sayfa atlatır, veri açmaz. `TryDecode` bozuk girdide **fırlatmıyor**, `false` dönüyor → 400, 500 değil. |
| **SignalR grup IDOR'u** | `SyncHub`ın **çağrılabilir hiçbir metodu yok**; tek grup `user:{self}` ve `self` sunucudaki kimlikten. İstemcinin grup adı belirlemesi yapısal olarak imkânsız. Kapsam yönlendirmesi bağlantı anından **yayın anına** taşınmış (o86-C §A). |
| **Dev kimlik kalkanı** | `CompositeCurrentUser` (dev başlığı) **yalnız** `IsDevelopment()` içinde kayıtlı (`Program.cs:57-65`); üretimde doğrudan `JwtCurrentUser`. |
| **CORS** | `AddCors` yalnız Development **ve** boş olmayan allow-list ile kuruluyor; `AllowAnyOrigin` yalnız bir yorumda geçiyor, kodda yok. Yapılandırmadan okunuyor, gömülü literal yok. |
| **JWT** | Issuer/audience/lifetime/imza doğrulaması açık, `ClockSkew` 30 sn. `Jwt:Secret` eksikse açılışta patlıyor (sessizce imzasız çalışma engellenmiş). |
| **Hub deny-by-default** | `/hubs/sync` negotiate öncesi 401 ara katmanı; hub içindeki `Context.Abort()` yalnız derinlik savunması olarak beyan edilmiş, bağımsız kapı sayılmamış. |
| **Hata kanalı** | `UseExceptionHandler` + ProblemDetails her ortamda; Developer Exception Page sızıntısı yok. |
| **İzolasyon** | COOP/COEP başlıkları ardışık hattın en üstünde, statik dosyadan önce. |
| **Git geçmişi** | `.env`, `.jks`, `.keystore`, `.p12`, `.pem` **hiç eklenmemiş**. |
| **CI tetikleyicisi** | `pull_request_target` yok; `paket.yml` hiç sır kullanmıyor; `pages.yml` izinleri asgari. |

---

## 3. ŞERİT KAPANIŞLARI

**Şerit C (BaaS / RLS): mimari olarak yok.** `supabase`, `firebase`, `appwrite`,
`pocketbase`, `@aws-amplify` → 0 eşleşme; `supabase/` klasörü, `firestore.rules`,
`storage.rules` yok. Yetkilendirme sunucuda, istemci veritabanıyla doğrudan konuşmuyor.

**Şerit D (yapay zekâ / LLM): mimari olarak yok.** `openai`, `anthropic`, `langchain`,
`llamaindex`, `ollama`, `chat.completions` → 0 eşleşme. İstem enjeksiyonu ve araç
yetkisi sınıfları yapısal olarak uygulanamaz.

---

## 4. ŞÜPHE — çalıştırılmadı, bulgu değil

- **Refresh token döndürme/tekrar oynatma.** `refresh_tokens` tablosu ve
  `/v1/auth/refresh` ucu var; kullanılmış bir token'ın iptal edilip edilmediği,
  çalınmış token'ın tespit edilip zincirin iptal edilip edilmediği **okunmadı**.
- **Parola politikası.** `RegisterRequestValidator` açılmadı; asgari uzunluk, sızmış
  parola kontrolü var mı bilinmiyor.
- **`EntityMaterializer` alan doğrulaması.** 78 parametreli, en yoğun yazma yüzeyi;
  alan adı/tip doğrulaması satır satır okunmadı.
- **OrSet üyelik silme yarışı.** Aynı anda davet + çıkarma işlemlerinde görünürlüğün
  hangi tarafa düştüğü **koşulmadı** (eşzamanlılık testi yapılmadı).

## 5. NE ÖLÇÜLMEDİ — açıkça

- **Hiçbir istek canlı sisteme atılmadı.** Denetim kaynak kodu, yapılandırma, iş akışı
  ve git geçmişi üzerinden yapıldı; backend ayağa kaldırılmadı, PoC HTTP çağrısı
  koşulmadı. G-1, G-2, G-3'ün etkileri **koddan çıkarıldı, koşumla doğrulanmadı.**
- **İstemci (Flutter) tarafı yüzeysel tarandı.** `Uri.parse` 17 çağrısı listelendi ama
  deep link / intent-filter / WebView yapılandırması **açılmadı**; Android manifest
  `exported=` denetimi yapılmadı. Şerit A eksik koştu.
- **Bağımlılık CVE'leri taranmadı.** `araclar/pub-cve-kapisi.py` depoda var ama
  **CI'da koşmuyor** (aynı gün ayrı bir ölçümle saptandı); bu koşumda da elle
  çalıştırılmadı. Bağımlılıkların bugünkü CVE durumu **ÖLÇÜLEMEDİ.**
- **Yayımlanmış APK sökülmedi.** Artefakt denetimi bu skill'in işi değil (`tuzak-app-qa`
  devri, §5 devir tablosu).
- **`verify.ps1` ve `paket.yml`'ın satır içi `run` blokları** güvenlik açısından
  okunmadı.

## 6. KENDİ KAPIMI SINADIM — K4 kör kapı protokolü

Bu rapordaki "yok" iddiaları, tarayıcı **kör olduğu için** boş çıkmış olabilirdi.
İki tarayıcı kum havuzunda (kopya üzerinde, hedef proje değiştirilmeden) kirletildi:

| kol | vaka | beklenen | sonuç |
|---|---|---|---|
| pozitif kontrol | temiz kopya, hız sınırı taraması | 0 vuruş | 🟢 0 |
| **mutant** | kopyaya `AddRateLimiter(...)` enjekte edildi | ≥1 vuruş | 🟢 **2** |
| pozitif kontrol | temiz kopya, SQL enjeksiyon taraması | 0 vuruş | 🟢 0 |
| **mutant** | `TaskReadStore`ın parametreli SQL'i `$"... {ownerId} ..."` yapıldı | ≥1 vuruş | 🟢 **1** (satır 61) |

**2/2 kol tuttu** → G-1'in "hız sınırı yok" hükmü ve "SQL enjeksiyonu yok" hükmü
kör bir taramanın sessizliği değil, **ısıran bir taramanın sonucudur.**

Betik: `C:\dev\_gedik_mutant.py` (depo dışında; kapı bütçesine girmez).

---

## 7. ÖNCELİK SIRASI

1. **G-1** — kimlik uçlarına hız sınırı + kilit. Tek gerçek saldırı yüzeyi bu.
2. **G-5** — imzalama anahtarını depodan çıkar ve **döndür**.
3. **G-2** — OpenAPI/Scalar'ı Development'a al, bayat yorumu sil.
4. **G-3** — gövde/alan boyutu sınırı.
5. **G-4** — eylemleri sha'ya sabitle.
6. **G-6** — bayat yorumu düzelt.

Düzeltme bu skill'in işi değil; görev yazan tarafa devredilir. **Her yama mutantla
ısırdığını kanıtlamadan yeşil sayılmaz.**

# BRİFİNG — dış inceleme için

**Tarih:** 6 Eylül 2026 · **Proje:** Momentum — çok platformlu görev yönetimi
**Durum:** `v1.1.0` teslim edildi; 13/13 madde bitti, açık dilim yok
**Bu belge kime:** kodu inceleyecek yazılımcıya. Tek oturumda okunacak uzunlukta.

> Aşağıdaki her sayı gerçek bir koşumdan gelir. Ölçülmemiş olan §4'te ayrıca listelidir;
> "ölçmediğine hüküm verme" bu projenin tek sert kuralıdır.

---

## 1 · NE, NEDEN

İşe-alım/portfolyo ödevi. Ürün bir **to-do** uygulaması — ama ödevin asıl konusu ürün değil,
**mimari ve kod kalitesi**. Vitrin üç zor şeyde:

1. **Çevrimdışı-öncelikli senkron** — uçak modunda yaz, bağlantı gelince kendiliğinden eşitle.
2. **Çakışma görünür olsun** — iki cihaz aynı görevi değiştirince kullanıcı kimin kazandığını görsün.
3. **Çok kullanıcılı paylaşım** — bir listeyi paylaş, ötekinin yazdığı senin ekranında belirsin.

**Yığın:** Flutter (Android + Web canlı; iOS yalnız CI'da derlenir, Mac yok) · N-katmanlı
.NET 10 / ASP.NET Core · PostgreSQL · SignalR (yüksüz sinyal) · Drift (istemci kalıcılık).

---

## 2 · NEREDEYİZ — ÖLÇÜLMÜŞ DURUM

| Ne | Ölçüm (6 Eyl 2026) |
|---|---|
| İstemci testleri | **767 yeşil**, `flutter test` EXIT 0 (128,4 sn) |
| `flutter analyze` | **0 uyarı** — *"No issues found!"* |
| Backend testleri | **177 başarılı / 0 başarısız / 0 atlanan** (mimari 6 · SyncCore 44 · Api 22 · kalıcılık 105) |
| `verify` zinciri | EXIT **0** — `build -warnaserror` 0 uyarı 0 hata · CVE kapısı **0 zafiyetli paket** |
| CI kapıları | `ci #85` · `paket #18` · `pages #17` — **üçü de `22fa0b2`'de yeşil** |
| Tek komutla ayağa kalkma | `docker compose up --build` → temiz koşucuda **149 sn** (önbelleksiz) |
| Çapraz-köken izolasyon | `crossOriginIsolated === true`, `SharedArrayBuffer` var (pakette ölçüldü) |
| İstemci kalıcılık | **OPFS** (Drift `drift_db/momentum/database`), geri düşüş değil |
| Çalışma imajı pini | `Microsoft.AspNetCore.App 10.0.11` · `Microsoft.NETCore.App 10.0.11` |
| Android paketi | `momentum-v1.1.0-emulator.apk`, 60.953.726 bayt, `versionCode 2` / `1.1.0` |

**Paylaşım vitrini canlı koşuldu** (gerçek telefon + emülatör + JWT'li üçüncü hesap,
`KANIT/o86F`) — **dokuz adımın dokuzu geçti**:

- Davet **11:33:51.094** → üyenin ekranında liste **11:34:01.216**: **≤10 sn**, üyede **hiçbir
  dokunuş ya da ikinci tetik yok**.
- Üye, davetten **önce** yaratılmış görevi de görüyor (katılım-sonrası geçmiş).
- Üyeden sahibe yayılma **≤12 sn**, elle yenileme yok.
- **Pozitif/negatif kontrol:** davet edilmemiş üçüncü hesabın snapshot'ı **boş**; üye paylaşmayı
  denediğinde *"Bu listeyi paylaşma yetkin yok (RejectedForbidden)"* ve üyelik tablosu değişmiyor.

**Senkron omurgası:** yerel yazma → itme kuyruğu → `POST /v1/sync`; sunucuda outbox + imleç
tabanlı çekme (snapshot/artımlı, `hasMore`). Çakışma çözümü **LWW** + kullanıcıya görünür rozet.
Üyelik **OrSet** (`project_members`), erişim **`project_access`** görünümünden (sahip ∪ üye).
Gerçek zamanlı kanal **veri taşımaz**, yalnız "çek" der.

---

## 3 · NASIL ÇALIŞIYORUZ (kod incelemesinde bunu göreceksiniz)

- **Ölçmediğine hüküm verilmez.** Her raporda boş bırakılamayan bir **"NE ÖLÇÜLMEDİ"** başlığı var.
- **Kör kapı yok.** Bir kapı, bozuk girdiyle **ısırdığını kanıtlamadan** yeşil sayılmaz — mutantla
  öldürülür. Örnek: Pages demosunun CDN kapısı **beş mutantla** doğrulandı.
- **Üreten ≠ denetleyen.** Hiçbir çıktı kendi üreticisi tarafından kabul edilmez.
- **Düşmüş denetimler silinmez.** `KANIT/` altında `…-DENETIMDE-DUSTU…`, `…-KILITLENEMEDI…`
  dosyaları **bilerek durur**. İzlenen 2.170 dosyanın **1.622'si** (%75, 27,9 MiB) ürün kodu değil,
  ham ölçüm kanıtıdır.

Bu disiplinin işe yaradığı ölçülmüş bir örnek: dört oturum boyunca "flake" sanılan bir kusur,
`SyncPuller`de gölgelenmiş bir `ORDER BY` çıktı — basamak sınırında satırlar **sessizce
kayboluyordu** ve `v1.0.1` bu kusurla teslim edilmişti (`KANIT/o84`).

---

## 4 · NELER KALDI — ve NE ÖLÇÜLMEDİ

**Bilinen açık kusurlar / sınırlar:**

1. 🔴 **Alan-kanalı sessiz kayıp.** `projectId`/`fields:projectId` **çakışma tespitine girmiyor**
   (`kanonikDize` çağrılmıyor, LWW sessizce kazanıyor). Ayrıca fractional alanlar
   (`pos`/`listPos`) snapshot'ta `fields:$ad`, artımlıda `order:$ad` olarak yazılıyor ⇒ aynı PK'de
   **iki satır**. Bugün etkisiz; sıralama kanalı açılınca `o84` sınıfı bir kusur doğurur.
2. **Sahiplik devri op'u YOK.** `tasks.owner_id` `ON CONFLICT`'ta güncellenmiyor (ilk yazan sabit)
   ⇒ üyenin projede yarattığı görevi sahip listeden koparırsa görev **üyenin** kutusunda kalır ve
   sahip artık ona yazamaz.
3. **Çakışma tespiti yalnız başlık ve tamamlanma** için çalışır; `priority`/`dueAt` sınıfı alanlar
   sessizce LWW ile ezilir.
4. **Etiketlerde büyük/küçük harf katlaması yok** (sunucu Ordinal karşılaştırır): `İş` ≠ `iş`.

**NE ÖLÇÜLMEDİ (dürüst liste):**

- **Gerçek arm64 donanımda paket kapısı koşulmadı** — kırılma yalnız manifestle gösterildi,
  arm64 makine yok.
- **Erişilebilirlik duyuruları gerçek ekran okuyucuyla (TalkBack) dinlenmedi** — widget testinde
  `announce` mesajı yakalanarak ölçülüyor.
- **iOS hiçbir cihazda koşmadı** (Mac donanımı yok; yalnız CI'da derlenir).
- **Liste diliminin canlı turu kısmi:** liste yaratma/girme/görev ekleme gerçek arayüzde ölçüldü;
  listenin **silinmesi** ve görevin listeler arası **taşınması** yalnız widget testiyle ölçülü.
- **Drift OPFS ölçümü tek tarayıcıda** (Chrome) yapıldı; Firefox/Safari'de geri düşüş ölçülmedi.
- **Yerel soğuk derleme süresi ölçülemedi** — Docker Desktop'ın Linux motoru derleme ortasında
  düştü (bellek). Soğuk sayı temiz CI koşucusundan alındı.

**Kapsamdan kesilenler** (eksik değil, takvim kutusu gereği **kesilmiş** ve yazılmış): hatırlatıcı
/ bildirim (4 Eyl) · tekrar eden görev, RRULE (5 Eyl) · proje-liste klasörü · Windows masaüstü
`.exe` · parola sıfırlama · e-posta doğrulama · OAuth · 2FA · RBAC.

---

## 5 · YOL HARİTASI

Ürün 13/13 bitti, açık dilim yok. Sıradaki iş **kusur kapatma**, yeni özellik değil:

1. Alan-kanalı sessiz kaybı (§4/1) — sıralama kanalı açılmadan **önce** kapatılmalı.
2. Sahiplik devri op'u (§4/2) — ya op eklenir ya "kapsam dışı" diye yazılır.
3. Çakışma tespitini `priority`/`dueAt`e genişletmek.
4. Gerçek arm64 ve TalkBack ölçümleri — donanım/erişim gerektiriyor.

---

## 6 · SİZDEN NE İSTİYORUZ (öncelik sırasıyla)

1. **Senkron omurgası doğru mu?** Outbox + imleç tabanlı çekme + LWW + OrSet üyelik. Tam bir CRDT
   (ör. Automerge/Yjs) kullanmak bu ölçekte kazanç mı olurdu, yoksa gereksiz karmaşa mı?
2. **§4/1'e nereden bakardınız?** `projectId` çakışma tespitine girmiyor ve fractional alanlar iki
   satır üretiyor. Sıralama kanalını açmadan önce mi kapatmalı, yoksa kanalla birlikte mi?
3. **Sahiplik devri (§4/2)** — bu bir kusur mu, yoksa bilinçli sadelik olarak yazılıp geçilebilir mi?
4. **Yetki/görünürlük deseni.** Bu alan geliştirme sırasında **beş kez ısırdı**; hepsinin kökü
   aynıydı: yetki kararı op'un **kendi beyanına** bakıyordu. Şimdiki desen — kaynak ve hedef
   scope'un ikisini birden sormak, kapsamsız görevde `tasks.owner_id`e düşmek — sizce yeterli mi,
   nerede kaçış kalmış olabilir?
5. **Test dağılımı 767 istemci / 177 backend.** Bu oran sizce sağlıklı mı; backend'de hangi
   sınıfa test eklerdiniz?
6. **Kodda en çok hangi bölüm sizi endişelendiriyor?**

Cevaplarınızı **"şu dosyada şu satır"** düzeyinde verirseniz doğrudan iş emrine çeviririz —
burada her düzeltme ölçülüp bir kapıya bağlanıyor.

---

## 7 · NEREYE BAKMALI (sıralı)

| Sıra | Yer | Neden |
|---|---|---|
| 1 | `README.md` | 5 dakikada mimari, ölçülmüş sınırlar, çalıştırma |
| 2 | `docs/ODEV.md` | kapsam otoritesi — neyin istendiği |
| 3 | `src/backend/Momentum.Application` | CQRS, doğrulama, senkron komut işleyicisi |
| 4 | `src/backend/Momentum.Infrastructure` | outbox dağıtıcısı + imleç tabanlı çekme — **kalbi burası** |
| 5 | `src/client/lib/` | Drift kalıcılık, itme kuyruğu, çakışma rozeti |
| 6 | `docs/ADR/` | kararların gerekçesi |
| 7 | `KANIT/o86F` · `KANIT/o84` | son canlı vitrin · sessiz veri kaybının kök neden raporu |

### Kod haritası — platformlar AYRI kod tabanı DEĞİL

Android, iOS ve Web **tek bir Dart kod tabanını** paylaşır; platform klasörleri yalnızca ince
kabuklardır. Ölçüm (`git ls-files`, 6 Eyl 2026):

| Yer | Ne | Ölçü |
|---|---|---|
| `src/client/lib/` | **paylaşılan** Flutter/Dart ürün kodu — üç platformun tamamı buradan çıkar | 42 dosya · **12.904 satır** |
| `src/client/android/` | Android kabuğu (Gradle, manifest, ikon) | 20 dosya · 290 satır |
| `src/client/ios/` | iOS kabuğu (Xcode projesi) — **hiçbir cihazda koşmadı**, yalnız CI'da derlenir | 40 dosya · 1.342 satır |
| `src/client/web/` | Web kabuğu: `index.html` + `manifest.json` = **73 satır** el yazısı. Yanındaki `drift_worker.js` (Drift üretiyor) ve `sqlite3.wasm` (ikili) satır olarak sayılmaz | 9 dosya |
| `src/backend/` | .NET, dört katman: Domain **1.504** · Application **1.121** · Infrastructure **6.374** · Api **990** satır | 133 dosya · **9.989 satır** |
| `src/client/test/` + `tests/` | testler | 121 dosya · **24.572 satır** |

**Test satırı ürün satırından fazladır: 24.572 / 22.893.** Bilinçlidir.

*(Not: `Infrastructure` ve `tests` ikisi de tam 6.374 satır çıktı — kopyala-yapıştır değil,
iki kez ayrı ayrı ölçüldü, rastlantı.)*

---

## 8 · ÇALIŞTIRMAK İSTERSENİZ

**İncelemek için gerekmiyor — depo yeter.** Ayrı bir kurulum paketi yoktur ve gerekmez: bu depoda
model ağırlığı, veri kümesi ya da sanal ortam taşınmaz; `.gitignore` yalnızca derleme çıktısını,
sırları ve editör çöpünü dışlar. `.env` bile oluşturmanıza gerek yok — `docker-compose.yml`
parola/kullanıcı için varsayılan taşır.

🔴 **Fiilen çalıştıracaksanız üç şey gerekiyor:** (1) **Docker Desktop** kurulu olmalı,
(2) **ilk derlemede internet** gerekir — Flutter SDK'sı, apt ve NuGet/pub paketleri o sırada iner
(kurulduktan sonra uygulama offline çalışır), (3) makinede rahat **~8 GB RAM** — temiz bir CI
koşucusunda ilk derleme **149 sn** sürüyor ama belleği dolu bir dizüstünde Docker'ın Linux motoru
derleme ortasında düşebiliyor (geliştirme makinesinde bir kez ölçüldü). **5298 portu boş olmalı.**

```
git clone https://github.com/onur-kesim/Momentum.git
cd Momentum
docker compose up --build
```

Sonra `http://localhost:5298`. Sıra otomatiktir: postgres → migrator (şemayı kurar, çıkar) → api
(web istemcisini de aynı kökenden servis eder). Hazır Android APK'sı `v1.1.0` release'inde
(emülatör için `10.0.2.2` hedefiyle derlendi; gerçek telefonda kendi LAN IP'nizle yeniden derleyin).

Hiçbir şey kurmadan bakmak isterseniz: <https://onur-kesim.github.io/Momentum/> —
aynı istemci ama **backend yok**, veriler yalnız tarayıcıda kalır ve senkron/paylaşım vitrini
görünmez. Vitrin yalnız docker paketinde çalışır.

# MOMENTUM — ARŞİV DEVİR BELGESİ

**Kapanış: 27 Eylül 2026.** Proje bitti. Dış inceleme (bağımsız yazılımcı) **tam not** verdi.
**Yayınlama kararı YOKTUR** — bu depo portföy ve gerekirse devam noktası olarak duruyor.

Son sürüm **`v1.1.0`** · etiketlenen commit **`22fa0b2`** · kapanış commit'i **`ca9b317`**
Depo: <https://github.com/onur-kesim/Momentum> (public, MIT)

> Bu belge deponun kökünde durur ve **yalnız klonla da yeterlidir**. Yerel arşivde (Onur'un
> diskinde) ek olarak üç şey daha var; §3'te işaretli — GitHub klonunda **bulunmazlar**.

---

## 1. NE BU

Çok platformlu görev yönetimi (to-do). İşe-alım/portfolyo ödevi; **odak ürün değil, mimari ve
kod kalitesi.** Yığın: Flutter (Android + Web canlı; iOS yalnız CI'da — Mac yok) · N-katmanlı
.NET 10 / ASP.NET Core · PostgreSQL · Drift (istemci kalıcılık) · SignalR (yüksüz sinyal).

Vitrin üç zor şeyde: **çevrimdışı-öncelikli senkron** · **çakışmanın görünür olması** ·
**çok kullanıcılı paylaşım**. Üçü de gerçek cihazlarda ölçüldü (`KANIT/o86F`, 9/9).

Kapsam otoritesi [`docs/ODEV.md`](docs/ODEV.md) · zorunlu şartlar ve ölçülmüş sınırlar
[`README.md`](README.md) · karar kayıtları [`docs/ADR/`](docs/ADR/) · proje esasları
[`CLAUDE.md`](CLAUDE.md) · son durum [`DURUM.md`](DURUM.md).

## 2. KLONDAN ÇALIŞTIRMAK

```
git clone https://github.com/onur-kesim/Momentum.git
cd Momentum
docker compose up --build
```
Sonra `http://localhost:5298`. Sıra otomatiktir: postgres → migrator (şemayı kurar, çıkar) → api
(web istemcisini de aynı kökenden servis eder).

**Ölçülmüş bedeli:** ilk derleme temiz bir CI koşucusunda **149 sn**; geliştirme makinesinde sıcak
önbellekle **443,6 sn** sürdü ve bir kez de **soğuk derleme ÇÖKTÜ** (Docker'ın Linux motoru
düştü, bellek yetmedi). İlk derlemede **internet gerekir** (Flutter SDK, apt, NuGet/pub iner);
kurulduktan sonra uygulama offline çalışır. Rahat **~8 GB RAM** ve **5298 portu boş** olmalı.

Kurmadan bakmak için: <https://onur-kesim.github.io/Momentum/> — aynı istemci ama **backend
YOKTUR**; senkron, çakışma ve paylaşım vitrini orada **görünmez** (ölçüldü:
`crossOriginIsolated === false`, yazılan satır *"↑ Gönderiliyor"*da asılı kalır).

Hazır APK: [Releases → `v1.1.0`](https://github.com/onur-kesim/Momentum/releases/latest).
🔴 Emülatör hedefli derlendi (`10.0.2.2`), **gerçek telefonda çalışmaz** — kendi LAN IP'nizle
yeniden derleyin. Debug anahtarıyla imzalıdır (yazılı kapsam kararı).

## 3. YEREL ARŞİVDE OLAN, KLONDA OLMAYAN ÜÇ ŞEY

| ne | boyut | niye önemli |
|---|---|---|
| `_momentum-teslim-paketi-v1.1.0/` | 340 MB | **Önceden derlenmiş Docker imajları.** `docker load -i` + `docker compose up -d` ⇒ **9,03 sn**'de ayağa kalkar, derleme yok, internet yok. §2'deki 149–443 sn ve çökme riski bu yolla tamamen kalkar. Brifing PDF'i de içinde (5 sayfa) |
| `_momentum_kanit_ham_20260908-1918/` | 32 MB · 1627 dosya | `KANIT/` 8 Eyl'de 1622 → 145 dosyaya budandı; **çıkarılanların tamamı** burada. Erişim haritası [`KANIT/INDEX.md`](KANIT/INDEX.md) |
| `_yedek_momentum_...bundle` + `Momentum-YEREL.bundle` | 24 MB | `git bundle` — tüm ref'ler. GitHub bir gün kaybolursa depo **bunlar** |

Bu üçü Onur'un diskindeki arşiv klasöründedir. Devralan taraf bunlara ihtiyaç duyarsa Onur'dan
istemelidir; depo onlar olmadan da tam çalışır.

---

## 4. NE ÖLÇÜLDÜ (v1.1.0)

- İstemci **767** test yeşil · `flutter analyze` **0** uyarı (`src/client` dizininden)
- Backend **177** başarılı / 0 başarısız / 0 atlanan · `verify` zinciri EXIT 0 ·
  `build -warnaserror` 0 uyarı · CVE kapısı **0 zafiyetli paket**
- CI kapıları **`ci #85` · `paket #18` · `pages #17`**, üçü de `22fa0b2` üzerinde yeşil; sha'lar
  her koşumun **kendi run kaydından** okundu, liste satırından DEĞİL (o81 dersi: liste satırı
  dalın o anki başını gösterip yanılttı)
- Yayınlanan APK indirilip **sha256 + `aapt dump badging`** ile doğrulandı (`1.1.0` / `2`)
- Paketlenmiş web istemcisinde **`crossOriginIsolated === true`** · Drift kalıcılık kipi **OPFS**
  (kalıcı, geri düşüş değil — iki uçtan ölçüldü)
- Paylaşım üyenin ekranına **≤10 sn**'de düştü, ikinci tetik olmadan (gerçek telefon + emülatör +
  JWT'li üçüncü hesap) · üyeden sahibe yayılma **≤12 sn** · davetsiz hesabın snapshot'ı **boş**
- **Bağımlılık kapıları CI'da mekanik koşar** (27 Eyl): `bagimlilik` işi her push'ta altın kümeyi
  (ağsız öz-kanıt), `pubspec` değişince de **gerçek taramayı** koşar — 108 paket, TEMİZ

## 5. AÇIK BULGULAR — kapatılmadı, gizlenmedi

[`GEDIK_RAPOR_2026-09-08.md`](GEDIK_RAPOR_2026-09-08.md) altı gedik bildirdi (KRİTİK yok). Arşiv
kararı: **kutu kapandı, dış inceleme bitti, bunlar bilerek kapatılmadı.** Devam edecek taraf için:

| | bulgu | durum |
|---|---|---|
| **G-1** | Kimlik uçlarında **hız sınırı ve hesap kilidi YOK** (`RateLimiter`/`Lockout`/`AccessFailedCount` → `src/backend`'de 0 eşleşme) | 🔴 **YÜKSEK — devam edilirse ilk iş budur** |
| G-2 | `MapOpenApi` / `MapScalarApiReference` `IsDevelopment`'a alınmalı | ORTA, açık |
| G-3 | `sync` gövdesine boyut/derinlik sınırı yok | ORTA, açık |
| G-4 | 13 CI eylemi sha'ya sabitlenmedi | DÜŞÜK, açık |
| **G-5** | *"`appsettings.Development.json`'daki imzalama anahtarı çıkar ve döndür"* | 🟢 **YANLIŞ POZİTİF — 27 Eyl'de ölçüldü** |
| G-6 | `SyncEndpoints`'teki bayat *"push-authz deferred"* yorumu silinmeli | BİLGİ, açık |

**G-5 neden yanlış pozitif:** dosyanın kendi 8. satırı zaten yazıyor —
*"DEMO signing key, Development ONLY — same convention as docker-compose.yml's POSTGRES_PASSWORD
dev default (documented, not a real secret)"*, 21 Ağu 2026'da `secrets.token_bytes(32)` ile
üretilmiş. `Jwt:Secret` her ortamda zorunludur, Development dışında **dışarıdan** verilir, eksikse
uygulama açılışta patlar. Anahtar döndürülmedi; depo public kalabilir.

🔴 **Her yama mutantla ısırdığını kanıtlamadan yeşil sayılmaz** — bu deponun sert kuralı.
Kapı bütçesi (`araclar/` ÷ el yazımı `src/`): **1561 / 18436 = %8,47 🟢**, tavan 1843 ⇒ yeni kapı
ve mutantı için **282 satır** hareket alanı var.

## 6. NE ÖLÇÜLMEDİ — boş bırakılmadı

- **Gerçek arm64 donanımda paket kapısı koşulmadı** (arm64 makine yok) — v1.0.1'den devreden bulgu
- **iOS hiçbir cihazda koşmadı** (Mac donanımı yok; yalnız CI'da derlenir)
- **Erişilebilirlik duyuruları gerçek ekran okuyucuyla (TalkBack) dinlenmedi** — widget testinde
  `announce` yakalanarak ölçülür, kulakla dinlenmedi. Kapsam kararıdır
- **Liste diliminin canlı turu kısmi:** listenin silinmesi ve görevin listeler arası taşınması
  gerçek arayüzde koşulmadı, yalnız widget testleriyle ölçülü
- **Yerel soğuk derleme süresi ölçülemedi** (Docker motoru düştü); soğuk sayı temiz CI'dan alındı
- **Drift OPFS ölçümü tek Chrome oturumunda** yapıldı; Firefox/Safari'de geri düşüş ölçülmedi
- 🔴 **Kapı bütçesi kuralının yazılı tanımı ile fiilî ölçümü AYNI DEĞİL.** `CLAUDE.md` §4 md.10
  *".g.dart .wasm .lock"* çıkarır der; fiilî ölçüm ayrıca `.js` `.json` `/Migrations/`
  `Designer.cs` `ModelSnapshot.cs` `.freezed.dart` ve `src/**/test/` çıkarıyor. Yazılı tanım
  birebir uygulanırsa oran **%10,3** çıkar. **Kör kapı sınıfı, kapanmadı**
- Release sayfasının gövdesi `KANIT/` için **1622 dosya** der; depoda **145** var. O sayı etiket
  anında doğruydu, yayımlanmış gövde bilerek değiştirilmedi; README güncel sayıyı yazar

## 7. DEVAM EDECEK TARAFA

1. [`CLAUDE.md`](CLAUDE.md) ve [`DURUM.md`](DURUM.md) **8 KB tavanındadır** — yeni kural girmek
   için bir satır silmek gerekir (İŞLEYİŞ md.8). Bilinçli kısıttır
2. Ortam mayınları `CLAUDE.md` §4'te ve **yalnız ölçülmüş olanlar** oradadır: PowerShell 5.1'de
   `&&` yok · mount'ta düz `git status` yasak · `git add -A` yasak (yol belirtilir) · Flutter
   komutu `src/client`'tan koşar · yol saf ASCII
3. `arsiv/` **append-only tarihçedir, açılışta okunmaz** — yalnız *"bu karar neden alındı?"* için
   (`PROJE_HAFIZA.md` · `BORCLAR.md` · `GOREV_CLAUDE_CODE/` · `PROJE_RADAR.jsonl`)
4. Kesilen kapsam maddeleri `CLAUDE.md` §5'te ve README'de yazılıdır — eksik değil, **kesilmiş**

---

*Bu belge 27 Eyl 2026'da yazıldı. İçindeki her sayı bir ölçümden gelir; ölçülmeyen "ölçülmedi"
diye yazılıdır — bu projenin tek sert kuralı buydu.*

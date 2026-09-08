# DİLİM-2 — M-2: Kod Sağlığı Kapısı (satır ekseni)

Dilim numarası tahmin değil ölçüm: `git log` içinde "M-1" etiketli iki commit
zaten var (`1f602c6`, `5d96623`, bkz. `git log --oneline | grep '^M-1'`), M-serisi
belge/kapı dizisinde bu ilk `DİLİM-*.md`. O yüzden ikinci dilim → `DİLİM-2`.

## 1. Hedef

M-2 iş emri iki eksen istiyordu: satır uzunluğu + fonksiyon karmaşıklığı. **Bu
dilim yalnız satır eksenini kapatır.** Karmaşıklık ekseni AÇIK bırakıldı (bkz. §4)
— gerekçe DÜZELTİLDİ (8 Eyl araştırması, canlı sayfadan ölçüldü, ilk yazım
YANLIŞTI):

İlk yazım "Dart/C# için böyle bir dış tanık yok" diyordu. **ÇÜRÜTÜLDÜ:**
- `pub.dev/packages/analyzer` v14.3.0 (1 Eyl 2026), yayıncı `tools.dart.dev`
  (doğrulanmış, Dart ekibi), BSD-3-Clause → Dart'ın **RESMİ AST'İ VAR.**
- `dart_code_metrics` ÖLÜ: "This package has been discontinued and is no
  longer maintained", son sürüm 5.7.6 (16 Tem 2023).
- DCM (dcm.dev) ücretsiz kademesi var ("$0 (no card required) For 1 seat",
  "Up to 50k analyzed LOC") ama `dcm activate --license-key=YOUR_KEY` "once
  per device" ister — CI'da her koşum TAZE makine, yani her koşumda **sır**.
- `Microsoft.CodeAnalysis.Metrics` v5.6.0 (2 Tem 2026), MIT, 6,7M indirme,
  sağlam — ama YALNIZ C#; bugünkü 5 ihlalin 5'i Dart, 0'ı C#.

Doğru hüküm: eksen bu dilimde YAZILMADI çünkü o dilimde **ÖLÇÜLMEDİ ve
KOŞULMADI** — "araç yok" olduğu için DEĞİL. Doğru yol radon kalıbının aynısı:
kapı `package:analyzer` + Roslyn üzerine **KENDİ aracımız** olur (CI'da sır
yok, tavan yok, tekrarlayan maliyet yok); DCM'in ücretsiz kademesi **BİR KEZ**
yerelde çapraz tanık olarak koşulur, kanıtı `KANIT/`e yazılır. Bu **bir
SONRAKİ dilimdir**, bu dilimde yazılmaz. Süslü parantez sayan bir sezgisel
yazıp adına "karmaşıklık kapısı" demek, `ADR_CC_OLÇÜTÜ.md`'nin kapattığı
kusuru (ölçüt kabul edilmeden yazılan kural) geri getirir — dış tanık artık
VAR, ama henüz SEÇİLİP KOŞULMADI.

## 2. Ölçülen taban (`araclar/kod_sagligi_taban.json`, commit `7aada32`)

Kapsam: `git ls-files -- '*.dart' '*.cs'`, üretilenler (`*.g.dart`,
`*.freezed.dart`, `*Designer.cs`, `*ModelSnapshot.cs`, `*/Migrations/*`),
`arsiv/` ve testler (`src/client/test/`, `tests/`) hariç.

| | |
|---|---|
| el yazımı ürün kaynak dosyası | **162** |
| toplam satır (python sayımı) | **16.238** |
| 400 satırı aşan | **5** (%3,1) |

5 dosya: `gorev_deposu.dart` (1135) · `gorev_listesi_ekrani.dart` (1005) ·
`uzak_degisiklik_uygulayici.dart` (887) · `gorev_satiri.dart` (823) ·
`senkron_dongusu.dart` (549). Bu turda **hiçbiri bölünmedi** — kapı kuruldu, kod
taşınmadı (§6, iş emri).

🔴 **Satır sayımı python'dadır, PowerShell'de DEĞİL:** `Measure-Object -Line`
boş satırları saymıyor ve 8 Eyl'de ilk ölçümü %8 eksik gösterdi
(`gorev_deposu.dart`: 1047 vs gerçek 1135). `kod_sagligi.py` `sum(1 for _ in
open(yol, encoding="utf-8"))` kullanır; `kod_sagligi_mutanti.py`'nin B kolu
(401 satırın 200'ü boş) tam bu sınıf kusuru sınar.

## 3. Yazılanlar

- [`araclar/kod_sagligi.py`](araclar/kod_sagligi.py) — ölçüm + kapı. `--json`
  · `--kapi` · `--kok`/`--taban` override (kum havuzu izolasyonu için).
  Çıkış kodu: 0 geçti/rapor · 1 ihlal (yalnız `--kapi`) · 2 ÖLÇÜLEMEDİ.
- [`araclar/kod_sagligi_taban.json`](araclar/kod_sagligi_taban.json) — sayı
  değil KÜME (§3[B], iş emri): dosya bölünüp biri eşiğin altına inince sayı
  sabit kalıp kapıyı kör bırakmasın diye.
- [`araclar/kod_sagligi_mutanti.py`](araclar/kod_sagligi_mutanti.py) — 6 kol,
  her biri kendi geçici git deposunu kurar: pozitif kontrol (hepsi eşik altı →
  YEŞİL) · Mutant A (401 dolu satır, tabanda yok → KIRMIZI) · Mutant B (401
  satır, 200 boş → yine KIRMIZI) · Mutant C (taban dosyası yok → exit 2, 0
  DEĞİL) · Mutant D (taban 500/dosya 300 → `TABAN GERI ADIM`) · Mutant E
  (taban 500/dosya YOK → `TABAN DOSYASI KAYIP`). Artı 2 aşırı-tetikleme ayağı
  (kol sayılmaz, kendi havuzlarında): 500→450 (hâlâ eşik üstü) ve 500→900
  (büyüdü) — ikisi de YEŞİL kalmalı, kural 3 fazla ateşlenmemeli.
- [`.github/workflows/ci.yml`](.github/workflows/ci.yml) — yeni `kod-sagligi`
  işi: `working-directory: .` (backend işindeki gibi elle verildi — üstteki
  `defaults` `src/client`'tır), önce kapı sonra mutant koşar, `continue-on-error`
  yok.

## 4. Kapı beyanı (bu oturumda ölçüldü, `7aada32`'nin ustune yazilan agacta -- yani bu belgenin bulundugu commit'te; o sha bu dosyanin icine yazilamaz (oz-basvuru))

1. `python araclar/kod_sagligi.py --kapi` → **exit 0**; metin raporu birebir:
   *"162 dosya tarandi ... toplam 16238 satir, 5 ihlal (esik=400 satir)"*, SONUÇ
   YEŞİL.
2. `python araclar/kod_sagligi_mutanti.py` → **exit 0**, 6/6 kol `[TUTTU]` +
   2 aşırı-tetikleme ayağı `[TUTTU]`.
   Ek özdenetim (iş emri istemedi, biz ekledik): (a) kural 1 VE kural 2
   birlikte kod içinde devre dışı bırakılıp yeniden koşuldu — çerçeve doğru
   şekilde Mutant A/B'yi `[SAPTI]` ve toplam `exit 1` ile yakaladı; (b) kural
   3 tek başına devre dışı bırakılıp yeniden koşuldu — Mutant D/E doğru
   şekilde `[SAPTI]` dedi. Her ikisinde de dosya yedekten `cp` ile geri
   alınıp diff'in boş olduğu doğrulandı, tekrar 6/6 + 2/2 `TUTTU`.
3. `grep continue-on-error .github/workflows/ci.yml` → 0 eşleşme.
4. 🔴 **AÇIK KALAN:** CI'nin gerçek koşumundaki log kanıtı — push Onur'da,
   push bu oturumda YAPILMADI (kural). "162/5 sayıları CI logunda görünüyor"
   maddesi, Onur commit'i push edip `kod-sagligi` işi bir kez yeşil yanana
   kadar **ÖLÇÜLEMEDİ** sayılmalı; kağıt beyan değil, ilk gerçek koşumun run
   kaydı doğrulanmalıdır.

## 4-EK. Bagimsiz denetim (ureten degil, 8 Eyl 2026)

Kapi beyani KAGITTAN degil, CANLI CIKTIDAN dogrulandi -- komutlar denetci
tarafindan yeniden kosuldu, builder'in raporu okunmadi:

| olculen | sonuc |
|---|---|
| `kod_sagligi.py --kapi` | exit **0**, "162 dosya ... 16238 satir, 5 ihlal", YESIL |
| rapor kipi (`--kapi` yok) | exit **0** -- olcum kipi kapiya donusmuyor |
| `kod_sagligi_mutanti.py` | exit **0**, 4/4 `[TUTTU]` |
| `continue-on-error` | ci.yml'de **0 eslesme** |
| taban dosyasi | 5 dosya, esik 400, sayilar bagimsiz olcumle **birebir** tutuyor |

**BULGU 1 -- 3. KIRMIZI KURAL MUTANTSIZDI.** Kapinin uc kirmizi kurali var;
mutantin dort kolu yalnizca 1. ve 2. kurali atesliyor (A/B yeni ihlal, C taban
yok, pozitif kontrol yesil). **Kural 3 (cirCir: tabandaki dosya esigin altina
inerse / kaybolursa) kodda vardi ama ISIRDIGI HIC GOSTERILMEMISTI.** Denetci
kum havuzunda elle atesledi -- **6/6 kol tuttu**:

- 3a taban 500 -> dosya 300 satir  -> KIRMIZI, iz `TABAN GERI ADIM` 🟢
- 3b tabandaki dosya kayboldu      -> KIRMIZI, iz `TABAN DOSYASI KAYIP` 🟢
- 3c kontrol: 500 -> 450 (hala esik ustu) -> YESIL (kural asiri tetiklemiyor) 🟢
- 4  bilinen sinir: taban dosyasi 500 -> 900 BUYUDU -> YESIL (kirmizi YAKMAZ) 🟢
- 5  sinir: tam 400 satir -> ihlal DEGIL (kural `>400`) 🟢
- 6  sinir: 401 satir -> ihlal 🟢

Kural 3 **davranissal olarak dogrulandi**, ama dogrulama denetcinin gecici
betigindeydi; **CI'da kosmuyordu**. Elle bir kez ispatlanmis kural, projenin
kendi doktrinine gore surekli ispatlanmis sayilmaz. **ACIK IS KAPANDI (8 Eyl,
ayni oturum):** `kod_sagligi_mutanti.py`'ye Mutant D (3a: taban geri adim) ve
Mutant E (3b: taban dosyasi kayip) kollari + 2 asiri-tetikleme ayagi (3c, 4)
eklendi -- artik 6 kolun 6'si de mekanik, CI'da kosuyor
(`python araclar/kod_sagligi_mutanti.py` -> exit 0). Kor-mutant ozdenetimiyle
de dogrulandi: kural 3 kod icinde gecici kapatilinca D ve E dogru sekilde
`[SAPTI]` dedi (exit 1), sonra dosya yedekten geri alinip 6/6 tekrar `TUTTU`
oldugu goruldu.

**BULGU 2 -- KIRIK KAPI ANKRAJI (duzeltildi).** §4 basligi olcumu
`103306e`'ye capaliyordu. O commit gercek ve main'de, ama **6 Eyl tarihli,
konusu docker-compose `DEV_USER_ID` yorumu** ve HEAD'den 10 commit geride --
o agacta `kod_sagligi.py` HENUZ YOK. Beyani oradan uretmeye calisan bos agac
bulurdu. Ankraj ebeveyn commit'e (`7aada32`) tasindi; oz-basvuru ozyinelemesine
girmeden dogru agaci gosteriyor.

**BILINEN VE KABUL EDILMIS SINIR (ustu kapanmasin diye yaziliyor):** taban
kumesindeki bir dosyanin BUYUMESI kirmizi yakmaz -- `gorev_deposu.dart` 1135'ten
5000'e ciksa kapi yesil kalir, rapor yalnizca `+N satir` yazar. Bu, is emrinin
bilincli tercihiydi (cirCir yeni ihlali ve geri adimi tutar, buyumeyi tutmaz).
Karsi tedbir gerekiyorsa ayri kilit ister.

**NE OLCULMEDI:** CI'nin gercek kosumundaki log kaniti (push oncesi
olculemez, bkz. §4 madde 4) · karmasiklik ekseni (§1) · 5 uzun dosyanin ICI --
uzunluklarinin hakli olup olmadigi okunmadi, yalniz sayildi.

## 5. M-3'ün ilk koşumu: belge/kod oranı

Kod satırı (python sayımıyla, D/E kolları + 2 ayak eklendikten sonra):
`kod_sagligi.py` 253 + `kod_sagligi_mutanti.py` 319 + `kod_sagligi_taban.json`
13 + `ci.yml` eklenen 19 satır = **604**.
Bu belgenin kendisi **167** satır (python sayımıyla, aynı yöntem).

**Oran = 167 / 604 ≈ 0,28 ≤ 1,0.** Geçti.

## 6. Sonuç

**M-2'NİN SATIR EKSENİ KAPANDI. KARMAŞIKLIK EKSENİ AÇIK** — dış tanık
seçilmeden yazılmaz (§1). Bu turda: 5 uzun dosya BÖLÜNMEDİ, testler ölçüldü
ama kapıya sokulmadı, `git add -A` kullanılmadı. **PUSH ONUR'DA** — §4 madde 4
ancak push sonrası kapanır.

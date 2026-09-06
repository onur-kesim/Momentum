# KANIT/o86E — o86-E bagimsiz denetimi (6 Eyl 2026, oturum 90)

**Karar: KABUL EDILDI.**

Kusur ve kok neden `KANIT/o86F/OKUMA-NOTU.md`de (§F adim 6 canli turu). Bu klasor duzeltmenin
denetimidir.

## Kapsam — kilide sadik

- Degisen URUN dosyasi **yalnizca** `src/client/lib/veri/senkron_dongusu.dart` (DUR 2 ✅)
- `git diff --stat -- src/backend tests` **BOS** ⇒ sunucu ayagina dokunulmadi (DUR 1 ✅)
- Test dosyasi: `src/client/test/g1_cekme_yolu_kapisi_test.dart` (G7/G8/G9 eklendi)

## Duzeltme

Tur sonu donusu artik kayit: `(devamGerekli, resyncTetiklendi)`.

    devamGerekli: resyncRequired || (hasMore && (changes.isNotEmpty || snapshot.isNotEmpty))

Yani `resyncRequired` D7/2'nin "bos sayfa donguyu durdurur" kuralindan **MUAF**. Sonsuz dongu
korumasi AYRI bir bayrakla (`resyncZincirlendiMi`): resync bir tur icinde **en fazla bir kez**
zincirlenir; sunucu art arda `true` donse bile ikinci ek istek atilmaz.

## Olcumler (hepsi denetci tarafindan KOSULDU)

| kosum | kaynak | sonuc |
|---|---|---|
| `flutter test test/g1_cekme_yolu_kapisi_test.dart` | temiz | 🟢 **16/16** |
| **MUTANT M5** (`resyncRequired \|\|` cikarildi) | mutant | 🔴 **G7 DUSTU**, G9 DUSTU, **G8 YESIL KALDI** |
| kararlilik ×3 (geri alim sonrasi) | temiz | 🟢 16/16 · 16/16 · 16/16 |
| `flutter analyze` (yalniz dokunulan iki dosya) | temiz | 🟢 **No issues found** |

**M5'te G8'in yesil kalmasi kritik:** G7'nin olumu bu duzeltmeye OZGU, genel bir kirilma degil.
D7/2'nin asil kurali (resyncRequired:false + bos sayfa ⇒ dongu durur) korunuyor.

**G9 de M5'te dustu** — beklenen: zincirin TAM BIR KEZ olmasini sinar, duzeltme yoksa hic zincir
olmaz. Kusur degil, kapinin duyarliligi.

## Kapilarin niteligi (denetci okumasi)

- **G7** dize degil **URUN UCU** sinar: ikinci istegin `sinceCursor` TASIMADIGI **ve** donen
  snapshot'in yerel `gorevler` tablosunda satir DOGURDUGU dogrulanir.
- **G8** pozitif kontrolun esi: bu satir olmasa G7 sahte gecerdi.
- **G9** kosum SAYISINI mekanik olarak sinar.

## Not — geri alim yontemi

Mutant ve geri alim `edit_block` ile yapildi (dosyaya TAZE zaman damgasi yazar). o89'da
`Copy-Item` ile geri alim zaman damgasini KORUMUS, derleyici yeniden derlememis ve iki "temiz"
kosum aslinda mutantli ikiliyi calistirmisti. Bu turda o tuzaga girilmedi.

## KALAN

Duzeltme **canli olarak dogrulanmadi**: §F adim 6, yeni APK ve TAZE hesap ciftiyle yeniden
kosulacak (mevcut a@/b@ hesaplari artik uye oldugu icin bu olcume uygun DEGIL).

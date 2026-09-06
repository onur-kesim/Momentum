# §F ADIM 6 — IKINCI TUR (o86-E duzeltmesinden SONRA), 6 Eyl 2026

**SONUC: 🟢 GECTI. §F artik 9/9.**

Taze kosum: yeni APK (o86-E duzeltmesiyle derlendi, 11:08:14, 222,8 sn), her iki cihaza
**kaldirilip yeniden kuruldu** (temiz durum), ve **TAZE hesap cifti** kullanildi --
`d@vitrin.test` (A2, gercek telefon) / `e@vitrin.test` (B2, emulator). Eski `a@`/`b@` cifti bu
olcume UYGUN DEGILDI (B artik uyeydi, imleci davetten sonraydi).

## Sira ve olcum

1. A2 telefonda kayit olur.
2. B2 emulatorde kayit olur ⇒ **B2'nin imleci BURADA sabitlenir** (davetten ONCE).
3. A2 "Proje Y" listesini ve `gorev-y1` gorevini yaratir.
4. **NEGATIF TABAN:** B2'nin Drawer'i acilir -- yalniz "Gelen Kutusu" + "Yeni liste",
   **Proje Y YOK** (`ui-T2-B2-03-DAVETTEN-ONCE.xml`).
5. B2'nin Drawer'i **ACIK BIRAKILIR** -- bundan sonra B2'ye HICBIR dokunus yapilmaz.
6. A2 `e@vitrin.test`i davet eder.

| olcum | deger |
|---|---|
| DAVET ANI | **11:33:51.094** |
| B2'de "Proje Y" GORULDU | **11:34:01.216** |
| GECEN SURE | **≤10 sn** |
| B2'de elle yenileme / ikinci tetik | **YOK** (Drawer zaten acikti, yalniz okundu) |

`ui-T2-B2-04-DAVETTEN-SONRA-10sn.xml`: Drawer'da **Proje Y** + "Liste secenekleri" var.

## Backfill de dogrulandi

B2 Proje Y'ye girdiginde **`gorev-y1`** goruyor (`ui-T2-B2-05-projey-icinde.xml`) -- bu gorev
davetten ONCE yaratilmisti. Yani resync yalnizca proje adini degil, **snapshot'in tamamini**
getirdi. o86-D2 + o86-E zinciri uctan uca kapandi.

## Sunucu teyidi

    select u.email, h.horizon_xid::text from user_resync_horizon h join users u on u.id=h.user_id
    b@vitrin.test | 1719   (birinci tur, 6 Eyl sabah)
    e@vitrin.test | 1743   (ikinci tur, bu kosum)

D2 her iki turda da resync borcunu yazdi.

## Karsilastirma — duzeltme oncesi/sonrasi

| | ONCE (o86-E'siz) | SONRA (o86-E ile) |
|---|---|---|
| Davetten sonra tek basina | **belirmedi** (8+ dk beklendi) | **≤10 sn belirdi** |
| Gerekli ikinci tetik | A'nin baska bir degisikligi | **YOK** |

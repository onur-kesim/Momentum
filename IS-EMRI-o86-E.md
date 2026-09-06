# IS-EMRI-o86-E — §F ADIM 6: resync yaniti donguyu durduruyor (ISTEMCI-ONLY)

`MOD: NORMAL` · Yazan: Cowork (oturum 90, §F canli vitrin turu) · Uygulayan: Claude Code
**Kilit K-o90/1 [Onur, 6 Eyl]:** K-o88/5'in `src/client DEGISMEZ` kilidi **YALNIZ BU KUSUR ICIN
ACILDI**. Kapsam DAR: `src/client/lib/veri/senkron_dongusu.dart` + onun testi.
🔴 **`src/backend` DEGISMEZ** (sunucu ayagi canli dogrulandi, dokunulmaz).

## 1. KUSUR — CANLI OLCULDU (KANIT/o86F)

§F adim 6 DUSTU: A davet ettikten sonra B'nin Drawer'inda "Proje X" **belirmedi**. Sunucu
dogru, sinyal ulasti, tur tamamlanmadi:

1. **Sunucu dogru:** `user_resync_horizon` B icin `horizon_xid=1719` (davetin txn xid'i);
   B kimligiyle davet-oncesi imlecle pull -> `{"resyncRequired":true,"changes":[],"snapshot":null}`
   (`KANIT/o86F/P-B-pull-davet-sonrasi.json`).
2. **Sinyal ULASTI:** emulator logcat, davet aninda
   `[sinyal] Changed alindi -- SinyalDegisiklik yayinlaniyor`.
3. **Tur tamamlanmadi:** API gunlugunde davet aninin ARDINDAN hicbir `/v1/sync` cagrisi YOK.
4. **Sebep tek satir** (`senkron_dongusu.dart`, tur sonu):

       // D7/2: bos sayfa hasMore'a BAKILMAKSIZIN donguyu durdurur.
       return hasMore && (changes.isNotEmpty || snapshot.isNotEmpty);

   `resyncRequired` yaniti **tanimi geregi bostur** (bayrak-only: `changes:[]`, `snapshot:null`).
   Imlec siliniyor (`_mevcutCursorJson = null`) ama D7/2 donguyu DURDURUYOR ⇒ `sinceCursor`suz
   ikinci istek **bir sonraki tetigi** bekliyor.
5. **Ispat:** A ikinci bir degisiklik yapinca (`tetik-2`, 10:22:22) Proje X B'de **10:22:34**'te
   belirdi. Mekanizma saglam; zincirde BIR HALKA eksik.
6. **xmin hipotezi ELENDI:** olcum aninda `pg_snapshot_xmin=1722 > 1719`.

## 2. DUZELTME

Tur sonu donusu, resync yanitini D7/2'nin "bos sayfa durdurur" kuralindan **MUAF** tutar:

    return resyncRequired || (hasMore && (changes.isNotEmpty || snapshot.isNotEmpty));

🔴 **SONSUZ DONGU KORUMASI PAZARLIKSIZ:** resync **bir tur icinde EN FAZLA BIR KEZ** zincirlenir.
Gerekce: imlec silindikten sonraki istek `sinceCursor`suz gider ve snapshot yoluna duser, sunucu
orada `resyncRequired:false` doner -- yani normalde tek ek tur yeter. Ama sunucu bir sekilde
tekrar `true` donerse dongu SONSUZA gitmemeli. Bicimi Claude Code secer (tur-basi bayrak vb.);
kriter davranistir, satir sayisi degil.

## 3. KAPILAR

**G7 (YENI, PAZARLIKSIZ):** sahte ag `resyncRequired:true` (bos govde) donunce, **AYNI dongu
cagrisinda** ikinci bir istek gider, o istek `sinceCursor` TASIMAZ, ve donen snapshot UYGULANIR
(varlik yerel depoda gorunur). Bugun bu kapi KIRMIZI yanmali; duzeltmeyle yesile donmeli.

**G8 (NEGATIF):** `resyncRequired:false` + bos sayfa -> dongu ESKISI GIBI durur (D7/2 korunur).
Bos liste her iddiayi gecirir; bu satir olmadan G7 sahte gecerdi.

**G9 (SONSUZ DONGU):** sahte ag HER yanitta `resyncRequired:true` dondururse dongu **durur**
(zincir en fazla bir kez). Kosum sayisi mekanik olarak sinanir.

**KIRILMAMASI SART (mevcut kapilar):** `g1_cekme_yolu_kapisi_test.dart:230` (D7) ve
`g3_kuyruk_kapisi_test.dart:463` (D6) -- ikisi de imlec silinmesini sinar, YESIL KALMALI.

**MUTANT M5:** duzeltme geri alinir (`resyncRequired ||` cikarilir) ⇒ **G7 DUSMELI**, G8 yesil kalmali.

**KARARLILIK:** ilgili testler **art arda UC KEZ** yesil; trx'ler `KANIT/o86E/trx/`e duser.

## 4. DUR NOKTALARI

1. `src/backend` altinda tek satir gerekirse **DUR**.
2. `senkron_dongusu.dart` disinda bir URUN dosyasi degismesi gerekirse **DUR** (test dosyasi haric).
3. Uc kararlilik kosumundan biri kirmizi olursa **DUR**, ham cikti ile don.
4. Geri alim/mutant turundan sonra **`LastWriteTime` tazelenmeden** olcum yapma: `Copy-Item`
   zaman damgasini korur, MSBuild/Flutter yeniden derlemez, mutantli ikili canli kalir
   (o89'da iki kez isirdi).

## 5. TESLIM

`flutter analyze` (yalniz dokunulan dosyada -- sinir 27: `dart format lib/` YASAK) + ilgili
testler + M5 + uc kararlilik kosumu → sonuc Cowork'e doner, bagimsiz denetim ondan sonra.
Ardindan **§F adim 6 CANLI yeniden kosulur** (APK yeniden derlenir, iki cihaza kurulur).
**COMMIT ETME, PUSH ONUR'DA.**

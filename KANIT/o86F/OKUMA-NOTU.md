# KANIT/o86F — §F VITRIN canli turu (6 Eyl 2026, oturum 90)

Ortam: gercek telefon (A, `fba69c15`, model 2510ERA8BG) + tek emulator (B, `tuzak_api34`) +
tarayici/protokol (C). Backend `docker compose up --build` ile ayakta; TEK APK
`SENKRON_SUNUCU_URL=http://localhost:5298`, iki cihazda da `adb reverse tcp:5298 tcp:5298`.
APK olculdu: `com.momentum.client`, **versionName 1.0.0 / versionCode 1** (release oncesi
1.1.0+2 yapilmali -- SURUM-NOTU-v1.1.0 H1), ABI arm64-v8a + armeabi-v7a + x86_64.

Hazirlik dikisleri: `momentum-migrator` exited 0 · `GET /health/ready` **200** ·
`POST /v1/sync` basliksiz **401** (deny-by-default).

## Adim adim sonuc

| adim | ne | sonuc |
|---|---|---|
| 3 | A ve B kayit olur | 🟢 |
| 4 | A: "Proje X" + `gorev-1` | 🟢 |
| 5 | A -> Paylas -> b@vitrin.test | 🟢 sunucuda `project_members` + `project_access` satiri dogdu |
| **6** | **B'nin Drawer'inda Proje X KENDILIGINDEN belirir** | 🔴 **DUSTU** |
| 7 | B `gorev-2` ekler -> A'da belirir | 🟢 **<=12 sn**, elle yenileme YOK |
| 8 | Davet edilmemis C, Proje X'i GORMEZ | 🟢 gercek JWT ile `"snapshot":[]` |
| 9 | Uye B paylasmaya calisir -> reddedilir | 🟢 UI: *"Bu listeyi paylasma yetkin yok. (RejectedForbidden)"*, uyelik DEGISMEDI |

## 🟢 o86-D2 UCTAN UCA DOGRULANDI

Sunucu ayagi canli olculdu: `user_resync_horizon` B icin `horizon_xid=1719, seq=0` (davetin txn
xid'i) · B kimligiyle davet-oncesi imlecle pull -> `{"resyncRequired":true,"changes":[],"snapshot":null}`
(`P-B-pull-davet-sonrasi.json`). Ve UI'da: B, projeye girdiginde **davetten ONCE yaratilmis
`gorev-1`'i goruyor** (`ui-B-08-projex-icinde.xml`). Katilim-sonrasi gecmis calisiyor.

## 🔴 ADIM 6 -- kok neden OLCULDU (istemci)

Sunucu dogru, sinyal ulasiyor, tur tamamlanmiyor:

1. Emulator logcat: `[sinyal] el sikisma basarili` (baglanti) ve davet aninda
   `[sinyal] Changed alindi -- SinyalDegisiklik yayinlaniyor` -> **sinyal B'ye ULASTI**.
2. API gunlugu: davet aninin (`07:18:21Z`) ARDINDAN hicbir `/v1/sync` cagrisi YOK.
3. `senkron_dongusu.dart` tur sonu: `return hasMore && (changes.isNotEmpty || snapshot.isNotEmpty);`
   (D7/2 "bos sayfa donguyu durdurur"). `resyncRequired` yaniti **tanimi geregi bostur**
   (bayrak-only) ⇒ imlec silinir ama **dongu durur**, ikinci tur bir sonraki tetigi bekler.
4. KANIT: A ikinci bir degisiklik yapinca (`tetik-2`, 10:22:22) B'nin Drawer'inda Proje X
   **10:22:34'te** belirdi -- yani 12 sn, ama SADECE ikinci tetik sayesinde.

**xmin hipotezi ELENDI:** olcum aninda `pg_snapshot_xmin=1722 > 1719` (davetin xid'i), yani
dispatcher davet satirini gorebiliyordu.

## Yan kazanim -- SURUM-NOTU H3 cevaplandi

C, `Authorization: Bearer <JWT>` ile kayit olup snapshot cekti (HTTP 201 + 200). Paketlenmis
demoda kimlik yolu **gercek JWT**; `DEV_USER_ID` define'i olmadan derlenen APK ile iki AYRI
hesap sorunsuz calisti.

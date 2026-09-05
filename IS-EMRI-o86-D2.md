# IS-EMRI-o86-D2 — SINIR KUSURU: `since == horizon` resync'i TETIKLEMIYOR

`MOD: NORMAL` · Yazan: Cowork (oturum 89 bagimsiz denetimi, 5 Eyl 2026) · Uygulayan: Claude Code
**Kilit K-o89/5 [Onur, 5 Eyl]:** A sikki — kullanici-horizonu kolunda **kapsayici esik**.
**SUNUCU-ONLY.** `src/client/**` DEGISMEZ. `ResyncPolicy` saf fonksiyonu DEGISMEZ.

## 1. KUSUR — OLCULDU (o86-D KABUL EDILMEDI)

`IS-EMRI-o86-D` uygulandi, kod is emrine sadik, mimari kapi temiz. Ama kapi **temiz kaynakta
DUSUYOR**. Ayni kaynak (sha256 `9B2D5C5B…FCE8` / `EABCC969…4BF1`), bes kosum:

| # | kaynak | sonuc | kanit |
|---|---|---|---|
| 1 | temiz | 🟢 1/1 | — |
| 2 | temiz | 🟢 1/1 | `KANIT/o86D/trx/00-TEMIZ-ONCE.trx` |
| 3 | MUTANT M2 (kullanici-horizonu kolu silindi) | 🔴 | `M2-kullanici-horizonu-silindi.trx` |
| 4 | MUTANT M1 (horizon yazimi kapatildi) | 🔴 | `M1-horizon-yazilmaz.trx` |
| 5 | temiz — sha256 yedekle BIREBIR | 🔴 | `99-TEMIZ-SONRA.trx` |
| 6 | temiz (tekrar) | 🔴 | `g1.ResyncRequired should be True but was False` |

Mutantlar OLDU ⇒ kapi SAHTE DEGIL. Kirmizi, **urunun sinir davranisidir**:

- Horizon **tam sinira** yaziliyor: `pg_current_xact_id()`, `seq = 0`.
- Uyenin imleci snapshot'tan gelir: `(pg_snapshot_xmin, 0)`.
- Bu ikisi **ESIT olabiliyor** (testin kendi yorumu: *"olculdu: 753==753"*).
- `ResyncPolicy.ShouldResync` kesin kucuktur (`since < horizon`) ⇒ esitlikte **TETIKLEMIYOR**
  ⇒ o86-D'nin kapattigi kusur AYNEN geri geliyor.

🔴 **Builder bunu gormus ama URUNU degil TESTI uyarlamis:** teste iki yapay adim eklenmis
(uyenin kendi push'u + owner'in "ara op"u) — ikisi de xid sayacini ilerletmek icin. O adimlar
cakismayi HER ZAMAN engellemiyor. Kapinin kendini zayiflatmasi sinifidir.

## 2. DUZELTME (K-o89/5)

`SyncPuller.ShouldResyncAsync`te iki horizonun **max**'ini alip tek esige sokan mantik KALKAR —
iki horizonun **esik semantigi farklidir**:

- **GC horizonu:** bugunku gibi kalir → `ResyncPolicy.ShouldResync(since, gc)` (kesin kucuktur).
- **Kullanici horizonu:** `since <= userHorizon` (KAPSAYICI).
- Sonuc: ikisinin **VEYA**'si. `g > u ? g : u` karsilastirmasi ve `SyncCursor` uzerindeki `>`
  bagimliligi bu satirdan KALKAR.

Gerekce: daveti ZATEN gormus bir istemcinin imleci `(X, seq>=1)` olur ⇒ `since <= (X,0)` FALSE ⇒
gereksiz resync YOK. Imleci tam `(X,0)` olan istemci bir kez fazladan snapshot alir — zararsiz.
Bu cozum **olculmemis hicbir varsayima yaslanmaz** (B sikki `server_seq`in 1'den basladigini
varsayardi; o yuzden REDDEDILDI).

## 3. KAPILAR

**G6 (YENI, PAZARLIKSIZ):** davet, uyenin imlecini sabitleyen snapshot'tan **HEMEN SONRA** —
araya HICBIR commit girmeden — gelirse de `resyncRequired:true` doner. Bu, bugun kirmizi olan
tam senaryodur ve §F adim 6'nin ta kendisidir (B kaydolur, A hemen davet eder).

**Mevcut G1-G5 testinden İKİ YAPAY ADIM KALDIRILIR:**
- owner'in "ara op"u (`color = mavi`) **SILINIR** — sirf xid ilerletmek icin konmustu.
- uyenin kendi push'u KALIR (kendi gorevinin snapshot'ta olmasi gercek bir pozitif kontroldur),
  ama artik xid ilerletme GEREKCESI yorumdan CIKARILIR.
- Kaldirildiktan sonra G1-G5 **yesil kalmali**. Kalmiyorsa duzeltme yetersizdir.

**M4 MUTANT:** kullanici kolundaki `<=` tekrar `<` yapilir ⇒ **G6 DUSMELI.**

🔴 **KARARLILIK SARTI:** tek yesil kosum KANIT DEGILDIR — kararsizlik bu oturumda OLCULDU.
Test **ART ARDA UC KEZ** kosulur ve **UCUNDE DE** yesil olur; uc trx de `KANIT/o86D/trx/`e duser
(`01-KARARLILIK-1/2/3.trx`). Bir tanesi bile kirmizi ise **KABUL YOK**.

## 4. DUR NOKTALARI

1. `ResyncPolicy` saf fonksiyonu ya da `ClampAndResyncProperties` **degismek zorunda kalirsa DUR** —
   kilit, degisikligin saf politikaya SIZMAMASIDIR.
2. `src/client` altinda tek satir gerekirse **DUR**.
3. Uc kararlilik kosumundan biri kirmizi olursa **DUR** ve ham cikti ile don — yeniden kosma.

## 5. TESLIM

`verify` zinciri + G1-G6 + M4 + uc kararlilik kosumu → sonuc Cowork'e doner, bagimsiz denetim
ondan sonra. **COMMIT ETME, PUSH ONUR'DA.**

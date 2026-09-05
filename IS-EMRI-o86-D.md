# IS-EMRI-o86-D — katilim-sonrasi gecmis (backfill) · SUNUCU-ONLY

`MOD: NORMAL` · Yazan: Cowork (oturum 89, 5 Eyl 2026) · Uygulayan: Claude Code
**Kilit K-o89/1 [Onur, 5 Eyl]:** A sikki — uyelik aninda resync borcu.
**Kilit K-o89/2 [Cowork olcumu, ayni gun]:** borcun BICIMI tuketilen bayrak DEGIL,
**kullanici-basina resync horizon'u** (mevcut GC horizon politikasinin birebir deseni).
Gerekcesi §2.3'te; tuketilen bayrak coklu cihazda sessiz kaybediyordu.

## 0. KAPSAM VE DEGISMEZLER

- **SUNUCU-ONLY.** `src/client/**` DEGISMEZ (K-o88/5'in devami).
- Tel sozlesmesi (`SyncContracts.cs`) DEGISMEZ — `ResyncRequired` alani ZATEN VAR ve
  ZATEN URETIMDE kullaniliyor (asagida olculdu).
- `git add -A` YASAK → yol belirt. Commit mesajinda cift tirnak yok. **PUSH ONUR'DA.**
- ADR/spec YAZILMAZ (ISLEYIS md.4). Yeni kapi DOSYASI acilmaz (CLAUDE.md §4/10) —
  kapilar mevcut test dosyalarina girer.
- Bu emirde SAYISAL SINIR YOK (satir/dosya adedi). Kriter tektir: **SAHTE YOK.**

## 1. KUSUR — OLCULDU, IDDIA DEGIL

`SyncPuller.cs:33` `PullIncrementalAsync` sorgusu (satir 57):

    AND (commit_xid, server_seq) > (@sinceXid::xid8, @sinceSeq)

Erisim kumesi (`project_access`) sorgu ANINDA dogru degerlendiriliyor; kusur orada DEGIL.
Kusur: bir scope'a SONRADAN erisim kazanan kullanicinin imleci, o scope'un gecmisinin
ILERISINDE oldugu icin o gecmis suzgecten HIC gecmez ve ASLA gelmez.

Canli kanit (KANIT/o86C, B2 kimligi, ayni backend, tek fark `since`):
- P1 `since=NULL` → snapshot'ta Proje X + gorev-1 + iki uye TAM (pozitif kontrol)
- P2 `since={xid:1703,seq:414}` → `{"changes":[],"snapshot":null,"resyncRequired":false}`

## 2. NEDEN BU COZUM

### 2.1 DEVIR-88'in bir iddiasi CURUDU — bunu bilerek calis

Devir notu "sunucu `ResyncRequired`'i HIC KURMUYOR, uretimde true yazan TEK SATIR YOK,
olu bayrak" diyor. **Yanlis.** Olculdu:
- `SyncCommandHandler.cs:85` → `resyncRequired = true;` URETIM kodunda, dali
  `_puller.ShouldResyncAsync(since, ct)` (satir 84) suruyor.
- `SyncPuller.cs:19-31` → `ShouldResyncAsync` YALNIZ `sync_gc_state` horizonuna bakar;
  horizon NULL ise **daima false**. Yani bayrak olu degil, **sebebi eksik**.
- Istemci dali olu degil, TESTLI: `g1_cekme_yolu_kapisi_test.dart:230` (D7) ve
  `g3_kuyruk_kapisi_test.dart:463` (D6) — imlec siliniyor, sonraki istek `sinceCursor`siz.

### 2.2 Bagirsak: yeni dal yok, sebep ekleniyor

`ShouldResyncAsync` uyelik degisimini de bir resync sebebi sayacak. Handler'a YENI DAL
girmez; yalnizca cagriya `actorId` eklenir.

### 2.3 Neden "tuketilen bayrak" DEGIL

Bayrak bir kez true donup temizlenseydi: ayni kullanicinin IKINCI cihazi (kendi eski
imleciyle) bayragi ALAMAZ ve o scope'un gecmisini KALICI kaybeder. Ayrica "bayragi
dondurunce mi, snapshot teslim edilince mi temizlensin" yarisinin dogru cevabi yok —
istemci cokerse gecmis kaybolur. Horizon bicimi ikisini de ortadan kaldirir: horizon
TUKETILMEZ, bir esiktir; imleci esikten eski olan HER istemci resync olur, yeni olan olmaz.

## 3. YAPILACAK — DORT DIKIS

### D1 · Migration: kullanici-basina resync horizonu

Yeni tablo `user_resync_horizon`: `user_id` (PK), `horizon_xid` (xid8), `horizon_seq` (bigint).
Deseni `sync_gc_state`in birebir aynisi, tek fark: tekil satir yerine kullanici-basina satir.
EF migration + `SyncDbContext`/`SyncConfigurations` kaydi, mevcut tablolarin desenine uyar.

🔴 **Kor kapi uyarisi:** `tests/Momentum.Persistence.Tests/SchemaTests.cs:22` tablo sayisini
sayiyor (taban `13 -> 14`, o86-A'da BILEREK guncellenmisti). Bu emirle **15** olur; taban
DEGISTIRILMEZSE CI kirmizi yanar ve sebebi bu emirle ilgisiz gorunur.

### D2 · `EntityMaterializer.ReplaceMembersAsync` (satir 142-159): kimin eristigi olculur

Bugun DELETE-then-INSERT var. Eklenecek: **DELETE'ten ONCE** o projenin mevcut uye kumesi
okunur; `eklenen = yeni_kume \ eski_kume`. `eklenen`in her uyesi icin `user_resync_horizon`
satiri **ayni op txn'inde** yazilir; deger = bu txn'in `pg_current_xact_id()`'si, seq = 0.

Pazarlıksiz sartlar:
- **Monotonluk:** mevcut satir GERI gitmez (yalniz ileri guncellenir).
- **Idempotans:** ayni op yeniden materyalize edilirse `eklenen` bos cikar, yeni borc DOGMAZ.
- **Sahip borc ALMAZ:** sahip OrSet'e yazilmaz (§C3), dolayisiyla `eklenen`e hic girmez —
  dogru davranis, sahibin erisimi hic kesilmedi.
- **Kaynak `members` op'un BEYANI degil, `ProjectProjection.From(...)`in urettigi GUNCEL
  kumedir** (sinir 38'in tam sinifi: op kendi beyanina bakarsa kacis dogar).
- Uyelikten CIKARMA bu emrin kapsaminda DEGIL (horizon degismez); hayalet-satir sinifi
  `old_scope_id` koluyla o86-A'da kapandi.

### D3 · `SyncPuller.ShouldResyncAsync` (satir 19-31): imza ve sebep genisler

Imza `(SyncCursor since, ...)` → `(Guid actorId, SyncCursor since, ...)`; `ISyncPuller.cs`
sozlesmesi de. Govde: GC horizonu (bugunku) VE kullanici horizonu okunur; ikisinden
**buyugu** etkin horizondur; ikisi de yoksa `false`. Karar yine `ResyncPolicy.ShouldResync`
saf fonksiyonuna verilir — **politika fonksiyonu DEGISMEZ.**

🔴 **Mimari kapi:** `ArchitectureRuleTests.cs:102` — `SyncPuller.cs` KAYNAGINDA
`"project_members"` dizgesi GECEMEZ. Yeni sorgu bu kurali bozmamali (tablo adi
`user_resync_horizon`; uyelik tablosuna dokunulmaz).

`SyncCommandHandler.cs:84` cagriya `command.ActorId` eklenir — **yeni dal yok.**

### D4 · Cagiranlar (bugun olculdu: SADECE ikisi)

- `SyncCommandHandler.cs:84`
- `tests/Momentum.Persistence.Tests/TestSupport.cs:108-111` (sarmalayici)

Baska bir cagiran cikarsa → **DUR** (§6/3).

## 4. KAPILAR — her POZITIF'in yaninda bir NEGATIF (bos liste her iddiayi gecirir)

Hepsi `Momentum.Persistence.Tests` icindeki MEVCUT dosyalara girer; yeni kapi dosyasi yok.
`MaterializationRoundTripTests.cs:165`in add/remove deseni ornektir.

| # | Tip | Sart |
|---|---|---|
| G1 | POZITIF | B2 uye olmadan once `since=NULL` alip imlecini sabitler; SONRA uye yapilir; B2'nin `since`li pull'u `resyncRequired:true` doner (bayrak-only: `changes` bos, `snapshot` null) |
| G2 | POZITIF | Ardindan `since=NULL` pull'u Proje X + gorev-1 + iki uyeyi ICERIR (P1'in birebir ciktisi) |
| G3 | NEGATIF | Uyelik HIC degismemisken ayni `since`li pull `resyncRequired:false` doner ve degisiklikler normal akar |
| G4 | NEGATIF | G2'den sonra ILERLEMIS imlecle ucuncu pull yine `false` — horizon yapiskan degil |
| G5 | POZITIF | Ayni kullanicinin IKINCI eski imleci de `true` alir. **Tuketilen bayrak olsaydi bu kapi duserdi** — K-o89/2'nin varlik sebebi |
| G6 | MIMARI | `ArchitectureRuleTests` yesil kalir: `SyncPuller.cs` kaynaginda `project_members` GECMEZ |

**Mutantlar (kapi olmeden kabul YOK):**
- **M1:** D2'deki horizon yazma satiri silinir → **G1 DUSMELI.**
- **M2:** D3'te kullanici-horizonu kolu silinir (yalniz GC horizonu kalir) → **G1 DUSMELI**,
  ve `ClampAndResyncProperties.cs:74` **YESIL KALMALI** (saf politika bozulmadi — bu ikinci
  sart, duzeltmenin politikaya sizmadiginin kanitidir).
- **M3:** horizon `pg_current_xact_id()` yerine sabit/eski bir degere yazilir → **G1 DUSMELI.**

## 5. KANIT — `KANIT/o86D/`

o86-C'nin birebir deseni: **ayni backend, tek fark `since`.** Ham HTTP govdeleri dosyaya
yazilir, ozet DEGIL. En az: P1 (pozitif kontrol, yeniden kosulur) · P2 (kusurun kendisi,
duzeltme ONCESI) · P3 (duzeltme SONRASI ayni istek) · mutant ciktilar.
`KANIT/slice-3c/02-G2/*.json` commit'e GIRMEZ (sinir 19) — `git add` yol belirterek.

## 6. DUR NOKTALARI (olcumu bildir, tahminle doldurma)

1. `xid8` uzerinde karsilastirma/`GREATEST` calismazsa **DUR** — M1 sinifi: xid8'in bigint
   cast'i yok, metin uzerinden `::xid8` dokulur.
2. `SchemaTests` tabanini 15'e cikarirken beklenmeyen bir tablo farki gorulurse **DUR.**
3. `ShouldResyncAsync`in §D4'te sayilan ikisi disinda bir cagirani cikarsa **DUR.**
4. Kapi butcesi (`araclar/` ÷ `src/` ≤ %10) bu emirle asilacaksa **DUR** — yeni kapi
   dosyasi acilmaz, kural tek cumleye doner.

## 7. BILINEN BEDEL (kabul edildi, DURUM'a sinir olarak yazilir)

Her yeni uyelik, o kullaniciya bir kez **TAM snapshot** indirtir (scope-hedefli backfill
DAHA ucuz olurdu ama yeni sozlesme alani + yeni istemci dali ister; bu dilimde
`src/client` DEGISMEZ kilidi onu imkansiz kilar). Portfolyo olceginde kabul edilebilir.

## 8. TESLIM

Kod bitince: `verify` zinciri + ilgili testler + mutant turu → sonuc Cowork'e doner,
**bagimsiz denetim** (ISLEYIS md.4: canli artefakta bakilir, kagida degil) ondan sonra.
Commit mesaji ASCII; `IS-EMRI-o86-D.md` ve `KANIT/o86D/` de agaca alinir. **Push Onur'da.**

---

## 9. VİTRİN ORTAMI — 5 Eyl 2026'da ÖLÇÜLDÜ (o86-B §F yeniden koşulurken kullanılır)

Araclar PATH'te DEGIL, tam yolla cagrilir:

- `D:\Android\Sdk\platform-tools\adb.exe`  (VAR)
- `D:\Android\Sdk\emulator\emulator.exe`   (VAR)
- AVD'ler: **`tuzak_api34`** ve **`tuzak_api34_b`** — `D:\Android\avd\` altinda
- SDK koku `local.properties`ten: `sdk.dir=D:\Android\Sdk` · `flutter.sdk=C:\src\flutter`

🔴 **MAYIN A — `emulator -list-avds` BOS DONER ve hata VERMEZ.** AVD'ler varsayilan
`%USERPROFILE%\.android\avd`da DEGIL, `D:\Android\avd`da; `ANDROID_AVD_HOME` kabukta tanimli
degil (Android Studio kendi ayarindan bilir, ciplak kabuk bilmez). Calisan bicim:

    cmd /c "set ""ANDROID_AVD_HOME=D:\Android\avd"" && D:\Android\Sdk\emulator\emulator.exe -list-avds"
    -> tuzak_api34
       tuzak_api34_b

🔴 **MAYIN B — cmd'de `set VAR=deger && ...` degere SONDAKI BOSLUGU katar** ⇒ yol bozulur ve komut
yine **sessizce bos** doner (exit 0). Daima tirnakli yaz: `set "VAR=deger" && ...`. Bu mayin bu
oturumda bir kez isirdi ve "bos liste her iddiayi gecirir" sinifinin ta kendisidir.

**5 Eyl 15:20 durumu:** bagli cihaz YOK (`adb devices` bos) · `:5298` dinlenmiyor (backend kapali)
· iki emulator ayni anda ACILMIYOR (o88'de olculdu: commit limiti + sabit 6144 MB pagefile).
⇒ Vitrin plani K-o88/2: **gercek telefon + TEK emulator + `adb reverse`** (firewall/admin gerekmez).

Sira: backend ayaga (`/health/ready` 200) → telefon USB + `adb reverse tcp:5298 tcp:5298` →
tek emulator (`-avd tuzak_api34`) → APK ikisine de kurulur → §F adim 3'ten devam.

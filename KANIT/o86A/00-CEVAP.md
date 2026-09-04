# IS-EMRI-o86-A -- CEVAP (alti satir, s11)

## 1. §A'nin dort olcumu (ozellikle A1, A4)

**A1** (kod yazmadan ONCE, tek testle): `SyncCommandHandler.ReadProjectId` `entityType == "Task"`
kisa-devresi yuzunden bir `Project` op'unda `postProjectId`/`scope_id` **HER ZAMAN null**dur --
`entity.Fields.TryGetValue("projectId", ...)` **hic cagrilmaz** (istisna/yanlis-alan-okuma riski
YOK). Ham kanit: `01-olcum.txt` (test ONCE KIRMIZI dustu, sonra §B ile YESILE dondu) --
**tasarimin varsayimi DOGRULANDI**, "DUR ve bildir" tetiklenmedi.
**A2**: `project_members`, `task_tags`in **birebir** zincirinin tekraridir (hidratlama + `PresentElements()`
+ DELETE-ALL-REINSERT); yazma tarafi (`SyncStore.PersistSetAsync`) zaten `entityType`/`setName`
parametreli, **hicbir kod degisikligi gerekmedi**.
**A3**: `IngestResultCode` alti deger tasiyordu, hicbiri "yetkisiz" degildi -- **`RejectedForbidden`
eklendi**, `RejectedInvalid` ile AYNI "KAYDEDILMEZ" deseninde (gerekce FARKLI: uyelik zamanla degisebilir).
**A4**: istemci (`senkron_dongusu.dart` `_tekSonucIsle`) reddedilen HERHANGI bir kodu (yeni
`RejectedForbidden` dahil) `default:` kolunda ayni sekilde isler: satir `'zehirli'` durumuna
gecer, `_bekleyenleriSec()` suzgecinden DUSER, **bir daha OTOMATIK gonderilmez** -- **sessiz
sonsuz yeniden deneme YOK** (risk dogrulanmadi, olcum sonucu).

## 2. `project_members` son sutun/indeks listesi + `members`in canli durumu

Sutunlar: `project_id uuid` · `user_id uuid`, PK `(project_id, user_id)`.
Indeks: **`ix_project_members_user_project`** = `(user_id, project_id)` (pull sorgusu user_id'den gider).
Canli durum: `ProjectProjection.From` icinde `state.TryGetSet("members", out var memberSet)` ardindan
`memberSet.PresentElements()` (>=1 iptalsiz add-tag) -- her eleman `Guid.TryParse`den GECER, basarisizsa
satir YAZILMAZ ve `"members"` `MalformedFields`e girer (sessiz yutma yok).

## 3. Pull'un yeni yuklemi (birebir, uc kol) + EXPLAIN'in indeksi kullandigi

```
AND ( o.owner_id = @actorId
   OR o.scope_id     IN (SELECT project_id FROM project_members WHERE user_id = @actorId)
   OR o.old_scope_id IN (SELECT project_id FROM project_members WHERE user_id = @actorId) )
```
Hem `PullIncrementalAsync` hem `ReadOwnedEntitiesAsync` (snapshot) AYNI uc kolu tasir.
`06-explain-pull.txt`: `Sort Key: o.commit_xid, o.server_seq` (o84 dersi -- ORDER BY nitelikli,
golgelenmiyor); HER IKI alt-sorgu da **`Bitmap Index Scan on ix_project_members_user_project`**
kullaniyor (indeks GERCEKTEN calisiyor, sequential scan degil).

## 4. Yetki tablosunun kodda yeri + reddedilen op'un istemciye donen sonucu

`SyncCommandHandler.ProcessOpAsync` icinde, `PersistDeltaAsync`/`MaterializeAsync`den **ONCE**
(`entity` POST-op durumdadir, hedef scope BUNDAN okunur): `IsAuthorizedAsync` metodu §E'nin bes
satirlik karar tablosunu **birebir** uygular (varlik yeni -> KABUL; Task+scope null -> KABUL;
Task+scope P -> sahip/uye; Project+`members` -> **yalniz sahip**; Project+diger -> sahip/uye).
Reddedilen op, istemciye `applied[].code = "RejectedForbidden"` olarak doner (`effectiveOpHlc: null`,
`RejectedInvalid` ile ayni KAYDEDILMEME deseninde -- A3).

## 5. `POST /v1/users/lookup` 401/404/200 + `email_normalized` Turkce davranisi

401 (kimliksiz) · 404 (bilinmeyen e-posta) · 200 + yanit **YALNIZ** `{"userId": "..."}` (fazla
alan varsa test kirmizi olur, canli turda da dogrulandi). `email_normalized` = `Trim().ToLowerInvariant()`,
kultur-DUYARSIZ: **Turkce İ/ı katlamasi YOKTUR** -- `'İ'` (U+0130) `ToLowerInvariant()` altinda
duz ASCII `'i'` DEGIL, `'i'` + BIRLESIK NOKTA (U+0069 U+0307) uretir; ayni e-postanin ASCII `'i'`
varyaniyla arama BULUNAMAZ (404) -- gercek kayit+arama ile OLCULDU (`AuthEndpointTests.
UserLookup_email_normalized_Turkce_I_harflerini_KATLAMAZ`).

## 6. `verify.ps1` + test sayisi + `git status --porcelain -- src tests`

**EXIT 0** (`07-verify-ps1.txt`: build 0 uyari/0 hata, CVE gate temiz).
Test sayisi: **145 -> 151** (net +6 `[Fact]`; §A'nin A1 olcum testi zaten 145'in icinde
sayilmisti, G1 onun mutant-ispatidir -- toplam yeni METOD sayisi 7, ama A1 ayrica sayilmadigi
icin net fark 6 degil 7 -- `git diff 0058703 -- tests` `+.*\[Fact\]` sayisi: **7**).
`git status --porcelain -- src tests`:

```
 M src/backend/Momentum.Api/Program.cs
 M src/backend/Momentum.Application/Abstractions/Sync/IScopeMembershipSource.cs
 M src/backend/Momentum.Application/Abstractions/Sync/ISyncStore.cs
 M src/backend/Momentum.Application/Features/Sync/SyncCommandHandler.cs
 M src/backend/Momentum.Domain/Sync/Envelope/IngestResult.cs
 M src/backend/Momentum.Domain/Sync/Projection/ProjectProjection.cs
 M src/backend/Momentum.Infrastructure/Persistence/Configurations/SyncConfigurations.cs
 M src/backend/Momentum.Infrastructure/Persistence/Migrations/SyncDbContextModelSnapshot.cs
 M src/backend/Momentum.Infrastructure/Persistence/SyncDbContext.cs
 M src/backend/Momentum.Infrastructure/Persistence/SyncEntities.cs
 M src/backend/Momentum.Infrastructure/Sync/EntityMaterializer.cs
 M src/backend/Momentum.Infrastructure/Sync/ScopeMembershipSource.cs
 M src/backend/Momentum.Infrastructure/Sync/SyncPuller.cs
 M src/backend/Momentum.Infrastructure/Sync/SyncStore.cs
 M src/backend/Momentum.Infrastructure/Sync/TaskReadStore.cs
 M tests/Momentum.Persistence.Tests/AuthEndpointTests.cs
 M tests/Momentum.Persistence.Tests/D9OwnerIdVisibilityTests.cs
 M tests/Momentum.Persistence.Tests/DispatcherTests.cs
 M tests/Momentum.Persistence.Tests/MaterializationRoundTripTests.cs
 M tests/Momentum.Persistence.Tests/RealtimeMembershipTests.cs
 M tests/Momentum.Persistence.Tests/RestoreAndScopeTests.cs
 M tests/Momentum.Persistence.Tests/SchemaTests.cs
?? src/backend/Momentum.Api/Endpoints/UserLookupEndpoints.cs
?? src/backend/Momentum.Application/Features/Auth/LookupUserQuery.cs
?? src/backend/Momentum.Infrastructure/Persistence/Migrations/20260820215545_AddProjectMembers.Designer.cs
?? src/backend/Momentum.Infrastructure/Persistence/Migrations/20260820215545_AddProjectMembers.cs
```

**`src/client/**` GORUNMUYOR** (istemci degismedi, ayrica dogrulandi). `FieldStrategyRegistry.cs`
ve mevcut 5 migration **BIREBIR DEGISMEDI** (ayrica dogrulandi, `git diff --stat` bos).

**Beyan edilmiş gozlem (duzeltilmedi, kapsam disi -- tasarim BIREBIR uygulandi):** §E'nin "Varlik
yeni -> KABUL" kurali, BRAND-NEW bir Task'in DOGRUDAN baskasinin projesine (`projectId`) DOGMASINI
engellemez -- ilk-op istisnasi kasitli (F2'nin "ilk yazan sahip olur" bootstrap deseniyle ayni
sinif) ama teorik olarak disaridan bir aktorun tahmin ettigi bir projectId'ye yeni bir Task
enjekte edebilecegi anlamina gelir. Tasarim tablosu bunu KAPSAMIYOR (Cowork kilidi 20 Agu,
birebir uygulandi); genisletme bu emrin isi degil.

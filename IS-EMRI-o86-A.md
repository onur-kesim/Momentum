# İŞ EMRİ o86-A — DİLİM 3 / İŞBİRLİĞİ · SUNUCU AYAĞI

`MOD: KRİTİK` · kutu **21-24 Ağu 2026** · yazan: **Cowork** · koşacak: **Claude Code**
Öncül: `0058703` (DİLİM 2 kapalı, üç kapı `141a94e` ile yeşil).

🔴 **Neden KRİTİK:** bu dilim uygulamanın **ilk yetki yüzeyini** açıyor. Yanlış yazılırsa bir yabancı
başkasının verisini okur veya yazar. Anayasa §5 gereği teslim doğrulama turu **2**'dir. Onur bunu
NORMAL'e indirebilir; indirmedikçe iki tur koşulur.

Tasarım **kilitli** (Onur, 20 Ağu): üyelik = `Project.members` OrSet'i → `project_members`
izdüşümü · davet **e-posta** ile · yazma yetkisi **kapıya bağlanır (reddedilir)** · vitrin =
paylaş + çek + realtime. **Bu emir yalnız SUNUCU ayağıdır**; istemci `o86-B`'nin işi.

---

## 0. DEMİR KURALLAR

1. 🔴 **İSTEMCİ DEĞİŞMEZ.** `src/client/**`e dokunma. Bu dilimin kapısı **protokol seviyesinde**
   canlı turdur (`o85A` deseni), UI değil. İstemciyi bağlama dürtüsü olursa **DUR ve bildir**.
2. 🔴 **`FieldStrategyRegistry` DEĞİŞMEZ.** `Project.OrSets = Set("members")` **zaten kayıtlı**
   (ölçüldü, satır 177-181). Alan eklemek ADR 0002 K2-B2 kilidini açmaktır.
3. 🔴 **ADR/spec YAZILMAZ** (İŞLEYİŞ md.4). Tasarım bu emirdedir.
4. 🔴 **PII: OrSet'e e-posta YAZILMAZ.** `members` elemanı **userId**'dir. E-posta yalnız arama
   ucunda yaşar, CRDT'ye ve hiçbir cihaza kopyalanmaz (anayasa §8, amaçla sınırlılık).
5. **Yeni kapı DOSYASI açılmaz** (DURUM sınır 3). Testler mevcut `Momentum.Persistence.Tests` /
   `Momentum.Api.Tests` dosyalarına girer. Canlı tur betiği kapı dosyası değildir, `KANIT/o86A/`e girer.
6. Mevcut migration'lara dokunulmaz · `verify.ps1` · `DURUM.md` · `CLAUDE.md` · `arsiv/` ·
   `.github/workflows/*`: **dokunma**. **PUSH ONUR'DA.**

---

## 1. NEREDEYİZ (Cowork ölçtü, 20 Ağu — kaynak dosyalardan)

**Hazır olan (işbirliğinin pahalı yarısı bitmiş):**
- `SyncCommandHandler` her op'a `scope_id` yazıyor: `scopeId = TryScope(postProjectId)`,
  `oldScopeId = TryScope(preProjectId)` (satır 175-194); `TryScope` = `projectId` alanını Guid'e çevirir (satır 211).
- `OutboxRecord` `ScopeId`/`OldScopeId` taşıyor; `owner_id`/`actor_id` **kimlik doğrulamadan** gelir (D9/F5).
- `SyncHub` scope-aware: her bağlantıda `scope:{id}` gruplarına katıyor, **hiç önbelleklemeden**.
- `sync_orset_tags` şeması: `(entity_type, entity_id, set_name, element, add_tag)` PK + `hlc` (collation C) + `cancelled`.
- `users` tablosu: `id · email · email_normalized (collation C) · password_hash · created_at`.

**Eksik olan (bu emrin işi):**
- 🔴 **Pull'un İKİ yolu da OWNER-ONLY:** `PullIncrementalAsync` → `AND owner_id = @actorId` (satır 41);
  `SnapshotAsync → ReadOwnedEntitiesAsync` → `WHERE owner_id = @actorId` (satır 92).
  *(DURUM md.35 yalnız artımlıyı yazıyordu; snapshot da aynı durumda — bu ölçüm yeni.)*
- 🔴 **`ScopeMembershipSource` sahte üyelik:** `SELECT DISTINCT scope_id FROM outbox_messages WHERE
  owner_id = @userId` — "bu kullanıcının şimdiye kadar İÇİNE YAZDIĞI her scope". Kendi belgesinde
  itiraf ediliyor: hiç yazmamış salt-okunur işbirlikçi hiçbir gruba katılamaz; yetkisi geri alınan
  eski scope'u görmeye devam eder. **Gerçek üyelik tablosu yok.**
- 🔴 **Yazma yetkisi YOK:** `ProcessOpAsync` yazanın o varlığa yazma hakkı olup olmadığını
  **hiç sormuyor**; `owner_id`yi yazana yazıyor. Sonucu ölçüldü (o85-B D2 testi): B, A'nın projesinin
  adını değiştirebiliyor — ve pull owner-only olduğu için **A bunu asla çekemiyor**. Yani bugün
  materyalize satır ile hiçbir istemcinin çekebildiği durum **AYRIŞMIŞ** hâlde.
- `project_members` tablosu YOK · `members` OrSet'i materyalize EDİLMİYOR (o85-B'de bilerek dışarıda).

**ÖLÇÜLEMEDİ (köprü iki kez düştü — §2'nin ilk işi):** `postProjectId`in bir **`Project`** tipi op'ta
gerçekten `null` olup olmadığı. Tasarım bunun null olduğu **varsayımına** dayanıyor; §2/A1 doğrulayacak.

---

## 2. §A — ÖLÇÜM (🔴 İLK İŞ, KOD YAZMADAN)

Dördü de ham çıktıyla `KANIT/o86A/01-olcum.txt`e:

**A1.** `Project` tipi bir op'ta `postProjectId` (ve dolayısıyla `scope_id`) **null** mu? Kaynaktan
oku + tek bir testle göster. **Null değilse DUR ve bildir** — §B'nin tasarımı çöker.
**A2.** `task_tags` materyalizasyonu OrSet'in canlı durumunu **nasıl** hesaplıyor (`sync_orset_tags`
+ `sync_orset_removes` + `cancelled` üçlüsü)? `project_members` **onun birebir deseni** olacak;
yeniden icat etme.
**A3.** `IngestResult` bugün hangi sonuçları taşıyor (`RejectedInvalid` var, satır 148 "ERRATA"
diyor)? Yetkisiz op için **yeni bir sonuç mu** gerekiyor yoksa mevcut biri mi yeterli?
**A4.** İstemci reddedilen op'a ne yapıyor — kuyrukta sonsuza dek yeniden mi deniyor? 🔴 Bu, bu
dilimin en sinsi riski: **sessizce sonsuz yeniden deneme**. Yalnız ÖLÇ, düzeltme `o86-B`'nin işi;
ölçüm sonucu CEVAP'a yazılır.

---

## 3. §B — `Project` op'ları scope taşır

`SyncCommandHandler.BuildOutbox`: `entityType == "Project"` ise **`ScopeId = wireOp.EntityId`**
(proje scope'un **kendisidir**), `OldScopeId = null` (bir projenin scope'u değişmez).
Diğer tipler bugünkü davranışta kalır.

🔴 **Neden pazarlıksız:** davet op'u (`members` yazımı) bir `Project` op'udur. Scope taşımazsa ne hub
yayar ne pull taşır ⇒ **davet edilen kişi davet edildiğini asla öğrenemez**. Bootstrap'ı istemcinin
CRDT durumu değil, sunucunun materyalize ettiği `project_members` çözer.

---

## 4. §C — `project_members` materyalizasyonu

**C1.** Migration `AddProjectMembers` (yalnız `CreateTable` + `CreateIndex`):
`project_members(project_id uuid, user_id uuid)`, PK `(project_id, user_id)`,
indeks `ix_project_members_user_project` = `(user_id, project_id)` — **pull sorgusu `user_id`den gider**.

**C2.** `EntityMaterializer`'ın `case "Project"` dalı genişler: `members` OrSet'inin **canlı durumu**
`project_members`e izdüşürülür (A2'de ölçülen `task_tags` deseni). Eleman **userId** olarak yorumlanır;
`Guid.TryParse` başarısızsa satır **YAZILMAZ** ve `malformed_fields`e girer (sessizce yutma yok).

**C3.** 🔴 **Sahip `members`de olmak ZORUNDA DEĞİL.** Yetki ve pull her yerde
`owner_id = @actorId **OR** project_members` biçiminde sorulur. Sahibi OrSet'e yazmak, sahipliğin
CRDT'den silinebilmesi demektir — yasak.

---

## 5. §D — Pull'un scope kolu (🔴 İKİ YOL DA)

**D1. `PullIncrementalAsync`:** mevcut `AND owner_id = @actorId` şu kola dönüşür:

```
AND ( o.owner_id = @actorId
   OR o.scope_id     IN (SELECT project_id FROM project_members WHERE user_id = @actorId)
   OR o.old_scope_id IN (SELECT project_id FROM project_members WHERE user_id = @actorId) )
```

🔴 **`old_scope_id` kolu pazarlıksız:** bir görev projeden çıkarılınca (`projectId → null`) o op'un
`scope_id`si null, `old_scope_id`si eski projedir. Bu kol düşerse üyenin ekranında **görev sonsuza
dek asılı kalır** (hayalet satır) — kullanıcı için "silinmiş bir şeyi görüyorum" demektir.
🔴 o84 dersi: `ORDER BY` nitelikli (`o.`) kalır, gölgelenmez; `EXPLAIN` çıktısı KANIT'a.

**D2. `SnapshotAsync → ReadOwnedEntitiesAsync`:** aynı kol. Bu olmadan **taze kurulmuş** bir istemci
paylaşılan hiçbir şeyi görmez (o85-A'nın C2 kanıtı tam buydu) ⇒ dilim yarım kalır.

**D3. `ScopeMembershipSource`** artık outbox'tan değil `project_members`ten okur. Belgesindeki
"named gap" yorumu **silinir**, yerine ne değiştiği tek cümleyle yazılır.

---

## 6. §E — Yazma yetkisi kapısı

`ProcessOpAsync` op'u kabul etmeden önce sorar. Kural tablosu **birebir**:

| Durum | Karar |
|---|---|
| Varlık **yeni** (daha önce hiç op'u yok) | **KABUL** — yazan sahip olur |
| `Task` op'u, hedef scope **null** (Gelen Kutusu) | **KABUL** — yazan kendi kutusuna yazıyor |
| `Task` op'u, hedef scope P | yazan P'nin **sahibi veya üyesi** ise KABUL, değilse **RED** |
| `Project` op'u, alan `members` | yalnız **`projects.owner_id`** ise KABUL, değilse **RED** |
| `Project` op'u, diğer alanlar | sahip **veya üye** ise KABUL, değilse **RED** |

🔴 `members` kuralı en sert ısıran yerdir: üyelik yazma yetkisi verdiği için, herhangi bir üye
`members`ten **sahibi silebilseydi** projeyi çalabilirdi.
🔴 Red **sessiz olamaz**: A3/A4'ün ölçümüne göre sonuç seçilir ve istemcinin sonsuz yeniden denemeye
girmediği (ya da girdiği — o zaman **bildir**) CEVAP'ta yazılır.

---

## 7. §F — E-posta arama ucu

`POST /v1/users/lookup`, gövde `{ "email": "..." }` → **200** `{ "userId": "..." }` | **404** | **401**.

- 🔴 **GET + query string YASAK** — e-posta URL'de, loglarda, Referer'da kalır.
- Kimlik doğrulaması **şart** (401), aksi hâlde açık kullanıcı sayım yüzeyi doğar.
- Yanıt **yalnız `userId`**: ad, e-posta, oluşturma tarihi **dönmez**.
- Arama `email_normalized` (collation C) üzerinden yapılır — 🔴 Türkçe `İ/ı` katlaması **YOKTUR**
  (DURUM md.14 ile aynı sınıf); normalizasyonun ne yaptığı ölçülüp CEVAP'a yazılır.

---

## 8. §G — Testler (mevcut dosyalara, mutant-ispatlı)

Her biri için mutant uygulanır, **kırmızı ham çıktıyla** gösterilir, geri alınır:

- **G1** `Project` op'u `scope_id = entityId` taşır · *mutant:* §B dalını kaldır
- **G2** `members` ekleme → `project_members` satırı; **kaldırma → satır GİDER** (pozitif + negatif)
- **G3** artımlı pull: üye B görür, **üye OLMAYAN C GÖRMEZ** · *mutant:* scope kolunu kaldır
- **G4** snapshot: **taze** B projeyi ve görevlerini görür · *mutant:* D2'yi kaldır
- **G5** `old_scope_id`: projeden çıkarılan görev üyeye "çıktı" olarak ulaşır · *mutant:* kolu kaldır
- **G6** yetki: üye olmayanın yazımı **RED** · üyenin `members` yazımı **RED** · sahibinki **KABUL**
- **G7** lookup: 401 · 404 · 200 + yanıtta **yalnız** `userId` (fazla alan varsa kırmızı)

🔴 **Pozitif kontrol zorunlu:** her "görüyor" iddiasının yanına bir "görmemeli" iddiası konur —
boş liste her iddiayı geçirir.

---

## 9. §H — Canlı tur + regresyon

**H1.** Yeni betik `KANIT/o86A/_canli_tur_o86a.py`, **ÜÇ hesap**, sekiz adım:
1. A, B, C kayıt olur (`POST /v1/auth/register`)
2. A proje P + P'de görev T yaratır
3. **B pull → HİÇBİR ŞEY görmez** (negatif kontrol — kapı burada)
4. A, B'nin e-postasını `lookup`lar → `userId`
5. A `members` op'unu yazar (P'ye B eklenir)
6. B pull → **P'yi ve T'yi görür** (snapshot ve artımlı **ayrı ayrı**)
7. B T'yi düzenler → **A pull → değişikliği görür** (çift yönlü yakınsama)
8. **C pull → hiçbir şey görmez** ve **C'nin P'ye yazımı REDDEDİLİR**

**H2. REGRESYON — yeni betik yazılmaz:** `KANIT/o85A/_canli_tur_o85a.py` **olduğu gibi** yeniden
koşulur, **6/6**. Pull'un yüklemi değişti; tek kullanıcılı davranışın bozulmadığının kanıtı budur.

---

## 10. KANIT — `KANIT/o86A/`

`00-CEVAP.md` · `01-olcum.txt` (§A, kod yazmadan önce) · `02-migration.txt` ·
`03-mutantlar-kirmizi.txt` (G1-G7, her biri ayrı başlıkla) · `04-canli-tur-o86a.txt` (8/8) ·
`05-regresyon-o85a.txt` (6/6) · `06-explain-pull.txt` (D1'in `EXPLAIN` çıktısı) · `07-verify-ps1.txt`

## 11. CEVAP — `KANIT/o86A/00-CEVAP.md`, altı satır

1. §A'nın dört ölçümü (özellikle A1: `Project` op'unda `scope_id` gerçekten null muydu; A4: istemci
   reddedilen op'a ne yapıyor).
2. `project_members`in son sütun/indeks listesi + `members` OrSet'inin canlı durumunun nasıl okunduğu.
3. Pull'un yeni yüklemi **birebir** (üç kol) + `EXPLAIN`in indeksi kullandığı.
4. Yetki tablosunun kodda nerede uygulandığı + reddedilen op'un istemciye dönen sonucu.
5. `POST /v1/users/lookup` 401/404/200 davranışı + `email_normalized`ın Türkçe harflerde ne yaptığı.
6. `verify.ps1` EXIT + test sayısı (öncesi/sonrası) + `git status --porcelain -- src tests`
   (`src/client/**` **GÖRÜNMEMELİ**).

## 12. KABUL

- [ ] §A ölçümleri **kod yazılmadan önce** alınmış (ham çıktı var); A1 tasarımı doğruladı
- [ ] `Project` op'u `scope_id = entityId` taşıyor; mutantı **öldü**
- [ ] `project_members` OrSet'ten materyalize; **ekleme VE kaldırma** ısırıyor
- [ ] Pull **her iki yolda** scope kolunu taşıyor (artımlı + snapshot); `old_scope_id` kolu **var**
- [ ] Üye olmayan C **hiçbir şey görmüyor** ve **yazamıyor** (pozitif kontrol var)
- [ ] `members`e yalnız **sahip** yazabiliyor — üyenin denemesi RED
- [ ] `ScopeMembershipSource` artık `project_members`ten okuyor; "named gap" yorumu kalktı
- [ ] `POST /v1/users/lookup` yalnız `userId` dönüyor; GET/query-string **yok**; 401 çalışıyor
- [ ] Canlı tur **8/8** · regresyon `_canli_tur_o85a.py` **6/6**
- [ ] `verify.ps1` EXIT 0 · `src/client/**` ve `FieldStrategyRegistry` **değişmedi** · yeni kapı dosyası yok
- [ ] Tek commit, yol belirterek, **çift tırnaksız** mesaj, author `onurkesimbjk@gmail.com`
- [ ] 🔴 **PUSH YOK** · kanıt dosyası kendi commit'inin hash'ini yazmaz
- [ ] `KANIT/slice-3c/02-G2/*.json` commit'e **girmedi** (mayın 19)

## 13. DOKUNMA LİSTESİ

- ❌ `src/client/**` (o86-B'nin işi) · `FieldStrategyRegistry` · mevcut migration'lar
- ❌ OrSet'e e-posta yazmak · lookup'ta e-posta/ad döndürmek · GET+query ile arama
- ❌ Sahibi `members`e yazmak · bekleyen davet (davetli kullanıcı yoksa **404**, dilim dışı)
- ❌ Rol/izin kademesi (viewer/editor) — bu dilimde üyelik **tek seviyedir**
- ❌ `verify.ps1` · `DURUM.md` · `CLAUDE.md` · `arsiv/` · `.github/workflows/*`
- ❌ **PUSH** — sıradaki adım Onur'un

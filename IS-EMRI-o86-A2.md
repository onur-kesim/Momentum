# İŞ EMRİ o86-A2 — DİLİM 3 / İŞBİRLİĞİ · DENETİM KAPANIŞI

`MOD: KRİTİK` [Onur, 4 Eyl] · kutu **4-6 Eyl 2026** · yazan: **Cowork** · koşacak: **Claude Code**
Öncül: `0058703` + **commit edilmemiş** o86-A ağacı. Denetim: `oturum-87-o86A-BAGIMSIZ-DENETIM`.

🔴 **o86-A denetimde DÜŞTÜ.** İki yeni bulgu + eldeki enjeksiyon deliği **aynı sınıfın üç üyesi**:
*yetki/görünürlük kararı op'un KENDİ beyanına bakıyor, varlığın MEVCUT bağlamına değil.*
Bu emir üçünü birden kapatır [Onur kilidi, 4 Eyl].

**Bulgu 1 ve 2 tasarım kusurudur (Cowork), builder hatası değildir** — §C3 `outbox.owner_id` (yazan)
ile `projects.owner_id` (proje sahibi) kavramlarını aynı saydı; §E tablosu yalnız "hedef scope" dedi.

---

## 0. ÖNCE — TIKANIKLIK (kod değil)

`.git/index.lock` (0 bayt, **20 Ağu 15:56**) duruyor ⇒ o86-A bu yüzden commit EDİLEMEDİ
(DEVİR "commit'lendi" diyordu, **yanlış**). Mount'ta silme yasak (mayın 8) ⇒ **Windows tarafından
sil**, sonra commit et. Silmeden hiçbir commit adımını deneme.

## 1. DEMİR KURALLAR

1. 🔴 **İSTEMCİ DEĞİŞMEZ** (`src/client/**` = o86-B). Dokunma dürtüsü olursa DUR ve bildir.
2. 🔴 **`FieldStrategyRegistry` DEĞİŞMEZ.**
3. 🔴 **ADR/spec YAZILMAZ** (İŞLEYİŞ md.4). Tasarım bu emirdedir.
4. 🔴 **PII:** OrSet'e e-posta yazılmaz; lookup yalnız `userId` döner. Değişmedi.
5. **Yeni kapı DOSYASI açılmaz.** Testler mevcut dosyalara girer (`D9OwnerIdVisibilityTests`,
   `RealtimeMembershipTests`, `MaterializationRoundTripTests`, `ArchitectureRuleTests`).
6. `verify.ps1` · `DURUM.md` · `CLAUDE.md` · `arsiv/` · `.github/workflows/*`: **dokunma**.
   **PUSH ONUR'DA.**

---

## 2. §A — `project_access` görünümü (tanım TEK yerde) [Onur kilidi, 4 Eyl]

Yeni migration `AddProjectAccessView`, yalnız `migrationBuilder.Sql`:

```sql
CREATE VIEW project_access AS
SELECT entity_id AS project_id, owner_id AS user_id FROM projects
UNION
SELECT project_id,             user_id           FROM project_members;
```

`Down` = `DROP VIEW project_access`. **EF varlığı olarak eşlenmez** (ham SQL ile okunur) ⇒
`DbSet`/konfigürasyon eklenmez, `ModelSnapshot` görünümü yönetmez.

🔴 **`is_deleted` SÜZÜLMEZ:** silinmiş projenin üyesi silinme olayını çekebilmeli.
🔴 **Sahip `project_members`e YAZILMAZ** — §C3 kilidi duruyor; sahiplik CRDT'den silinemez kalır.

**ÖLÇÜM (kod bitmeden):** `EXPLAIN` ile görünümün iki kolunun da indeks kullandığını göster —
`ix_project_members_user_project` ve `ix_projects_owner_deleted_pos_entity` (öncü kolon `owner_id`).
Biri Seq Scan'e düşerse **dar bir indeks ekle** ve gerekçesini CEVAP'a yaz.

## 3. §B — Tüketiciler görünümü sorar

Üç yerde `project_members` → `project_access`, `user_id` yüklemi aynı kalır:

- `SyncPuller.PullIncrementalAsync` (satır 45-46) — **iki alt-sorgu da**
- `SyncPuller.ReadOwnedEntitiesAsync` (satır 101-102) — **iki alt-sorgu da**
- `ScopeMembershipSource.GetScopesAsync`

`SyncStore.IsProjectOwnerOrMemberAsync` gövdesi tek `EXISTS (SELECT 1 FROM project_access ...)`
olur (**ad değişmez** — arayüz ve çağrı yerleri sabit kalsın).
🔴 `IsProjectOwnerAsync` **DEĞİŞMEZ**: `members` yazımı yalnız gerçek sahibindir.
🔴 `ORDER BY` nitelikli (`o.`) kalır — o84 dersi.

## 4. §C — Yetki: KAYNAK **ve** HEDEF scope

`IsAuthorizedAsync` bugün yalnız POST-op scope'a bakıyor (satır 243) ⇒ eski üye, `projectId`yi
`null`a ya da kendi projesine çevirerek görevi **koparabiliyor/çalabiliyor**.

`preProjectId` satır 141'de **zaten hesaplanıyor** — parametre olarak `IsAuthorizedAsync`'e geç.
`Task` dalının kuralı:

```
IZIN(null) = true
IZIN(P)    = IsProjectOwnerOrMemberAsync(P, actor)
KABUL  <=>  IZIN(preScope)  VE  IZIN(postScope)
```

Sonuç kodu değişmez: `RejectedForbidden`.

## 5. §D — Yeni varlık deliği daralır

`isNewEntity` bugün koşulsuz `true` (satır 236) ⇒ yabancı, **tahmin ettiği** `projectId` ile
başkasının projesinde yepyeni bir görev doğurabiliyor.

- `Task` yeni varlık: hedef scope `null` ise KABUL; scope P ise **`IZIN(P)` şart**, değilse RED.
- `Project` / `TaskList` / `Tag` yeni varlık: KABUL (kişi kendi kabını yaratıyor) — değişmez.

## 6. §E — Testler (mevcut dosyalara, her biri **mutant-ispatlı**)

Her madde için mutant uygulanır, **kırmızı ham çıktıyla** gösterilir, geri alınır.

- **H1** 🔴 *bu dilimin ASIL kapısı:* **SAHİP A**, üye B'nin yazdığını **ARTIMLI** pull'da
  (kendi cursor'undan) **GÖRÜR** · negatif: üye olmayan C **GÖRMEZ**
  · *mutant:* `project_access`in owner kolunu kaldır ⇒ kırmızı.
- **H2** Sahip A bağlanınca `scope:{P}` grubuna **KATILIR** (`RealtimeMembershipTests`)
  · *mutant:* `GetScopesAsync`i `project_members`e geri al ⇒ kırmızı.
- **H3** Üyelikten **çıkarılmış** B, T'yi `projectId=null` yapamaz ⇒ `RejectedForbidden`
  · **pozitif kontrol:** hâlâ üye olan B **AYNI** op'u yapabilir ⇒ `Applied`
  · *mutant:* `IZIN(preScope)` kolunu kaldır ⇒ kırmızı.
- **H4** Eski üye B, T'yi **kendi** projesi Q'ya taşıyamaz ⇒ `RejectedForbidden`.
- **H5** Yabancı C, tahmin ettiği `projectId=P` ile **yeni** görev doğuramaz ⇒ `RejectedForbidden`
  · **pozitif kontrol:** üye B **AYNI** op'la yeni görev doğurabilir
  · *mutant:* §D daralmasını kaldır ⇒ kırmızı.
- **H6** REGRESYON: sahip kendi Gelen Kutusu'na (scope `null`) yeni görev yazabilir; `members`
  ekleme/kaldırma materyalizasyonu (o86-A G2) **bozulmadı**.

**§G — sınıf kapısı (mekanik, İŞLEYİŞ md.8):** `ArchitectureRuleTests`e **tek** test —
`SyncPuller.cs` ve `ScopeMembershipSource.cs` kaynaklarında `project_members` dizgesi
**GEÇMEZ** (erişim yalnız `project_access` üzerinden). Yeni dosya değil, mevcut dosyaya tek test.

## 7. §F — Canlı tur + regresyon

**F1.** `KANIT/o86A2/_canli_tur_o86a2.py` — o86-A'nın sekiz adımı **artı** dördü:

🔴 **7. adım DEĞİŞTİ:** A için **ARTIMLI** pull (A'nın kendi cursor'undan) koşulur.
**TAZE snapshot ile ÖLÇME** — o86-A'yı yanlışlıkla geçiren tam buydu (snapshot, T'yi A'nın eski
yaratım satırından bulup güncel CRDT durumunu döndürüyor ⇒ iddia başka sebeple yeşil oluyor).

9. A, B'yi `members`tan **çıkarır** ⇒ B artık P'yi pull'da **görmez** (negatif kontrol).
10. **Eski üye** B, T'yi `projectId=null` yapmaya çalışır ⇒ **RED**.
11. **Eski üye** B, T'yi kendi projesi Q'ya taşımaya çalışır ⇒ **RED**.
12. C, tahmin ettiği P ile **yeni görev** doğurmaya çalışır ⇒ **RED**.

**F2. REGRESYON — yeni betik yazılmaz:** `_canli_tur_o85a.py` **6/6** ve `_canli_tur_o86a.py`
**8/8**, ikisi de **olduğu gibi** yeniden koşulur.

## 8. KANIT — `KANIT/o86A2/`

`00-CEVAP.md` · `01-explain-view.txt` (§A ölçümü) · `02-migration.txt` ·
`03-mutantlar-kirmizi.txt` (H1-H5 + §G, her biri ayrı başlıkla) · `04-canli-tur-o86a2.txt` (12/12) ·
`05-regresyon.txt` (o85a 6/6 + o86a 8/8) · `06-verify-ps1.txt`

## 9. CEVAP — `KANIT/o86A2/00-CEVAP.md`, altı satır

1. `project_access`in `EXPLAIN`i: her iki kol da indekste mi, hangi indeks?
2. Pull'un yeni yüklemi **birebir** (üç kol, görünüm üzerinden) + hub'ın sahibi hangi gruba soktuğu.
3. Yetki kuralının kodda nerede uygulandığı: `IZIN(pre) VE IZIN(post)` + yeni-varlık daralması.
4. H1'in **artımlı** olduğunun kanıtı (istekte `sinceCursor` **null DEĞİL** — ham gövdeyi yapıştır).
5. §G kapısının ne dediği ve mutantının nasıl kırmızı olduğu.
6. `verify.ps1` EXIT + test sayısı (öncesi/sonrası) + `git status --porcelain -- src tests`
   (`src/client/**` **GÖRÜNMEMELİ**).

## 10. KABUL

- [ ] `.git/index.lock` silindi; commit çalışıyor
- [ ] `project_access` görünümü var; **iki kolu da** indeks kullanıyor (`EXPLAIN` ham çıktı)
- [ ] Üç tüketici de görünümü soruyor; `IsProjectOwnerAsync` **değişmedi**
- [ ] 🔴 **H1 yeşil:** sahip, üyenin yazdığını **ARTIMLI** pull'da görüyor; mutantı **öldü**
- [ ] H2 yeşil: sahip `scope:{P}` grubunda; mutantı öldü
- [ ] H3/H4 yeşil (**pozitif kontrolüyle**): eski üye koparamıyor/çalamıyor, gerçek üye yapabiliyor
- [ ] H5 yeşil (**pozitif kontrolüyle**): tahmin edilen `projectId` ile yeni görev **RED**
- [ ] §G kapısı `ArchitectureRuleTests`te, tek test, mutantı kırmızı
- [ ] Canlı tur **12/12** · regresyon **6/6** ve **8/8**
- [ ] `verify.ps1` EXIT 0 · `src/client/**` ve `FieldStrategyRegistry` **değişmedi**
- [ ] **İKİ commit** (o85 deseni): önce o86-A olduğu gibi, sonra o86-A2 düzeltmesi.
      Yol belirterek, **çift tırnaksız** mesaj, author `onurkesimbjk@gmail.com`
- [ ] 🔴 **PUSH YOK** · kanıt dosyası kendi commit'inin hash'ini yazmaz
- [ ] `KANIT/slice-3c/02-G2/*.json` ve `slice-3d/.../outbox-sorgu.txt` commit'e **girmedi** (mayın 19)

## 11. DOKUNMA LİSTESİ

- ❌ `src/client/**` · `FieldStrategyRegistry` · `IsProjectOwnerAsync` · mevcut migration'lar
- ❌ Sahibi `project_members`e yazmak (§C3 duruyor) · OrSet'e e-posta · lookup'ta fazla alan
- ❌ Rol/izin kademesi (viewer/editor) — üyelik bu dilimde **tek seviye**
- ❌ `verify.ps1` · `DURUM.md` · `CLAUDE.md` · `arsiv/` · `.github/workflows/*`
- ❌ **PUSH** — sıradaki adım Onur'un

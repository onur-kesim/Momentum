# İŞ EMRİ o86-A3 — DİLİM 3 / İŞBİRLİĞİ · SINIF KAPANIŞI

`MOD: KRİTİK` · kutu **5-6 Eyl 2026** · yazan: **Cowork** · koşacak: **Claude Code**
Öncül: `8855ca8` (o86-A2, push EDİLMEDİ). Denetim: oturum 87 turu 2.

🔴 **Neden bu emir var:** yetki/görünürlük sınıfı **beşinci kez** ısırdı. Beşinin de kökü aynı:
*karar op'un KENDİ beyanına bakıyor, varlığın MEVCUT bağlamına değil.* Bu emir hem beşinciyi
kapatır hem sınıfı **mekanikleştirir** — İŞLEYİŞ md.8'in emrettiği kalıcı, CI'da koşan kural budur.

**Radar notu:** `proje-radari` altın kümesi geçti ama Momentum'un kök defteri yok
(`arsiv/PROJE_RADAR.jsonl` oturum 71'de donduruldu) ⇒ radar **hüküm veremedi**. Petersen-Lincoln
iki denetçinin kesişimi **0** olduğu için hesaplanamadı — "kalan kusur az" **iddia EDİLEMEZ**.
Bu emir o belirsizliği tur ekleyerek değil, **sınıfı ölçen kontrolü yazarak** kapatır.

---

## 1. DEMİR KURALLAR

1. 🔴 **İSTEMCİ DEĞİŞMEZ** (`src/client/**` = o86-B). · **`FieldStrategyRegistry` DEĞİŞMEZ.**
2. 🔴 **ADR/spec YAZILMAZ** (İŞLEYİŞ md.4). Tasarım bu emirdedir.
3. **Yeni kapı DOSYASI açılmaz.** Testler mevcut dosyalara girer.
4. `verify.ps1` · `DURUM.md` · `CLAUDE.md` · `arsiv/` · `.github/workflows/*`: **dokunma**.
   **PUSH ONUR'DA.**

---

## 2. §A — BULGU 5: kapsamsız görevde sahiplik sorulacak

**Ölçülen kusur:** `IzinAsync(null) => true` **koşulsuz**. §E tablosundaki "yazan kendi kutusuna
yazıyor" gerekçesi kodda hiç doğrulanmıyor. Sonuç (kaynaktan zincirlendi):
- UUID'sini bilen herhangi bir kimlikli kullanıcı (ör. görevin bir zamanlar içinde bulunduğu
  projenin **eski üyesi**) başkasının Gelen Kutusu görevine **yazabiliyor**;
- `{projectId: Q}` yazıp görevi **kendi projesine çalabiliyor** (pre=null serbest, post=Q sahibi);
- ardından satır `owner_id=C, scope_id=Q` olduğu için sahip **ne artımlıda ne snapshot'ta**
  öğrenir — görev sahibin ekranından **sessizce kaybolur**.

**Kural (asimetrik — pazarlıksız):**

```
IZIN_PRE(preScope, isNewEntity, entityId, actor):
    isNewEntity        -> true                                  (geçmiş bağlam yok)
    preScope is null   -> _store.IsTaskOwnerAsync(entityId, actor)
    preScope is P      -> IsProjectOwnerOrMemberAsync(P, actor)

IZIN_POST(postScope, actor):
    postScope is null  -> true                                  (kendi kutusuna indirmek serbest)
    postScope is Q     -> IsProjectOwnerOrMemberAsync(Q, actor)

KABUL  <=>  IZIN_PRE(...)  VE  IZIN_POST(...)
```

🔴 **Asimetri neden:** post tarafına sahiplik şartı koyarsan H3'ün **pozitif kontrolü** kırılır —
hâlâ üye olan biri görevi projeden koparabilmelidir.

**Yeni store metodu** `ISyncStore.IsTaskOwnerAsync(Guid taskId, Guid actorId)`:
`SELECT EXISTS (SELECT 1 FROM tasks WHERE entity_id = @t AND owner_id = @a)`.
🔴 **Fail-closed:** `tasks` satırı yoksa **false** döner (materyalizasyon boşluğu yetki AÇMAZ).

🔴 **ÖLÇ ve CEVAP'a YAZ (düzeltme bu emrin işi DEĞİL):** `tasks.owner_id` `ON CONFLICT`ta
güncellenmiyor ⇒ **ilk yazan** sabit. Dolayısıyla bir üyenin P içinde yarattığı görevi sahip
P'den koparırsa görev **o üyenin** Gelen Kutusu'na düşer ve sahip artık ona yazamaz. Sahiplik
devri op'u yok (kapsam dışı) ⇒ bu, **beyan edilmiş sınır** olarak yazılır.

## 3. §B — SINIFIN MEKANİK KONTROLÜ (doğruluk tablosu)

`Rule5` yalnız bir **dizgeyi** yasaklıyor; kararın **şeklini** sabitlemiyor. Sınıfı ölçen kontrol:

Mevcut `D9OwnerIdVisibilityTests`e **tek** parametreli test (`[Theory]` + `InlineData`), eksenler:

- `isNew` ∈ { yeni, mevcut }
- `preScope` ∈ { null-**benim** görevim, null-**başkasının** görevi, P-**üyesiyim**, P-**değilim** }
- `postScope` ∈ { null, Q-**benim**, Q-**üyesi değilim** }

Her satırın beklenen kararı (`Applied` / `RejectedForbidden`) tabloda **açıkça** yazılır;
anlamsız bileşimler (yeni varlık + preScope≠null) tabloda **yok** sayılmaz, `[InlineData]`ya
alınmaz ve gerekçesi tek satır yorumla belirtilir.

🔴 **Gerekçe (doktrinin MEKANİKLEŞTİR filtresi):** bu sınıf **koşan kodla ölçülebilir**, çünkü
yetki kararı saf bir fonksiyondur — `(isNew, preScope, postScope) -> karar`. Beş turda prozada
kalan sınıf her turda geri geldi; tablo onu numaralandırıp donduruyor.

**Mutantlar:** `IZIN_PRE`in null kolu · `IZIN_PRE`in scope kolu · `IZIN_POST`un scope kolu —
**üçü ayrı ayrı** kaldırılır, tablo her seferinde **kırmızı** olur, ham çıktı KANIT'a.

## 4. §C — İKİ BELGE KUSURU (o86-A2'nin kanıtında)

**C1.** `KANIT/o86A2/00-CEVAP.md` **madde 2** teslim edilen yüklemi YANLIŞ yazıyor: eski üç kollu
hâli aktarıyor, 4. bulgunun `AND o.scope_id IS NULL AND o.old_scope_id IS NULL` daralmasını
atlıyor ve madde 3 ile çelişiyor. Madde 2 **birebir** teslim edilen SQL ile değiştirilir.

**C2.** `KANIT/o86A2/06-verify-ps1.txt` **kesik**: dosyada ne `EXIT` satırı var ne CVE kapısı
satırı — oysa CEVAP madde 6 ikisini de beyan ediyor. `verify.ps1` **yeniden koşulur** ve çıktı
**sonuna kadar** (CVE kapısı + `== VERIFY PASSED ==` + `EXIT:0`) yakalanır.
🔴 Kesik çıktı kanıt değildir; beyan kanıtla örtüşmezse beyan **silinir**.
(Mayın 6: `verify.ps1`, `Momentum.Api` ayaktayken koşulamaz — backend kapatma **Onur'un izniyle**.)

## 5. §D — Canlı tur eki

`KANIT/o86A2/_canli_tur_o86a2.py` **genişletilir** (yeni betik yazılmaz), iki adım eklenir:

13. A kendi **Gelen Kutusu**'nda `T2` yaratır → **C** (yabancı, `T2`'nin id'sini bilir) `T2`'ye
    başlık yazmaya çalışır ⇒ **RejectedForbidden**.
14. C, `T2`'yi kendi projesi `Q`'ya taşımaya çalışır ⇒ **RejectedForbidden**.
    🔴 **Pozitif kontrol:** A **aynı iki op'u** yapabilir ⇒ `Applied` (kutu kilitlenmedi).

**REGRESYON — betikler DEĞİŞTİRİLMEZ:** `_canli_tur_o85a.py` **6/6** · `_canli_tur_o86a.py`
**8/8** · `_canli_tur_o86a2.py` (13-14 hariç önceki adımların **hiçbiri düşmeden**).

## 6. KANIT — `KANIT/o86A3/`

`00-CEVAP.md` · `01-mutantlar-kirmizi.txt` (üç mutant, ayrı başlıklarla) ·
`02-canli-tur-o86a2-genisletilmis.txt` · `03-regresyon.txt` · `04-verify-ps1.txt` (**tam**, EXIT dahil)

## 7. CEVAP — `KANIT/o86A3/00-CEVAP.md`, beş satır

1. `IZIN_PRE`/`IZIN_POST`un kodda nerede olduğu + `IsTaskOwnerAsync`in SQL'i (fail-closed dahil).
2. Doğruluk tablosunun **kaç satır** olduğu, hangi bileşimlerin dışarıda bırakıldığı ve **neden**.
3. Üç mutantın her birinin hangi tablo satırını kırmızıya düşürdüğü.
4. 🔴 `tasks.owner_id` = **ilk yazan** ölçümü + sahibin koparılmış üye-görevine yazamaması
   (beyan edilmiş sınır; README'ye de yazılır).
5. `verify.ps1` **EXIT** (ham satırıyla) + test sayısı (öncesi/sonrası) +
   `git status --porcelain -- src tests` (`src/client/**` **GÖRÜNMEMELİ**).

## 8. KABUL

- [ ] `IZIN_PRE`/`IZIN_POST` asimetrik kuralı kodda; `IsTaskOwnerAsync` **fail-closed**
- [ ] Yabancı, başkasının Gelen Kutusu görevine **yazamıyor** ve onu **çalamıyor**
- [ ] **Pozitif kontrol:** sahip kendi kutusuna yazabiliyor · hâlâ üye olan görevi koparabiliyor
- [ ] Doğruluk tablosu tek `[Theory]` olarak mevcut dosyada; **üç mutant** ham kırmızı
- [ ] Canlı tur 13-14 eklendi, önceki adımların hiçbiri düşmedi
- [ ] Regresyon `o85a` **6/6** · `o86a` **8/8**
- [ ] `KANIT/o86A2/00-CEVAP.md` madde 2 düzeltildi · `06-verify-ps1.txt` **tam** yeniden alındı
- [ ] `verify.ps1` **EXIT 0 ham satırıyla** · `src/client/**` ve `FieldStrategyRegistry` değişmedi
- [ ] Tek commit (üçüncü), yol belirterek, **çift tırnaksız** mesaj, author `onurkesimbjk@gmail.com`
- [ ] 🔴 **PUSH YOK** · `KANIT/slice-3c/02-G2/*.json` commit'e **girmedi** (mayın 19)

## 9. DOKUNMA LİSTESİ

- ❌ `src/client/**` · `FieldStrategyRegistry` · `IsProjectOwnerAsync` · mevcut migration'lar
- ❌ `IZIN_POST`a sahiplik şartı koymak (H3'ün pozitif kontrolünü kırar)
- ❌ Sahiplik devri op'u / rol kademesi — kapsam dışı, beyan edilmiş sınır olarak yazılır
- ❌ `verify.ps1` · `DURUM.md` · `CLAUDE.md` · `arsiv/` · `.github/workflows/*` · **PUSH**

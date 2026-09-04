# DURUM.md — Momentum

**BİTTİ: 12/14 · kutu 9 Eyl · dilim 3/5 sunucu ayağı KABUL+PUSH · HEAD `12dc0a0`. AŞAMA: `o86-B` (istemci).** Teslim biçimi paketlenmiş build (docker imajı + APK); yeni teslim `v1.1.0`.

> Açılış ≤3 komut: ① `git --no-optional-locks log --oneline -1` + `status --porcelain -- src`
> ② bu dosya ③ CI durumu — **cihaz Chrome'undan** (bulut tarayıcısı kanıt değil). `arsiv/` AÇILMAZ.
> **Flutter `src/client`'tan** (sınır 1). Oturum 74-86 + kapanmışlar: `arsiv/DURUM-arsiv-o85.md`.

## Kalıcı dersler (dilimden bağımsız)

🔴 **Kör kapı:** dize değil **VARLIK** · sayı değil **AD** · **ÜRÜN UCU**. Düzeltmenin yazılmış
olması indiği anlamına gelmez (sha256 yakaladı). Ortamı değil **DİKİŞİ** ölç.
🔴 **Zamanlamaya benzeyen kusurdan önce `EXPLAIN Sort Key`'e bak:** o84'te dört oturumluk "flake",
`SyncPuller`de gölgelenmiş `ORDER BY` çıktı — basamak sınırında satırlar **sessizce kayboluyordu**
ve `v1.0.1` bununla teslim edilmişti (`KANIT/o84`).
🔴 **Kapı beyanı commit ile birlikte yazılır**; **elle tetiklenen** koşumun sha'sı **run kaydından**
doğrulanır, liste satırı yanıltır (o81: `pages #8` = o78 kodu ⇒ canlı demo teslim edilen kod değildi).
🔴 **Pozitif kontrol:** boş liste her iddiayı geçirir — her "görünüyor" iddiasının yanına bir
"görünmemeli" iddiası konur.

## DİLİM 3 — İŞBİRLİĞİ (sunucu ayağı KABUL EDİLDİ ve PUSH'LANDI)

**Kilitler [Onur, 20 Ağu]:** üyelik = `Project.members` OrSet → `project_members` · davet e-posta
ile · rol kademesi YOK · sahip OrSet'e YAZILMAZ; erişim = **`project_access`** (sahip ∪ üye).

**Kapı beyanı (cihaz Chrome, 4 Eyl):** `ci #79`·`paket #14`·`pages #15` — üçü de **`12dc0a0`**
ve yeşil; `pages` elle tetiklendi, sha'sı **run kaydından** okundu (liste satırından DEĞİL).
Dört commit push'lu. İki denetim turu **beş** bulgu çıkardı (hepsi sınır 38'in sınıfı), beşi de
kapandı; kapı proza değil **doğruluk tablosu** (15 satır, 3 mutant). Canlı tur **14/14** ·
regresyon **6/6** ve **8/8** · `verify` EXIT 0 / **175** test · kanıt `KANIT/o86A*`.

**DİLİM 2 — LİSTE BİTTİ** (20 Ağu; kapı beyanları `arsiv/DURUM-arsiv-o85.md`). **Kilitler [Onur,
19 Ağu]:** Liste = sunucudaki **`Project`** · klasör KESİLDİ · `listPos`/`order` kanalı AÇILMADI ·
`projectId == null` = **Gelen Kutusu** · liste silinince görevler Gelen Kutusu'na **düşer**.

## Bilinen sınırlar

1. 🔴 **Flutter komutu repo kökünden koşulursa yalan söyler.** Doğru dizin `src/client`.
   PowerShell 5.1'de `&&` yok, `;` yaz.
2. **Canlı ölçümde tıklama tuzağı:** hover'sız sentetik tıklama çalışmaz, hover'lı bile bazen İKİ
   kez gerekir; diyalogdaki `İptal` tetiklenmez, modalı **Escape** kapatır.
3. **Kapı bütçesi ihlalde** ⇒ yeni kapı DOSYASI açılmaz (widget/birim testleri orana girmez).
   **[o81] Kalan TEK açık bulgu:** arm64 kırılması manifestle gösterildi, **gerçek arm64'te
   KOŞULMADI** — donanım yok.
6. **Pages demosunda backend yok** ⇒ satır kuyrukta kalır, rozet **"↑ Gönderiliyor"**da asılı
   durur. Senkron ayağı Pages'te ASLA ölçülemez, **pakette ölçülür**. Eşitlenmiş satır rozet
   GÖSTERMEZ (`senkronize => null`).
7. **[o79] `.github/workflows/*` yalnız `device_commit_files`'ta reddedilir; `device_bash` oraya
   YAZABİLİR** — koruma araçta, klasörde değil. Yol: korumasız yola yaz → `cp` → sha256 doğrula.
8. **`pub cache` boşalabiliyor** (`flutter pub get`).
9. **ÇAKIŞMA TESPİTİ yalnız başlık/tamamlanma:** `kanonikDize` `fields:title` + `groups:completion`
   tanır; başkasında **FIRLATIR**. Bilinmeyen `priority` çizilmez ama EZİLMEZ.
14. **Etiketlerde BÜYÜK/KÜÇÜK HARF KATLAMASI YOK** (sunucu Ordinal karşılaştırır): `İş` ≠ `iş`.
    32 karakter sınırı YALNIZ İSTEMCİ kelepçesidir.
16. **[o77] Doğal dil sınırları (kilitli):** `Yarın`/ASCII `yarin` TANINMAZ · saat başlıkta kalır ·
    yılsız `03.01` GEÇMİŞE düşer · `#İş` ile `#iş` ayrı etikettir.
18. **[o77 · o85 genişledi] `GorevDeposu.ekle` imzası** DÖRT opsiyonel alan taşır
    (`oncelik`/`sonTarih`/`etiketler`/`projeId`) ⇒ yeni sahte depo dördünü de kabul ETMEK ZORUNDA.
19. **[o77] `flutter test` `KANIT/slice-3c/02-G2/*.json`i her koşumda YENİDEN YAZAR** ⇒ o dört
    dosya commit'e GİRMEMELİ; `git add` yol belirterek yapılır.
21. **[o77] CI `istemci` işi `TZ: Europe/Istanbul` koşar**; `ekle`nin `sonTarih`i normalize EDİLMEZ.
22. 🔴 **[o77] Canlı turda Ctrl+Shift+R YAPMA.** Hard reload drift'in SharedWorker'ını öldürür:
    ekran **bomboş** kalır, **konsolda hata olmaz**. Çözüm: Chrome'u tamamen kapat–aç. Ek tuzak:
    CanvasKit canvas'ı `flt-glass-pane`in SHADOW ROOT'unda ⇒ `querySelectorAll('canvas')` GÖREMEZ.
23. **[o78 ÖLÇÜLDÜ] `'İ'.toLowerCase()` VM'de `[105]`, dart2js'te `[105, 775]`** ⇒ katlama
    tablosundan `İ` silen mutant VM'de ÖLMEZ ama WEB'de arama kopardı; test TABLOYU BİREBİR sınar.
25. **[o78 ÖLÇÜLDÜ] `hintText` hiçbir mevcut kapıya görünmez** (`Text(` yok ⇒ statik tarayıcı
    görmez; semantik düğümde label/value boş ⇒ kontrast kapısı ATLAR). TEK pin
    `test/arama_dilimi_test.dart`.
27. 🔴 **[o78 İKİNCİ KEZ ISIRDI] `dart format lib/` YASAK** — depo format-temiz DEĞİL; 10 ilgisiz
    dosyayı yeniden biçimlendirdi ve `analyze` 4 yeni uyarı verdi. Yalnız DOKUNULAN dosyada koş.
28. **[o78 KİLİT — Onur, 16 Ağu] Ekleme süzgeçleri SIFIRLAR** (arama + etiket çipi). Sıfırlama
    SENKRON ve yalnız `onEkle` ateşlenince. 🔴 **[o85] AKTİF LİSTE BUNA DAHİL DEĞİL** — liste
    süzgeç değil **BAĞLAM**tır, sıfırlanmaz; `test/liste_baglam_test.dart` ısırıyor.
29. 🔴 **[o81] `DEV_USER_ID` iki tarafta AYNI olmalı:** `docker-compose.yml:31` web istemcisini
    `deadbeef-0000-4000-8000-000000000001` ile derler; APK define'sız derlenirse **rastgele**
    kullanıcı üretir ⇒ emülatör ile tarayıcı birbirini GÖRMEZ. `SENKRON_SUNUCU_URL` = `main.dart:25`.
32. **[o85-A] `projeId`/`fields:projectId` ÇAKIŞMA TESPİTİNE GİRMEZ** — `priority`/`dueAt` ile aynı
    sınıf: `kanonikDize` çağrılmaz, `cakismaKayitlari`'na yazılmaz; LWW sessizce kazanır/kaybeder.
33. 🔴 **[o85-A ÖLÇÜLDÜ] Kanal-adı asimetrisi UYUYOR:** fractional alanlar (`pos`/`listPos`/
    `boardPos`) snapshot'ta **`scalars[]`** (`fields:$ad`), artımlıda **`order` haritası**
    (`order:$ad`) gelir — AYNI alan, İKİ `alan` dizgesi ⇒ `UzakAlanDurumu` PK'sinde iki satır.
    Bugün etkisiz; kanal açılınca o84'le AYNI SINIF sessiz-kayıp riski — **İLK ÖLÇÜLECEK yer**.
34. **[o85-A · o86-A] Dilim 2-3'ün canlı ölçümü PROTOKOL SEVİYESİNDEDİR** (`/v1/sync` HTTP betiği);
    **Flutter UI canlı koşturulmadı**, ekran widget testleriyle ölçüldü (o83-G ile aynı sınır).
36. **[İŞLEYİŞ md.4] Builder beyanı örneklemesi üç dilim üst üste TUTTU** (o85-A2 bayt-özdeşlik ·
    o86-A verify/EXPLAIN · o86-A3 test sayısı) — hiçbir dilim %100 doğrulamaya DÖNMEDİ.
37. 🔴 **[o86] `outbox.owner_id` = YAZAN; `projects.owner_id` = PROJE SAHİBİ.** Erişim kümesi
    `project_access` görünümüdür, ham `project_members` sormak yasak (mimari testi). Snapshot varlık
    listesi outbox GEÇMİŞİNDEN değil GÜNCEL `tasks`/`projects`ten okunur.
38. 🔴 **[o86 · BEŞ ISIRIK] Yetki/görünürlük op'un KENDİ beyanına bakarsa kaçış doğar:** POST-op
    scope · `isNewEntity` koşulsuz KABUL · tahmin edilen `projectId` · `owner_id`in sonsuz
    görünürlüğü · scope'suz görevde sahipliğin sorulmaması. KAYNAK ve HEDEF scope'un ikisi,
    kapsamsızda `tasks.owner_id` sorulur. "A pull" TAZE snapshot'la ölçülürse YALAN söyler.
39. **[o86-A3 BEYAN] `tasks.owner_id` ON CONFLICT'ta güncellenmez** (ilk yazan sabit) ⇒ üyenin
    projede yarattığı görevi sahip koparırsa görev **üyenin** kutusuna düşer ve sahip artık ona
    yazamaz. Sahiplik devri op'u YOK (README'de).

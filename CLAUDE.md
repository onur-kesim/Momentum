# Momentum — PROJE ESASLARI (CLAUDE.md)
`MOD: NORMAL`

> Bu dosya ≤ 8 KB, projenin **TEK talimat dosyasıdır**; yanında tek `DURUM.md` yaşar (≤ 8 KB).
> `arsiv/` append-only tarihçedir, **AÇILIŞTA OKUNMAZ**, hükmü kalkmıştır — yalnız *"bu karar neden
> alındı?"* diye sorulunca açılır. Yeni canlı defter AÇILMAZ. Bu iki dosyaya kapı/mutant yazılmaz.

## 1. NE

Çok platformlu görev yönetimi (to-do): **Flutter** (Web + Android canlı, iOS yalnız CI'da —
Mac yok) + **N-katmanlı .NET/ASP.NET Core** + **PostgreSQL**. İşe-alım/portfolyo ödevi; odak mimari
ve kod kalitesi. Kapsam otoritesi **`docs/ODEV.md`**. Canlı demo README'de.
**İŞ BÖLÜMÜ [Onur, 16 Ağu]:** ürün kodunu **Claude Code** yazar; Cowork tasarım · iş emri · denetim ·
orkestrasyon · hafıza yapar, **kod yazmaz**.

## 2. BİTTİ LİSTESİ (kutu **9 Eyl 2026** · sayaç DURUM.md ilk satırı · tarihler DURUM'da)

- [x] Görev ekle · başlığını düzenle · tamamla
- [x] Görevi sil (onay sorarak)
- [x] Göreve **öncelik** ve **son tarih** ver, ikisini de listede gör
- [x] Göreve **etiket** ekle ve etikete göre **süz**
- [x] Görevleri **başlıkta ara**
- [x] Görevi **tek satır doğal dille** ekle
- [x] İnternetsiz çalış, veri kalıcı, bağlantı gelince kendiliğinden eşitle
- [x] İki cihaz aynı görevi değiştirince **çakışma görünür**, kazanan anlaşılır
- [x] Canlı adresten açılır; tema sistemin **açık/karanlık** ayarına uyar
- [x] Depo klonlanır, README'deki komutla testler **yeşil** koşar
- [x] **Hesap aç, giriş yap;** kendi görevlerini gör, başkasınınkini görme — *19 Ağu*
- [x] Görevleri **listelere** ayır, görevi listeye taşı — *20 Ağu*
- [x] **İki kullanıcı bir listeyi paylaşır;** birinin yazdığı ötekinin ekranında belirir — *6 Eyl*

*Bu listede yasak: spec/ADR/kapı/mutant/borç. Madde eklemek §5'e kesme yazmadan olmaz.*

## 3. SIRADAKİ İŞ (tek dikey dilim)

🟢 **TESLİM EDİLDİ — `v1.1.0`, 6 Eyl 2026.** DİLİM 3 (işbirliği) §F vitrininde **9/9**
(`KANIT/o86F`). Release yayında ve `latest`; etiket **`22fa0b2`**, üç kapı (`ci #85` · `paket #18` ·
`pages #17`) **aynı sha'da yeşil**; yayınlanan APK indirilip **sha256 + `aapt dump badging`** ile
doğrulandı (`1.1.0`/`2`). **AÇIK DİLİM YOK.**
🔴 **ADR/spec YAZILMAZ** (İŞLEYİŞ md.4): kimliği bir kez **altı kâğıt kapı turu öldürdü, 30 gün**
(ÖDEV §6.1). Hatırlatıcı 4 Eyl, **tekrar** 5 Eyl kesildi; kesme sırası TÜKENDİ.

## 4. ORTAM MAYINLARI (yalnız ÖLÇÜLMÜŞ)

1. **git okuma:** `--no-optional-locks` zorunlu; mount'ta tüm-ağaç `status`/`diff` 45 sn tavanını
   aşar ve `.git/index.lock` bırakır ⇒ **daima dar yol**, tur sonunda **kilidi yokla**.
   `rev-parse --short` **tek** revizyon alır (iki verilirse "Needed a single revision").
2. **git yazma:** `git add -A` **YASAK** (başka elin işini kör alır) → yol belirt. Mesaja **çift
   tırnak yazma**. **Mount'tan commit YASAK** (DC ya da Claude Code). **PUSH ONUR'DA.**
   Author `onurkesimbjk@gmail.com`.
3. **flutter `.bat`'tir** → tam yol `C:\src\flutter\bin\flutter.bat` (subprocess PATHEXT'i çözmez).
   DC kabuğunda `PROGRAMFILES(X86)` enjekte edilmeden çöker; `--platform chrome` sonuç üretmez.
4. **Kodlama:** yol **saf ASCII** (Türkçe karakter `build_runner`, `analyze`, AGP, `.ps1` kırdı).
   `python` stdout cp1254 ⇒ `sys.stdout.reconfigure(encoding="utf-8")`.
5. **Geri alım:** `* text=auto eol=lf` `core.autocrlf`'i **ezer**. Mount'ta `git restore`/`checkout
   --` **ÇALIŞMAZ** → `git show HEAD:<yol> > <yol>`; sonuç **sha256 ile** doğrulanır.
6. **`verify.ps1`, `Momentum.Api` ayaktayken koşulamaz** (MSB3026/3027) ⇒ sıra: cihaz kanıtı →
   backend kapat (`netstat -ano | findstr :5298` **boş**) → `verify.ps1`. Kapatma Onur'un izniyle.
7. **Backend:** `ConnectionStrings__Momentum` yoksa host **DB'siz** açılır ama port **dinler** ⇒
   hazırlık portla ölçülmez: `/health/ready` 200 + `POST /v1/sync` başlıksız 401, `X-Momentum-Dev-User`
   ile 200. `ASPNETCORE_ENVIRONMENT=Development` yoksa her istek 401.
8. **Mount'ta `os.remove`/`unlink` YASAK** → artık dosya `arsiv/_SILINECEKLER/<oturum>/`'e `mv`
   edilir; kalıcı silme Onur'da.
9. **Bulut ≠ cihaz:** Cowork UTC koşar ⇒ tarih `TZ='Europe/Istanbul' date` ile ölçülür; **CI
   `istemci` işi de `TZ: Europe/Istanbul`** [o77]. Bulut tarayıcısı canlı kanıt DEĞİLDİR.
10. **Kapı bütçesi (İŞLEYİŞ md.3):** `araclar/` ÷ **el yazımı** `src/` ≤ %10 (üretilen/vendor
    `.g.dart` `.wasm` `.lock` paydaya girmez — ham `src/` %71 üretilmişti, kapı kördü).
    **8 Eyl: 2867/18435 = %15,6 🔴** ⇒ yeni kapı yazılmaz.

## 5. KAPSAM DIŞI (kesilenler — README'ye de yazılır)

- **[18 Ağu GERİ ALINDI]** liste · tekrar · hatırlatıcı · işbirliği · kimlik **§2'ye taşındı**,
  kutu 2 Eyl'e uzadı [Onur kilidi]. `v1.0.1` bunlar kesikken çıktı; yeni teslim `v1.1.0`.
- **[4 Eyl KESİLDİ] Hatırlatıcı** (bildirim): kutu 2 Eyl'de doldu, İŞLEYİŞ md.1 gereği madde
  kesildi [Onur kilidi, 4 Eyl]. Kesme sırası kilitliydi: hatırlatıcı → tekrar.
- **[5 Eyl KESİLDİ] Tekrar eden görev:** kutuda tek dilim kaldı, İŞLEYİŞ md.1 gereği süre
  uzatılmadı [Onur kilidi, K-o88/7 — kesme sırasının SON adımı]. README'ye de yazıldı.
- **[o85 KESİLDİ] Proje/liste klasörü** (üst kap): yeni entityType + materyalizasyon + registry
  dokunuşu demekti [Onur kilidi, 19 Ağu]. Üründe "Liste" denen kap kodda/telde **`Project`**'tir.
- **Windows masaüstü hedefi yok** [Onur, 16 Ağu]: `src/client/` yalnız `android`/`ios`/`web` taşır;
  Windows'ta uygulama **tarayıcıdan** çalışır.
- **[o77 kesildi]** Ayrıştırma her şeyi yutunca (`#iş !p1 yarın`) hata metni YOK: metin alanda kalır.
- **Zaten kapsam dışı (ODEV §4.1/§6.1):** AI asistan · Google Takvim · Kanban/Takvim · iOS cihaz ·
  parola sıfırlama · e-posta doğrulama · OAuth · 2FA · RBAC.

---

## İŞLEYİŞ (v2 — TESLİM KURALLARI'nın halefi; değişmez blok)

1. **Takvim kutusu:** her maddeye gün verilir; kutu dolarsa MADDE kesilir, süre uzamaz; kesilen §5'e ve README'ye yazılır. Kalan madde > kalan gün ise hemen kes.
2. **Dikey dilim pazarlıksız** (§3'teki tanım). Dilim sonunda çalışan üründe elle tek doğrulama.
3. **Kapı bütçesi %10:** projeye özel denetim/araç kodu, ürün kodunun %10'unu geçemez (normal birim/widget testleri orana girmez — onlar üründür). Ölçüm tek satırdır, bu dosyanın §4'üne yazılır ve CI'da koşar; ölçüm için ayrı araç dosyası açılmaz. Aşımda yeni kapı yazılmaz: ya kapı silinir ya kural tek cümleye döner.
4. **Kâğıt denetim turu = 0.** Spec/ADR/plan/iş emri denetlenmez; denetim yalnız koşan artefakta. "Üreten ≠ denetleyen" TESLİMDE uygulanır: kullanıcıya değen her teslimde TEK bağımsız tur, CANLI çıktıya bakılır (`MOD: KRİTİK` ise 2 tur). Ajan beyanları güvenilirdir; teslim başına rastgele 1 beyan doğrulanır — sonucu DURUM.md "bilinen sınırlar"a tek cümle yazılır; tutmazsa o dilim %100 doğrulamaya döner. Ayrı sicil defteri açılmaz.
5. **Açılış ≤3 komut:** git durumu · DURUM.md · test durumu. Kapılar CI'da koşar, oturumda değil.
6. **Borç defteri tutulmaz.** Üç seçenek: ŞİMDİ YAP · KAPSAMDAN KES (§5 + README) · SİL.
7. **Tek vitrin.** İkinci "vay" birincinin bitişini geciktirir; diğerleri §5'e.
8. **Kural yaşam döngüsü:** kalıcı kural ancak aynı sınıf İKİ kez ısırınca doğar; ya CI'da mekanik koşar ya tek cümledir; §4'e girer ve girerken bir satır silinir. Kurallar birbirine kimlik numarasıyla atıf veremez. Olay bir kez ısırdıysa yeri DURUM.md "bilinen sınırlar"dır, kural değil.
9. **Haftalık tek soru:** "Bu hafta bitti listesinde kaç madde ✅'ye döndü?" Cevap 0 ise o hafta yapılan her şey gözden geçirilir.
10. **Taban:** bu dosya için kapı/mutant/altın küme/denetim turu yazılmaz. Çelişki çıkarsa öncelik: MUTLAK SINIRLAR > global anayasa > bu dosya > diğer her şey.

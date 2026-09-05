/// F6 -- 13 kullanici dizgesi TEK YERDE (GOREV-R10/K75: iki yeni dizge BU
/// SAYIYA DAHIL DEGIL, ayri EK grupta -- fixture/metinler-kilit.json F6
/// kilidini bozmaz). Kilit aninda araclar/fixture/metinler-kilit.json'a
/// dondurulur; G5 bu dosyayi fixture ile birebir karsilastirir. DESIGN.md
/// ayristirilmaz (§9.1 F6).
class Metinler {
  Metinler._();

  // Gorunur (8)
  static const String yalnizcaBuCihazda = 'Yalnızca bu cihazda';
  static const String gonderiliyor = 'Gönderiliyor';
  static const String cevrimdisiKaydedildi =
      'Çevrimdışısınız. Değişiklikler kaydedildi.';
  static const String cakismaVar = 'Bu görev başka bir cihazda da değişti.';
  static const String bosDurum = 'Henüz görev yok. Aşağıdan ekleyin.';
  static const String birSeylerTersGitti = 'Bir şeyler ters gitti.';
  static const String yenidenDene = 'Yeniden dene';
  static const String yukleniyor = 'Yükleniyor';

  // Semantics duyurusu (5)
  static const String duyuruGorevlerYukleniyor = 'Görevler yükleniyor';
  static const String duyuruSenkronizeEdildi = 'Senkronize edildi';
  static const String duyuruCevrimdisi = 'Çevrimdışı';
  static const String duyuruCakismaVar = 'Çakışma var';
  static const String duyuruHata = 'Hata';

  // GOREV-R10 [K75] -- 'gonderilmemis' durumu (DESIGN.md v2 §4/§6).
  static const String gonderilmemisDegisiklik = 'Gönderilmemiş değişiklik';
  static const String duyuruGonderilmemisDegisiklik =
      'Gönderilmemiş değişiklik var';

  // GOREV-A7 [K85 / spec v4 §4/D-A7-1] -- ROZETIN EKRANDA GORUNEN KISA
  // metinleri. Yukaridaki "Gorunur (8)" grubu DEGISMEDI: o dizgeler artik
  // Semantics(label:)'da yasiyor ve ekran okuyucu ONLARI okuyor -- bilgi
  // KAYBI YOKTUR. Burada yasayanlar yalniz GOZLE gorulen kisaltmalardir.
  //
  // Neden ayri grup: bunlar F6'nin KILITLI 13 dizgesine DAHIL DEGILDIR
  // (GOREV-slice-3b §T-metin / K59'un "13 dizge" kilidi bozulmaz) --
  // GOREV-R10'un `gonderilmemisDegisiklik` icin kurdugu ayni EK deseni.
  // fixture/metinler-kilit.json'da "rozetKisaGorunur" grubunda aynalanir ve
  // a11y_kapisi_test.dart bu grubu da BIREBIR sinar (uc dosya es zamanli).
  //
  // OLCULDU (flutter_test fontu, 236 px alan, olcek 2.0x -- KANIT/A7/
  // 02-COZUM-OLCUM.txt): "Bu cihazda" 262,5 px / 2 satir · "Cevrimdisi"
  // 262,5 / 2 · "Gonderilmedi" 315,0 / 2. Reddedilenler (3 satir):
  // "Cevrimdisi kaydedildi" 551,3 · "Yalniz bu cihazda" 446,3.
  // 'kuyrukta' icin AYRI sabit YOKTUR: `gonderiliyor` (315,0 / 2 satir) hem
  // tam hem kisa metindir; kopya sabit yazmak F6'yi ikiye bolerdi.
  static const String rozetKisaYerel = 'Bu cihazda';
  static const String rozetKisaCevrimdisi = 'Çevrimdışı';
  static const String rozetKisaGonderilmedi = 'Gönderilmedi';

  // EK -- F6'nin 13 dizgesine DAHIL DEGIL (G5 fixture karsilastirmasi bunu
  // icermez). Sabit kontrol etiketidir, durum dizgesi degildir; GorevEkleAlani
  // ikon-yalniz ekle dugmesi tasidigindan A11Y-3 (ikon-yalniz buton yasak)
  // bunu zorunlu kilar, DESIGN.md §6 bu eylem icin ayri bir metin tanimlamaz.
  static const String ekleDugmesi = 'Ekle';

  // GOREV-SS2 [D-SS2-8] -- cakisma cozum ekrani. F6'nin 13 dizgesine DAHIL
  // DEGIL (GOREV-R10/A9'un ayni EK deseni). Kanonik dize (D-SS2-4) EKRANDA
  // GOSTERILMEZ -- bu etiketler yerellestirilmis karsiliklardir.
  static const String cakismaKaydiYok = 'Çakışan bir değişiklik bulunamadı.';
  static const String cakismaAlaniBaslik = 'Başlık';
  static const String cakismaAlaniTamamlanma = 'Tamamlanma durumu';
  static const String cakismaDegerTamamlandi = 'Tamamlandı';
  static const String cakismaDegerAcik = 'Açık';
  static const String cakismaBenimki = 'Benimki';
  static const String cakismaOnlarinki = 'Onlarınki';
  static const String cakismaBenimkiniTut = 'Benimkini tut';
  static const String cakismaOnlarinkiniAl = 'Onlarınkini al';

  // GOREV-W2 [D-W2-3/D-W2-8] -- depolama seridi. F6'nin 13 dizgesine DAHIL
  // DEGIL (GOREV-R10/SS2'nin ayni EK deseni) -- K59'un kilitli "13 dizge"
  // iddiasi bu ucle bayatlamaz.
  static const String depolamaGeriDususu =
      'Veriler tarayıcı deposunda tutuluyor.';
  static const String depolamaKaliciDegil =
      'Veriler kalıcı DEĞİL: sekme kapanınca silinir.';
  static const String duyuruDepolamaGeriDususu = 'Depolama geri düşüşü';

  // IS-EMRI-o68 -- duzenleme ikonu + diyaloğu. F6'nin 13 dizgesine DAHIL
  // DEGIL (ayni EK deseni). Ayni dize hem ikonun tooltip'i hem diyalog
  // basligi olarak kullanilir -- ikisi de ayni eylemi adlandirir, ayri dize
  // UYDURULMAZ. ODEV.md §4(a): diyalog artik baslik + oncelik + son tarih
  // duzenledigi icin dize 'Başlığı düzenle'den 'Görevi düzenle'ye gecti
  // (F6'nin kilitli 13'unde DEGILDI, kilit bozulmadi).
  static const String gorevDuzenle = 'Görevi düzenle';
  static const String iptalDugmesi = 'İptal';
  static const String kaydetDugmesi = 'Kaydet';

  // IS-EMRI-o72 -- gorev silme eylemi (ikon + onay diyaloğu). F6'nin 13
  // dizgesine DAHIL DEGIL (ayni EK deseni, IS-EMRI-o68 emsali). Ayni dize
  // hem ikonun tooltip'i hem diyalog basligi olarak kullanilir.
  static const String gorevSil = 'Görevi sil';
  static const String gorevSilOnay = 'Bu görev silinsin mi?';

  // ODEV.md §4(a) -- oncelik + son tarih dilimi. F6'nin 13 dizgesine DAHIL
  // DEGIL (o68/o72'nin ayni EK deseni).
  static const String oncelikBasligi = 'Öncelik';
  static const String oncelikYok = 'Yok';
  static const String oncelikYuksek = 'Yüksek';
  static const String oncelikOrta = 'Orta';
  static const String oncelikDusuk = 'Düşük';
  static const String sonTarihBasligi = 'Son tarih';
  static const String sonTarihSec = 'Tarih seç';
  static const String sonTarihiTemizle = 'Son tarihi temizle';

  // ODEV.md §4(a) -- etiket dilimi. F6'nin 13 dizgesine DAHIL DEGIL
  // (o68/o72/oncelik diliminin AYNI EK deseni).
  static const String etiketlerBasligi = 'Etiketler';
  static const String etiketEkle = 'Etiket ekle';
  static const String etiketKaldir = 'Etiketi kaldır';

  // ODEV.md §4(a) -- baslikta ARAMA dilimi. F6'nin 13 dizgesine DAHIL DEGIL
  // (o68/o72/oncelik/etiket diliminin AYNI EK deseni).
  //
  // `aramaEslesmeYok` BILEREK `bosDurum`dan AYRI bir dizgedir: gorevleri OLAN
  // kullaniciya "Henüz görev yok." demek YALAN SOYLEYEN bir ekrandir (kilit
  // maddesi 3).
  // KISA TUTULDU (OLCULDU 15 Agu, denetim): 320 dp genislikte 'Görevlerde
  // ara' 1.0x'te bile kirpiliyordu (cizilen 220 px / gereken 231 px) ve
  // `hintText` bir `Text(` OLMADIGI icin R1 statik tarayicisi onu GORMEZ ⇒
  // kirpma tam da doktrinin yasakladigi bicimde SESSIZ olurdu. Buyutec
  // ikonuyla birlikte 'Ara' anlami tasir.
  static const String aramaIpucu = 'Ara';
  static const String aramaEslesmeYok = 'Eşleşen görev yok.';
  static const String suzgecleriTemizle = 'Süzgeçleri temizle';

  /// Suzme seridindeki "suzme yok" cipi. Etiket metni ile CAKISABILIR
  /// ("Tümü" adli bir etiket) -- cip SECIMI metne gore DEGIL, `secili ==
  /// null` durumuna gore verilir, bu yuzden cakisma davranisi bozmaz.
  static const String etiketTumu = 'Tümü';

  /// Ay kisaltmalari -- `intl` paketi BILEREK eklenmedi (yeni bagimlilik =
  /// lisans + CVE kapisi; tek bir tarih etiketi icin orantisiz). Sira
  /// PAZARLIKSIZ 1..12'dir ve `DateTime.month - 1` ile indislenir.
  static const List<String> ayKisaltmalari = [
    'Oca',
    'Şub',
    'Mar',
    'Nis',
    'May',
    'Haz',
    'Tem',
    'Ağu',
    'Eyl',
    'Eki',
    'Kas',
    'Ara',
  ];

  // IS-EMRI-o83 DILIM 1 (KIMLIK) -- giris/kayit ekrani. F6'nin 13 dizgesine
  // DAHIL DEGIL (yukaridaki EK gruplarin AYNI deseni).
  static const String girisEkraniBasligi = 'Momentum';
  static const String epostaEtiketi = 'E-posta';
  static const String sifreEtiketi = 'Parola';
  static const String girisYapDugmesi = 'Giriş yap';
  static const String kayitOlDugmesi = 'Kayıt ol';
  static const String hesabinYokMu = 'Hesabın yok mu? Kayıt ol';
  static const String zatenHesabinVarMi = 'Zaten hesabın var mı? Giriş yap';
  static const String girisHatasiGecersizBilgi = 'E-posta veya parola hatalı.';
  static const String girisHatasiEpostaKullanimda = 'Bu e-posta zaten kayıtlı.';
  static const String girisHatasiGenel =
      'Bir şeyler ters gitti, tekrar deneyin.';
  static const String girisHatasiAg =
      'Sunucuya ulaşılamadı, bağlantını kontrol et.';
  static const String girisHatasiKisaSifre = 'Parola en az 8 karakter olmalı.';
  static const String oturumAcikBekleniyor = 'Oturum hazırlanıyor…';
  static const String cikisYapDugmesi = 'Çıkış yap';

  // IS-EMRI-o85-A -- DILIM 2 "Liste" (K1: uruntteki "Liste" = telde/kodda
  // `Project`). F6'nin 13 dizgesine DAHIL DEGIL (yukaridaki EK gruplarin
  // AYNI deseni).
  static const String gelenKutusu = 'Gelen Kutusu';
  static const String listeYeniDugmesi = 'Yeni liste';
  static const String listeYenidenAdlandirDugmesi = 'Yeniden adlandır';
  static const String listeSil = 'Listeyi sil';
  static const String listeSilOnay =
      'Bu liste silinsin mi? İçindeki görevler Gelen Kutusu\'na taşınır.';
  static const String listeBasligi = 'Liste';

  /// D5: liste boşken mevcut `bos_durum.dart` deseni, liste adıyla. `Metinler
  /// .bosDurum` gibi SABIT DEGIL (liste adi calisma-zamaninda gelir) --
  /// EK grubun tek FONKSIYON uyesi, F6 kilidi yalniz `const String`leri sinar.
  static String bosDurumListe(String listeAdi) =>
      '$listeAdi listesinde henüz görev yok.';

  // IS-EMRI-o86-B §C/§D -- DILIM 3 "Paylaş" (davet). K-o88/3 PAZARLIKSIZ:
  // reddedilen davet KULLANICIYA GORUNMEK ZORUNDADIR -- sessizlik yasak.
  // K-o88/4 (D-D2 kirmizisi sonrasi Onur kilidi, 4 Eyl): uc ayri IconButton
  // yerine TEK menu -- `listeMenuAc` menunun KENDI tooltip'i, `listePaylasDugmesi`
  // hem madde basligi hem diyalog basligi olarak YENIDEN KULLANILIR (listeSil'in
  // AYNI cok-rollu deseni).
  static const String listeMenuAc = 'Liste seçenekleri';
  static const String listePaylasDugmesi = 'Listeyi paylaş';
  static const String listePaylasGecersizEposta =
      'Geçerli bir e-posta adresi gir.';
  static const String listePaylasKullaniciYok =
      'Bu e-postayla kayıtlı kullanıcı yok.';
  static const String listePaylasBaglantiYok =
      'Bağlantı yok, davet gönderilemedi.';
  static const String listePaylasKendiniDavetEdemezsin =
      'Kendini davet edemezsin.';
  static const String listePaylasYenidenDene = 'Gönderilemedi, yeniden dene.';
  static const String listePaylasDavetDugmesi = 'Davet et';

  /// §C adım 6 başarı: `eposta` çalışma-zamanında gelir -- `bosDurumListe`
  /// ile AYNI desen (F6 kilidi yalnız `const String`leri sınar).
  static String listePaylasBasarili(String eposta) =>
      '$eposta listeye eklendi.';

  /// §C adım 6 red (D-C1 PAZARLIKSIZ): ham `sonHataKodu` metne EKLENIR,
  /// gizlenmez -- destek/denetim onu ekrandan OKUYABILSIN.
  static String listePaylasYetkiYok(String sonHataKodu) =>
      'Bu listeyi paylaşma yetkin yok. ($sonHataKodu)';
}

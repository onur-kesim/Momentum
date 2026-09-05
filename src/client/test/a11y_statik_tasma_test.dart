@TestOn('vm')
library;

// G5 -- A11Y-4 STATIK YARISI: "kirpma sessizdir" (Z16/spec SS5 G5). Bir
// Text() dugumu textScaler 2.0 altinda GORUNMEDEN kirpilabilir -- bu, calisan
// bir widget testinin YAKALAYAMAYACAGI bir sessizlik turudur. Bu yuzden
// lib/sunum ve lib/vitrin kaynagi dogrudan TARANIR.
//
// OLCULMUS KURAL [M16'nin ogrettigi]: 'maxLines VARLIGI' TEK BASINA yeterli
// DEGILDIR -- maxLines, overflow:ellipsis OLMADAN verilirse Flutter'in
// VARSAYILANI TextOverflow.clip'tir: metin "..." GOSTERMEDEN SESSIZCE kirpilir.
// Bu yuzden ZORUNLU olan overflow: TextOverflow.ellipsis'in KENDISIDIR (maxLines
// bunun YERINE GECMEZ, onunla BIRLIKTE kullanilabilir). dart:io kullanir --
// @TestOn('vm') PAZARLIKSIZDIR (web'de dart:io yok).
//
// GOREV-A9 [K93/spec SS5/G5] -- GENISLETME: R1 (mevcut, degismez) + R2 (YENI:
// ellipsis tasiyan HER govde maxLines de tasir) + R4 (YENI: pozitif kontrol --
// tarayicinin bulduğu Text( aday sayisi = 55 [taban 8 -> 12 -> 13 -> 16 -> 20 -> 25 -> 29 -> 30 -> 34 -> 47 -> 55
// guncellendi;
// kanonik deger R4'un expect'indedir, bu satir ona atiftir],
// arac kendini kanitlar). R3 (govde
// toplayicinin KENDISI dogru topluyor mu) ayri bir test DEGILDIR -- yalniz
// mutantlarla (M105/M107) sinanir (spec SS7 kriter 4'un sayi hesabi: yalniz
// R2+R4 +2 test ekler).
//
// ORTAK GOVDE TOPLAYICI (R1/R2/R4 UCU DE KULLANIR, kopyalanmaz -- kanonik-kopya
// bu projede bes kez isirdi): parantez-derinligi ile Text(/Text.rich( govdesini
// toplar, YORUMLARI (`//`dan sonrasi) ATAR -- F6 testinin (asagida) yontemiyle
// AYNI. Pencere 25 satir (mevcut sinir, spec S9 -- DEGISTIRILMEZ).

import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// Tek bir `Text(`/`Text.rich(` cagrisi: konum + rapor icin ORIJINAL ilk
/// satir + kural kontrolu icin YORUMDAN ARINMIS govde.
class _TextCagrisi {
  final String dosyaYolu;
  final int satirNo; // 1-based
  final String ilkSatirHam; // orijinal (yorumlu) ilk satir, rapor icin
  final String govde; // yorumdan arinmis govde, kural kontrolu icin

  const _TextCagrisi({
    required this.dosyaYolu,
    required this.satirNo,
    required this.ilkSatirHam,
    required this.govde,
  });
}

List<File> _taranacakDosyalar() {
  final kok = Directory('lib');
  return <File>[
    ...Directory(
      '${kok.path}/sunum',
    ).listSync(recursive: true).whereType<File>(),
    ...Directory(
      '${kok.path}/vitrin',
    ).listSync(recursive: true).whereType<File>(),
  ].where((f) => f.path.endsWith('.dart')).toList();
}

final RegExp _textCagriDeseni = RegExp(r'\bText(\.rich)?\s*\(');

/// GOREV-A9 [K93]: R1 VE R2'NIN (VE R4'UN) ORTAK govde toplayicisi.
/// `pencere` parametresi M105/M107'nin sinadigi ayardir (varsayilan 25).
List<_TextCagrisi> _textCagrilariniTopla(File dosya, {int pencere = 25}) {
  final satirlarHam = dosya.readAsLinesSync();
  // Yorumdan arindirilmis satirlar -- INDEX korunur (satir numaralari kaymaz).
  final satirlarTemiz = satirlarHam.map((s) {
    final k = s.indexOf('//');
    return k < 0 ? s : s.substring(0, k);
  }).toList();

  final sonuc = <_TextCagrisi>[];
  for (var i = 0; i < satirlarTemiz.length; i++) {
    if (!_textCagriDeseni.hasMatch(satirlarTemiz[i])) continue;
    // Text(...) cagrisi genelde birden fazla satira yayilir -- kapanan
    // parantezi bulana kadar (basit derinlik sayaci ile) govdeyi topla.
    var derinlik = 0;
    var basladi = false;
    final govde = StringBuffer();
    for (var j = i; j < satirlarTemiz.length && j < i + pencere; j++) {
      final satir = satirlarTemiz[j];
      govde.writeln(satir);
      for (final ch in satir.split('')) {
        if (ch == '(') {
          derinlik++;
          basladi = true;
        } else if (ch == ')') {
          derinlik--;
        }
      }
      if (basladi && derinlik <= 0) break;
    }
    sonuc.add(
      _TextCagrisi(
        dosyaYolu: dosya.path,
        satirNo: i + 1,
        ilkSatirHam: satirlarHam[i].trim(),
        govde: govde.toString(),
      ),
    );
  }
  return sonuc;
}

void main() {
  test(
    'R1: lib/sunum ve lib/vitrin altindaki her Text( cagrisi korunakli (overflow/maxLines)',
    () {
      final dosyalar = _taranacakDosyalar();
      expect(
        dosyalar,
        isNotEmpty,
        reason: 'lib/sunum + lib/vitrin taranacak dosya bulunamadi',
      );

      final korunmasizlar = <String>[];
      for (final dosya in dosyalar) {
        for (final cagri in _textCagrilariniTopla(dosya)) {
          // maxLines TEK BASINA YETMEZ (M16): overflow:ellipsis OLMADAN maxLines,
          // Flutter varsayilani TextOverflow.clip'e duser -- SESSIZ kirpma.
          final korunakli = cagri.govde.contains('TextOverflow.ellipsis');
          if (!korunakli) {
            korunmasizlar.add(
              '${cagri.dosyaYolu}:${cagri.satirNo}: ${cagri.ilkSatirHam}',
            );
          }
        }
      }

      expect(
        korunmasizlar,
        isEmpty,
        reason:
            'Asagidaki Text() cagrilari overflow: TextOverflow.ellipsis tasimiyor -- '
            'textScaler 2.0 altinda SESSIZCE kirpilabilir (A11Y-4, M16):\n'
            '${korunmasizlar.join('\n')}',
      );
    },
  );

  test(
    'R2: ellipsis tasiyan her govde maxLines de tasir (GOREV-A9, sinif kapisi)',
    () {
      final dosyalar = _taranacakDosyalar();
      expect(
        dosyalar,
        isNotEmpty,
        reason: 'lib/sunum + lib/vitrin taranacak dosya bulunamadi',
      );

      final ihlaller = <String>[];
      for (final dosya in dosyalar) {
        for (final cagri in _textCagrilariniTopla(dosya)) {
          final ellipsisVar = cagri.govde.contains('TextOverflow.ellipsis');
          if (!ellipsisVar) continue; // R1'in konusu, R2'nin degil.
          final maxLinesVar = RegExp(r'\bmaxLines\s*:').hasMatch(cagri.govde);
          if (!maxLinesVar) {
            ihlaller.add(
              '${cagri.dosyaYolu}:${cagri.satirNo}: ${cagri.ilkSatirHam}',
            );
          }
        }
      }

      expect(
        ihlaller,
        isEmpty,
        reason:
            'Asagidaki Text() cagrilari TextOverflow.ellipsis tasiyor ama maxLines '
            'TASIMIYOR -- ellipsis TEK BASINA metni fiilen tek satira indirir ve '
            'fazlasini SESSIZCE atar (B3, KANIT/A7):\n'
            '${ihlaller.join('\n')}',
      );
    },
  );

  test(
    'R4: pozitif kontrol -- tarayicinin buldugu Text( aday sayisi = 55 (arac kendini kanitlar)',
    () {
      final dosyalar = _taranacakDosyalar();
      final adaylar = <String>[];
      for (final dosya in dosyalar) {
        for (final cagri in _textCagrilariniTopla(dosya)) {
          adaylar.add('${cagri.dosyaYolu}:${cagri.satirNo}');
        }
      }
      // OLCULDU (GOREV-A9, oturum 43): bos_durum 1 * cakisma_rozeti 2 *
      // gorev_satiri 1 * hata_durumu 2 * senkron_rozeti 1 * yukleme_durumu 1
      // = 8. 🔴 GOREV-SS2 [T5] TABAN BILEREK GUNCELLENDI: cakisma_rozeti.dart
      // 2 -> 6 Text( cagrisina cikti (bos durum + AppBar basligi + iki buton
      // etiketi + alan etiketi + iki deger blogu basligi -- deger metinlerinin
      // KENDISI zaten sayiliyordu) ⇒ toplam 8 - 2 + 6 = 12. "olcum araci ONCE
      // KENDINI kanitlar" doktrininin bu kapidaki karsiligi.
      // 🔴 GOREV-W2 [T8] TABAN BILEREK GUNCELLENDI 12 -> 13: depolama_seridi.dart
      // TEK BIR Text( dugumu ekledi (spec T3: "TEK BIR Text( dugumu" pini).
      // 🔴 IS-EMRI-o68 TABAN BILEREK GUNCELLENDI 13 -> 16: gorev_satiri.dart'a
      // baslik duzenleme diyaloğu (`_BaslikDuzenleDiyaloguState.build()`) UC
      // YENI Text( cagrisi ekledi (diyalog basligi + Iptal dugmesi + Kaydet
      // dugmesi, ucu de overflow/maxLines korumali) ⇒ 13 + 3 = 16.
      // 🔴 IS-EMRI-o72 TABAN BILEREK GUNCELLENDI 16 -> 20: gorev_satiri.dart'a
      // silme onay diyalogu DORT YENI Text( cagrisi ekledi (diyalog basligi +
      // govde + Iptal dugmesi + Sil dugmesi) ⇒ 16 + 4 = 20. Test ADI da bu
      // turda tazelendi: o68'de pin 13->16 olurken ad "12" kalmisti.
      // 🔴 ODEV.md §4(a) TABAN BILEREK GUNCELLENDI 20 -> 25: gorev_satiri.dart
      // 8 -> 13 Text( SATIRI'na cikti. BES YENI satir: (1) baslik ALTINDAKI
      // meta satiri; (2) 'Öncelik' bolum etiketi; (3) oncelik ciplerinin
      // label'i -- DORT cip TEK KAYNAK SATIRINDAN (`for` dongusu) uretildigi
      // icin tarayici bunu BIR kez sayar; (4) 'Son tarih' bolum etiketi;
      // (5) tarih dugmesinin etiketi. Diyalog basligi ile Iptal/Kaydet
      // dugmeleri ZATEN sayiliyordu (o68'den), yenisi degildir.
      // 🔴 ODEV.md §4(a) ETIKET DILIMI TABAN BILEREK GUNCELLENDI 25 -> 29:
      // DORT YENI Text( SATIRI -- gorev_satiri.dart'ta (1) 'Etiketler' bolum
      // etiketi ve (2) InputChip'in label'i (etiket basina ayri cip, TEK
      // kaynak satiri); gorev_listesi_ekrani.dart'ta (3) suzme seridinin
      // 'Tümü' cipi ve (4) etiket ciplerinin label'i. Satir meta metni
      // ZATEN sayiliyordu (oncelik diliminden) -- etiketler AYNI `Text(`e
      // eklendigi icin yeni aday DEGILDIR.
      // 🔴 TARAYICI TANE DEGIL SATIR SAYAR: ayni satirdaki iki `Text(` BIR
      // aday eder (main dongusu satir basina bir kez `add` eder) -- bu sayi
      // o kurala gore hesaplandi ve bulut aynasinda testin KENDI toplayici
      // mantigi birebir taklit edilerek OLCULDU (varsayilmadi; etiket diliminde
      // testin KENDI toplayicisi 29 saydi).
      // 🔴 ODEV.md §4(a) ARAMA DILIMI TABAN BILEREK GUNCELLENDI 29 -> 30:
      // TEK YENI Text( SATIRI -- bos_durum.dart'in ikinci varyantindaki
      // "Süzgeçleri temizle" BUTON etiketi. Iki varyantin BASLIK metni AYNI
      // `_metin` yardimcisindan cizilir ⇒ o AYNI aday olarak kalir, yeni
      // aday DEGILDIR. Arama alaninin ipucu bir `Text(` degil `hintText`
      // dizgesidir ⇒ tarayiciya HIC gorunmez. Sayi VARSAYILMADI: testin
      // KENDI toplayicisi 30 saydi (bulutta kosuldu, 15 Agu 2026).
      // 🔴 IS-EMRI-o83 (DILIM 1 KIMLIK) TABAN BILEREK GUNCELLENDI 30 -> 34:
      // giris_ekrani.dart (YENI dosya) DORT Text( SATIRI ekledi -- baslik,
      // hata metni, giris/kayit dugmesi etiketi, mod-degistirme baglantisi
      // (dordu de overflow/maxLines korumali). Yeni ekranin KENDISI bu
      // turun konusu -- baska hicbir dosyaya dokunulmadi.
      // 🔴 IS-EMRI-o85-A (DILIM 2 LISTE) TABAN BILEREK GUNCELLENDI 34 -> 47:
      // ON UC YENI Text( SATIRI. gorev_listesi_ekrani.dart'ta ON: Drawer'in
      // `_drawer()`'i UC (Gelen Kutusu basligi, liste basligi -- TEK kaynak
      // satiri, Yeni liste basligi), `_listeSilOnayDiyaloguAc()`'in silme
      // onay diyaloğu DORT (baslik + govde + Iptal + Sil dugmesi, gorev
      // silme onayinin AYNI deseni) ve `_ListeAdiDiyalogu` widget'i UC
      // (diyalog basligi + Iptal + Kaydet dugmesi, ayrinti diyaloğunun AYNI
      // deseni). gorev_satiri.dart'ta UC: 'Liste' bolum etiketi + Gelen
      // Kutusu cip etiketi + liste cipi etiketi (TEK kaynak satiri, oncelik
      // ciplerinin AYNI deseni). Sayi VARSAYILMADI: `git diff | grep -c
      // 'Text('` ile OLCULDU (10 + 3 = 13).
      // 🔴 IS-EMRI-o86-B (DILIM 3 PAYLAS) TABAN BILEREK GUNCELLENDI 47 -> 55:
      // SEKIZ YENI Text( SATIRI, HEPSI gorev_listesi_ekrani.dart'ta.
      // `_ListePaylasDiyalogu` diyalogu DORT (diyalog basligi, hata metni
      // slotu, Iptal dugmesi, Davet et dugmesi) + basari SnackBar'i BIR
      // (`ScaffoldMessenger...Text(Metinler.listePaylasBasarili(...))`) +
      // K-o88/4'un (Onur kilidi, 4 Eyl) Drawer PopupMenuButton'u UC (madde
      // basligi Yeniden adlandir/Paylas/Sil -- eski uc AYRI IconButton'un
      // tooltip'leri Text( SAYILMIYORDU, PopupMenuItem'in govdesi SAYILIYOR;
      // net degisim +3 DEGIL +8, cunku ucuncu IconButton hic COMMIT
      // EDILMEDI -- D-D2 kirmizisi PopupMenuButton'a donusturuldu). Sayi
      // VARSAYILMADI: `git diff -- lib/sunum lib/vitrin | grep -c '^+.*Text('`
      // ile OLCULDU (8, 0 silindi).
      expect(
        adaylar.length,
        55,
        reason:
            'Text( aday sayisi 55 DEGIL -- ya tarayici bozuldu (regex hic '
            'eslesmiyor ⇒ R1/R2 kor) ya taban degisti (yeni bir Text( eklendi/'
            'silindi). Bulunanlar:\n${adaylar.join('\n')}',
      );
    },
  );

  test(
    'metin: F6 dizgeleri lib/sunum + lib/vitrin icinde HAM LITERAL olarak TEKRARLANMAMIS '
    '(tek kaynak metinler.dart -- M10)',
    () {
      // Widget testleri GORUNEN metni Metinler.X ile karsilastirir (find.text) --
      // ama bir bilesen AYNI degeri ham dizge olarak yazsa gorunum FARK ETMEZ,
      // yalniz KAYNAK TARAMASI yakalar. Bu, M10'un ("Metinler.bosDurum yerine
      // duz dizge yaz") tek mekanik kapisidir.
      const f6Dizgeleri = <String>[
        'Yalnızca bu cihazda',
        'Gönderiliyor',
        'Çevrimdışısınız. Değişiklikler kaydedildi.',
        'Bu görev başka bir cihazda da değişti.',
        'Henüz görev yok. Aşağıdan ekleyin.',
        'Bir şeyler ters gitti.',
        'Yeniden dene',
        'Yükleniyor',
        'Görevler yükleniyor',
        'Senkronize edildi',
        'Çevrimdışı',
        'Çakışma var',
        'Hata',
        // GOREV-W2 [T8]: uc yeni sabit (F6'nin KILITLI 13'une DAHIL DEGIL --
        // ayni EK deseni -- ama bu ham-literal-tekrar taramasi onlari da
        // kapsar).
        'Veriler tarayıcı deposunda tutuluyor.',
        'Veriler kalıcı DEĞİL: sekme kapanınca silinir.',
        'Depolama geri düşüşü',
      ];

      final dosyalar = <File>[
        ...Directory('lib/sunum').listSync(recursive: true).whereType<File>(),
        ...Directory('lib/vitrin').listSync(recursive: true).whereType<File>(),
      ].where((f) => f.path.endsWith('.dart'));

      final kacaklar = <String>[];
      for (final dosya in dosyalar) {
        // Yorum satirlarini (// ve ///) at -- dogal dilde bahsetme, KOD DEGIL.
        final govde = dosya
            .readAsLinesSync()
            .map((s) => s.contains('//') ? s.substring(0, s.indexOf('//')) : s)
            .join('\n');
        for (final s in f6Dizgeleri) {
          if (govde.contains("'$s'") || govde.contains('"$s"')) {
            kacaklar.add('${dosya.path}: "$s" ham dizge olarak gecıyor');
          }
        }
      }

      expect(
        kacaklar,
        isEmpty,
        reason:
            'F6 dizgeleri YALNIZ lib/design/metinler.dart\'ta yasamali; '
            'asagidaki dosyalarda HAM LITERAL olarak tekrarlanmis:\n'
            '${kacaklar.join('\n')}',
      );
    },
  );
}

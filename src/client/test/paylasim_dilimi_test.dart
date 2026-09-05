@TestOn('vm')
library;

// IS-EMRI-o86-B §E -- DILIM 3 "Paylaş" (davet) URUN TESTI (kapi DEGIL, %10
// oranina GIRMEZ, IŞLEYIŞ md.3). Her iddianin bir "olmamali" esi vardir
// (DURUM.md "pozitif kontrol" dersi -- bos liste her iddiayi gecirir).
//
// Grup 1 (real Drift, DriftGorevDeposu.uyeEkle -- liste_dilimi_test.dart'in
// AYNI deseni): §A'nin urettigi WireOp'un SEKLI.
// Grup 2 (widget, dialog akisi -- §C adim 1-3): "op URETILMEZ" negatifleri.
// Grup 3 (widget, §C adim 4-6): uc dalli karar (basari/zehirli/bekliyor).
// Grup 4 (real Drift, UzakDegisiklikUygulayici -- liste_dilimi_test.dart'in
// AYNI deseni): uye tarafi -- entityType:'Project' materyalizasyonu.
// Grup 5 (widget, K-o88/4 Onur kilidi 4 Eyl): Drawer menusu.

import 'dart:async';

import 'package:client/ag/kullanici_arama_agi.dart';
import 'package:client/design/metinler.dart';
import 'package:client/senkron/uzak_degisiklik_uygulayici.dart';
import 'package:client/sunum/gorev_listesi_ekrani.dart';
import 'package:client/veri/ayarlar_deposu.dart';
import 'package:client/veri/gorev_deposu.dart';
import 'package:client/veri/hlc.dart';
import 'package:client/veri/veritabani.dart';
import 'package:client/veri/wire_op.dart';
import 'package:drift/drift.dart' hide isNull, isNotNull;
import 'package:drift/native.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

// ============================================================
// Grup 1 + 4 yardimcilari -- liste_dilimi_test.dart'in AYNI deseni.
// ============================================================

class _SayacId {
  int _n = 0;
  String cagir() => 'id-${++_n}';
}

Veritabani _bellekDb() => Veritabani(NativeDatabase.memory());

Future<DriftGorevDeposu> _depoKur(Veritabani db, _SayacId id) async {
  const sabitSaat = 1788500000000;
  final ayarlarDeposu = AyarlarDeposu(db, idUret: id.cagir);
  final ayarlar = await ayarlarDeposu.yukleVeyaOlustur();
  return DriftGorevDeposu(
    db,
    saat: () => DateTime.fromMillisecondsSinceEpoch(sabitSaat, isUtc: true),
    idUret: id.cagir,
    hlc: HlcUretici(simdiMs: () => sabitSaat, clientId: ayarlar.clientId),
    ayarlarDeposu: ayarlarDeposu,
    actorId: ayarlar.devUserId,
  );
}

Future<List<SenkronKuyruguRow>> _kuyrukSatirlari(Veritabani db) => (db.select(
  db.senkronKuyrugu,
)..orderBy([(t) => OrderingTerm(expression: t.olusturuldu)])).get();

Map<String, Object?> _projeDegisikligi({
  required String entityId,
  required String alan,
  required String? deger,
  String opId = 'op-uzak',
}) {
  final hlc = Hlc(wallMs: 2000, counter: 0, clientId: 'uzak');
  final op = WireOp(
    operationId: opId,
    clientId: 'uzak',
    entityId: entityId,
    actorId: 'sahip-uzak',
    entityType: 'Project',
    opHlc: hlc,
    fields: {alan: WireFieldWrite(value: deger, hlc: hlc)},
  );
  return {
    'cursor': {'xid': 1, 'seq': 0},
    'payload': op.toJson(),
  };
}

// ============================================================
// Grup 2 + 3 + 5 yardimcilari -- widget duzeyi.
// ============================================================

class _SahteAramaAgi implements KullaniciAramaAgi {
  final KullaniciAramaSonucu sonuc;
  final List<String> aramalar = [];

  _SahteAramaAgi(this.sonuc);

  @override
  Future<KullaniciAramaSonucu> ara(String eposta) async {
    aramalar.add(eposta);
    return sonuc;
  }
}

class _SahteDepo implements GorevDeposu {
  final _listelerAkisi = StreamController<List<Proje>>.broadcast();

  @override
  Stream<List<GorevGorunum>> gorevlerGorunur() => Stream.value(const []);

  @override
  Stream<List<Proje>> listelerGorunur() => _listelerAkisi.stream;

  @override
  Future<void> ekle(
    String baslik, {
    int? oncelik,
    DateTime? sonTarih,
    Set<String> etiketler = const {},
    String? projeId,
  }) async {}

  @override
  Future<void> duzenle(String id, String yeniBaslik) async {}

  @override
  Future<void> ayrintilariGuncelle(
    String id, {
    Yazim<String>? baslik,
    Yazim<int?>? oncelik,
    Yazim<DateTime?>? sonTarih,
    Yazim<String?>? projeId,
    Set<String>? etiketEklenen,
    Set<String>? etiketSilinen,
  }) async {}

  @override
  Future<void> tamamlaGeriAl(String id, {required bool tamamlandi}) async {}

  @override
  Future<void> sil(String id) async {}

  @override
  Stream<List<CakismaKaydi>> cakismaKayitlariniIzle(String entityId) =>
      Stream.value(const []);

  @override
  Future<void> cakismaCoz(String entityId, CakismaSecimi secim) async {}

  @override
  Future<void> listeEkle(String ad) async {}

  @override
  Future<void> listeDuzenle(String id, String yeniAd) async {}

  @override
  Future<void> listeSil(String id) async {}

  void yayinlaListeler(List<Proje> p) => _listelerAkisi.add(p);

  void kapat() => _listelerAkisi.close();
}

final _projeP1 = Proje(
  id: 'p1',
  ad: 'İş',
  silindi: false,
  olusturuldu: DateTime.utc(2026, 9, 1),
);

/// Ekrani KURAR ve Drawer'i ACIP menuyu ACIK birakir -- her widget testin
/// ORTAK baslangici (Onur'un menu redesign'i, K-o88/4).
Future<_SahteDepo> _ekraniKurVeMenuyuAc(
  WidgetTester tester, {
  required KullaniciAramaAgi aramaAgi,
  required Future<String> Function(String, String) uyeEkle,
  required Future<({String durum, String? sonHataKodu})?> Function(String)
  kuyrukSatiriniOku,
  Future<void> Function()? turCalistir,
  String actorId = 'actor-ben',
}) async {
  final depo = _SahteDepo();
  addTearDown(depo.kapat);
  await tester.pumpWidget(
    MaterialApp(
      home: GorevListesiEkrani(
        depo: depo,
        kullaniciAramaAgi: aramaAgi,
        actorId: actorId,
        uyeEkle: uyeEkle,
        paylasimKuyrukSatiriniOku: kuyrukSatiriniOku,
        onYerelYazma: turCalistir,
      ),
    ),
  );
  depo.yayinlaListeler([_projeP1]);
  await tester.pump();

  tester.state<ScaffoldState>(find.byType(Scaffold)).openDrawer();
  await tester.pumpAndSettle();
  await tester.tap(find.byKey(const ValueKey('liste_menu_p1')));
  await tester.pumpAndSettle();
  return depo;
}

Future<void> _paylasDiyaloguAc(WidgetTester tester) async {
  await tester.tap(find.byKey(const ValueKey('liste_menu_paylas_p1')));
  await tester.pumpAndSettle();
}

void main() {
  group('Grup 1 -- DriftGorevDeposu.uyeEkle: uretilen WireOp SEKLI', () {
    late Veritabani db;
    late DriftGorevDeposu depo;

    setUp(() async {
      db = _bellekDb();
      depo = await _depoKur(db, _SayacId());
    });

    tearDown(() async => db.close());

    test(
      'D-A1/D-A2 PAZARLIKSIZ: TEK WireOp, entityType Project, sets.members.adds TEK eleman = userId, fields BOS',
      () async {
        final opId = await depo.uyeEkle('proje-1', 'kullanici-B');

        final kuyruk = await _kuyrukSatirlari(db);
        expect(kuyruk, hasLength(1), reason: 'TEK WireOp uretilmeli');
        final satir = kuyruk.single;
        expect(satir.opId, opId, reason: 'donen operationId kuyruktaki SATIRLA eslesmeli');
        expect(satir.entityType, 'Project');
        expect(satir.entityId, 'proje-1');
        expect(satir.govdeJson, isNot(contains('"fields"')), reason: 'D-A2: fields BOS');
        expect(satir.govdeJson, isNot(contains('"groups"')), reason: 'D-A2: groups BOS');
        expect(satir.govdeJson, contains('"members"'));
        expect(satir.govdeJson, contains('"el":"kullanici-B"'));
      },
    );

    test('D-A4: yerel projeksiyon YAZILMAZ -- Projeler tablosuna satir DUSMEZ', () async {
      await depo.uyeEkle('proje-1', 'kullanici-B');

      expect(
        await db.select(db.projeler).get(),
        isEmpty,
        reason: '`members` sutunu yok, D-A4 -- metot YALNIZ kuyruga yazar',
      );
    });
  });

  group('Grup 2 -- §C adim 1-3: op URETILMEZ negatifleri', () {
    testWidgets(
      'KullaniciBulunamadi -- op URETILMEZ, red metni gorunur, diyalog ACIK KALIR',
      (tester) async {
        var uyeEkleCagrildi = false;
        await _ekraniKurVeMenuyuAc(
          tester,
          aramaAgi: _SahteAramaAgi(const KullaniciBulunamadi()),
          uyeEkle: (p, u) async {
            uyeEkleCagrildi = true;
            return 'op-x';
          },
          kuyrukSatiriniOku: (opId) async => null,
        );
        await _paylasDiyaloguAc(tester);

        await tester.enterText(
          find.byKey(const ValueKey('liste_paylas_eposta_alani')),
          'yok@ornek.com',
        );
        await tester.tap(find.byKey(const ValueKey('liste_paylas_davet_dugmesi')));
        await tester.pumpAndSettle();

        expect(uyeEkleCagrildi, isFalse, reason: 'lookup basarisiz -- op HIC uretilmemeli');
        expect(find.text(Metinler.listePaylasKullaniciYok), findsOneWidget);
        expect(
          find.byKey(const ValueKey('liste_paylas_eposta_alani')),
          findsOneWidget,
          reason: 'diyalog ACIK KALMALI',
        );
      },
    );

    testWidgets(
      'KullaniciAramaHatasi -- op URETILMEZ, baglanti-yok metni gorunur, diyalog ACIK KALIR',
      (tester) async {
        var uyeEkleCagrildi = false;
        await _ekraniKurVeMenuyuAc(
          tester,
          aramaAgi: _SahteAramaAgi(const KullaniciAramaHatasi()),
          uyeEkle: (p, u) async {
            uyeEkleCagrildi = true;
            return 'op-x';
          },
          kuyrukSatiriniOku: (opId) async => null,
        );
        await _paylasDiyaloguAc(tester);

        await tester.enterText(
          find.byKey(const ValueKey('liste_paylas_eposta_alani')),
          'biri@ornek.com',
        );
        await tester.tap(find.byKey(const ValueKey('liste_paylas_davet_dugmesi')));
        await tester.pumpAndSettle();

        expect(uyeEkleCagrildi, isFalse, reason: 'ag hatasi -- op HIC uretilmemeli');
        expect(find.text(Metinler.listePaylasBaglantiYok), findsOneWidget);
        expect(
          find.byKey(const ValueKey('liste_paylas_eposta_alani')),
          findsOneWidget,
          reason: 'diyalog ACIK KALMALI',
        );
      },
    );

    testWidgets(
      'userId == actorId (kendi kendini davet) -- op URETILMEZ, red metni gorunur',
      (tester) async {
        var uyeEkleCagrildi = false;
        await _ekraniKurVeMenuyuAc(
          tester,
          aramaAgi: _SahteAramaAgi(const KullaniciBulundu('actor-ben')),
          actorId: 'actor-ben',
          uyeEkle: (p, u) async {
            uyeEkleCagrildi = true;
            return 'op-x';
          },
          kuyrukSatiriniOku: (opId) async => null,
        );
        await _paylasDiyaloguAc(tester);

        await tester.enterText(
          find.byKey(const ValueKey('liste_paylas_eposta_alani')),
          'ben@ornek.com',
        );
        await tester.tap(find.byKey(const ValueKey('liste_paylas_davet_dugmesi')));
        await tester.pumpAndSettle();

        expect(uyeEkleCagrildi, isFalse, reason: 'kendi kendini davet -- op HIC uretilmemeli');
        expect(find.text(Metinler.listePaylasKendiniDavetEdemezsin), findsOneWidget);
      },
    );

    testWidgets(
      'yerel dogrulama -- @ iceren gecersiz eposta ile AG HIC CAGRILMAZ',
      (tester) async {
        final aramaAgi = _SahteAramaAgi(const KullaniciAramaHatasi());
        await _ekraniKurVeMenuyuAc(
          tester,
          aramaAgi: aramaAgi,
          uyeEkle: (p, u) async => 'op-x',
          kuyrukSatiriniOku: (opId) async => null,
        );
        await _paylasDiyaloguAc(tester);

        await tester.enterText(
          find.byKey(const ValueKey('liste_paylas_eposta_alani')),
          'gecersiz-eposta',
        );
        await tester.tap(find.byKey(const ValueKey('liste_paylas_davet_dugmesi')));
        await tester.pumpAndSettle();

        expect(aramaAgi.aramalar, isEmpty, reason: 'yerel dogrulama dusmeli -- AG cagrilmamali');
        expect(find.text(Metinler.listePaylasGecersizEposta), findsOneWidget);
      },
    );
  });

  group('Grup 3 -- §C adim 4-6: uc dalli karar', () {
    testWidgets(
      'satir YOK -- diyalog KAPANIR, basari metni SnackBar\'da gorunur',
      (tester) async {
        var turCalistiSirasi = <String>[];
        await _ekraniKurVeMenuyuAc(
          tester,
          aramaAgi: _SahteAramaAgi(const KullaniciBulundu('kullanici-B')),
          uyeEkle: (p, u) async {
            turCalistiSirasi.add('uyeEkle');
            return 'op-basarili';
          },
          turCalistir: () async {
            turCalistiSirasi.add('turCalistir');
          },
          kuyrukSatiriniOku: (opId) async {
            expect(opId, 'op-basarili');
            turCalistiSirasi.add('kuyrukOku');
            return null;
          },
        );
        await _paylasDiyaloguAc(tester);

        await tester.enterText(
          find.byKey(const ValueKey('liste_paylas_eposta_alani')),
          'b@ornek.com',
        );
        await tester.tap(find.byKey(const ValueKey('liste_paylas_davet_dugmesi')));
        await tester.pumpAndSettle();

        expect(
          turCalistiSirasi,
          ['uyeEkle', 'turCalistir', 'kuyrukOku'],
          reason: '§C sirasi PAZARLIKSIZ: op -> tur -> kuyruk okuma',
        );
        expect(
          find.byKey(const ValueKey('liste_paylas_eposta_alani')),
          findsNothing,
          reason: 'diyalog KAPANMALI',
        );
        expect(find.text(Metinler.listePaylasBasarili('b@ornek.com')), findsOneWidget);
      },
    );

    testWidgets(
      'satir VAR, durum==zehirli -- red metni + HAM sonHataKodu gorunur, basari metni ASLA gorunmez',
      (tester) async {
        await _ekraniKurVeMenuyuAc(
          tester,
          aramaAgi: _SahteAramaAgi(const KullaniciBulundu('kullanici-B')),
          uyeEkle: (p, u) async => 'op-red',
          kuyrukSatiriniOku: (opId) async =>
              (durum: 'zehirli', sonHataKodu: 'RejectedForbidden'),
        );
        await _paylasDiyaloguAc(tester);

        await tester.enterText(
          find.byKey(const ValueKey('liste_paylas_eposta_alani')),
          'b@ornek.com',
        );
        await tester.tap(find.byKey(const ValueKey('liste_paylas_davet_dugmesi')));
        await tester.pumpAndSettle();

        expect(
          find.text(Metinler.listePaylasYetkiYok('RejectedForbidden')),
          findsOneWidget,
          reason: 'D-C1 PAZARLIKSIZ: ham sonHataKodu METINDE GORUNUR',
        );
        expect(
          find.text(Metinler.listePaylasBasarili('b@ornek.com')),
          findsNothing,
          reason: 'NEGATIF KONTROL: zehirli durumunda basari metni EKRANDA OLMAMALI',
        );
        expect(
          find.byKey(const ValueKey('liste_paylas_eposta_alani')),
          findsOneWidget,
          reason: 'diyalog ACIK KALMALI',
        );
      },
    );

    testWidgets(
      'satir VAR, durum==bekliyor -- yeniden-dene metni gorunur, diyalog ACIK KALIR',
      (tester) async {
        await _ekraniKurVeMenuyuAc(
          tester,
          aramaAgi: _SahteAramaAgi(const KullaniciBulundu('kullanici-B')),
          uyeEkle: (p, u) async => 'op-bekliyor',
          kuyrukSatiriniOku: (opId) async =>
              (durum: 'bekliyor', sonHataKodu: null),
        );
        await _paylasDiyaloguAc(tester);

        await tester.enterText(
          find.byKey(const ValueKey('liste_paylas_eposta_alani')),
          'b@ornek.com',
        );
        await tester.tap(find.byKey(const ValueKey('liste_paylas_davet_dugmesi')));
        await tester.pumpAndSettle();

        expect(find.text(Metinler.listePaylasYenidenDene), findsOneWidget);
        expect(
          find.byKey(const ValueKey('liste_paylas_eposta_alani')),
          findsOneWidget,
          reason: 'diyalog ACIK KALMALI -- op kuyrukta KALDI',
        );
      },
    );
  });

  group('Grup 4 -- uye tarafi: entityType:Project materyalizasyonu', () {
    late Veritabani db;
    late UzakDegisiklikUygulayici uygulayici;
    late DriftGorevDeposu depo;

    setUp(() async {
      db = _bellekDb();
      uygulayici = UzakDegisiklikUygulayici(db, clientId: 'client-uye');
      depo = await _depoKur(db, _SayacId());
    });

    tearDown(() async => db.close());

    test(
      'entityType:Project change gelince Projeler satiri dogar VE listelerGorunur() onu YAYINLAR',
      () async {
        final yayinlar = <List<Proje>>[];
        final abonelik = depo.listelerGorunur().listen(yayinlar.add);
        await pumpEventQueue();

        await uygulayici.changesUygula([
          _projeDegisikligi(entityId: 'p-davet', alan: 'name', deger: 'Ortak Proje'),
        ]);
        await pumpEventQueue();

        expect(
          yayinlar.any((liste) => liste.any((p) => p.id == 'p-davet' && p.ad == 'Ortak Proje')),
          isTrue,
          reason: 'davetle gelen Project listelerGorunur() akisinda GORUNMELI',
        );
        await abonelik.cancel();
      },
    );

    test(
      'NEGATIF: silindi=true gelen proje listelerGorunur() tarafindan YAYINLANMAZ',
      () async {
        await uygulayici.changesUygula([
          _projeDegisikligi(entityId: 'p-silinmis', alan: 'name', deger: 'Silinecek'),
        ]);
        await uygulayici.changesUygula([
          _projeDegisikligi(entityId: 'p-silinmis', alan: 'isDeleted', deger: 'true'),
        ]);

        final sonListe = await depo.listelerGorunur().first;
        expect(
          sonListe.any((p) => p.id == 'p-silinmis'),
          isFalse,
          reason: 'silindi=true olan proje YAYINLANMAMALI (listelerGorunur() silindi.equals(false) suzer)',
        );
      },
    );
  });

  group('Grup 5 -- K-o88/4 (Onur kilidi, 4 Eyl): Drawer menusu', () {
    testWidgets('menu acilir; UC madde de gorunur', (tester) async {
      final depo = _SahteDepo();
      addTearDown(depo.kapat);
      await tester.pumpWidget(MaterialApp(home: GorevListesiEkrani(depo: depo)));
      depo.yayinlaListeler([_projeP1]);
      await tester.pump();

      tester.state<ScaffoldState>(find.byType(Scaffold)).openDrawer();
      await tester.pumpAndSettle();
      expect(find.byKey(const ValueKey('liste_menu_p1')), findsOneWidget);

      await tester.tap(find.byKey(const ValueKey('liste_menu_p1')));
      await tester.pumpAndSettle();

      expect(find.byKey(const ValueKey('liste_menu_duzenle_p1')), findsOneWidget);
      expect(find.byKey(const ValueKey('liste_menu_paylas_p1')), findsOneWidget);
      expect(find.byKey(const ValueKey('liste_menu_sil_p1')), findsOneWidget);
    });

    testWidgets(
      'menu -> Paylas -- diyalog GORUNUR ve Drawer KAPALI (TUZAK olculdu: tek pop yeterli)',
      (tester) async {
        await _ekraniKurVeMenuyuAc(
          tester,
          aramaAgi: _SahteAramaAgi(const KullaniciBulunamadi()),
          uyeEkle: (p, u) async => 'op-x',
          kuyrukSatiriniOku: (opId) async => null,
        );
        await _paylasDiyaloguAc(tester);

        expect(
          find.byKey(const ValueKey('liste_paylas_eposta_alani')),
          findsOneWidget,
          reason: 'diyalog GORUNUR olmali',
        );
        expect(
          find.byType(Drawer),
          findsNothing,
          reason: 'Drawer KAPALI olmali -- cift pop riskine karsi TEK pop yeterli oldugu dogrulanir',
        );
      },
    );
  });
}

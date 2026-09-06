@TestOn('vm')
library;

// GOREV-slice-3d G1 -- YALNIZ-CEKME ISTEK YOLU KAPISI (Dart birim testi,
// sahte ag). Sahte ag sunucunun sozlesmesini TAKLIT eder: govdede "ops"
// anahtari yoksa 400 doner (SyncRequestValidator Ops NotNull, spec 1.1).

import 'dart:convert';
import 'dart:io';

import 'package:client/ag/senkron_agi.dart';
import 'package:client/veri/ayarlar_deposu.dart';
import 'package:client/veri/gorev_deposu.dart';
import 'package:client/veri/hlc.dart';
import 'package:client/veri/senkron_dongusu.dart';
import 'package:client/veri/veritabani.dart' hide Ayarlar;
import 'package:drift/native.dart';
import 'package:drift_dev/api/migrations_native.dart';
import 'package:flutter_test/flutter_test.dart';

import 'destekler/sahte_senkron_agi.dart';
import 'generated_migrations/schema.dart';
import 'generated_migrations/schema_v3.dart' as v3;

class _Kurulum {
  final Veritabani db;
  final AyarlarDeposu ayarlarDeposu;
  final Ayarlar ayarlar;
  final HlcUretici hlc;
  final DriftGorevDeposu depo;
  _Kurulum(this.db, this.ayarlarDeposu, this.ayarlar, this.hlc, this.depo);
}

Map<String, Object?> _degisiklikGirdisi(String entityId, int xid) => {
  'cursor': {'xid': xid, 'seq': 0},
  'payload': {
    'operationId': 'op-$xid-$entityId',
    'clientId': 'c1',
    'entityId': entityId,
    'actorId': 'a1',
    'entityType': 'Task',
    'opHlc': {'wallMs': xid, 'counter': 0, 'clientId': 'c1'},
    'fields': {'title': {'value': 'v$xid', 'hlc': {'wallMs': xid, 'counter': 0, 'clientId': 'c1'}}},
  },
};

/// IS-EMRI-o86-E G7: `WireSnapshotEntity` bicimi (Task, tek `title` scalar'i)
/// -- `snapshotUygula`nin GERCEKTEN calistigini (yerel `gorevler` satiri
/// DOGDU) dogrulamak icin yeterli minimal govde.
Map<String, Object?> _snapshotGorevGirdisi(String entityId, String title) => {
  'entityType': 'Task',
  'entityId': entityId,
  'scalars': [
    {
      'field': 'title',
      'value': title,
      'hlc': {'wallMs': 1, 'counter': 0, 'clientId': 'c1'},
      'winOperationId': 'op-snap-$entityId',
    },
  ],
  'sets': [],
  'groups': [],
};

void main() {
  late Directory gecici;

  setUp(() {
    gecici = Directory.systemTemp.createTempSync('g1-cekme-yolu-kapisi');
  });

  tearDown(() {
    try {
      gecici.deleteSync(recursive: true);
    } catch (_) {}
  });

  Veritabani dosyaDbAc() => Veritabani(NativeDatabase(File('${gecici.path}/m.sqlite')));

  Future<_Kurulum> kurulumYap() async {
    final db = dosyaDbAc();
    final ayarlarDeposu = AyarlarDeposu(db, idUret: uretimIdUret);
    final ayarlar = await ayarlarDeposu.yukleVeyaOlustur();
    final hlc = HlcUretici(simdiMs: () => DateTime.now().toUtc().millisecondsSinceEpoch, clientId: ayarlar.clientId);
    final depo = DriftGorevDeposu(db, saat: () => DateTime.now().toUtc(), idUret: uretimIdUret, hlc: hlc, ayarlarDeposu: ayarlarDeposu, actorId: ayarlar.devUserId);
    return _Kurulum(db, ayarlarDeposu, ayarlar, hlc, depo);
  }

  SenkronDongusu donguOlustur(_Kurulum k, SahteSenkronAgi agi, {String? baslangicCursorJson}) => SenkronDongusu(
    db: k.db,
    agi: agi,
    ayarlarDeposu: k.ayarlarDeposu,
    hlc: k.hlc,
    clientId: k.ayarlar.clientId,
    devUserId: k.ayarlar.devUserId,
    baslangicCursorJson: baslangicCursorJson,
  );

  // ================= D0 =================

  test('D0: kuyruk bos, cekmeTuruCalistir() -- BIR istek gitti (bugun: sifir)', () async {
    final k = await kurulumYap();
    final agi = SahteSenkronAgi();
    final dongu = donguOlustur(k, agi);
    await dongu.cekmeTuruCalistir();
    expect(agi.alinanIstekler, hasLength(1));
    await k.db.close();
  });

  test('D0: govde -- "ops":[] tasir', () async {
    final k = await kurulumYap();
    final agi = SahteSenkronAgi();
    final dongu = donguOlustur(k, agi);
    await dongu.cekmeTuruCalistir();
    expect(agi.alinanIstekler.single['ops'], isEmpty);
    await k.db.close();
  });

  test('D0: tetikleyici sayimi -- acilis + elle yenileme (kuyruk bos) -- gozlenen istek sayisi IKI, fazlasi yok', () async {
    final k = await kurulumYap();
    final agi = SahteSenkronAgi();
    final dongu = donguOlustur(k, agi);
    await dongu.cekmeTuruCalistir(); // acilis
    await dongu.cekmeTuruCalistir(); // elle yenileme
    expect(agi.alinanIstekler, hasLength(2));
    await k.db.close();
  });

  test('D0: zaman ilerler, hicbir tetik yok -- EK ISTEK YOK (periyodik yoklama yasagi)', () async {
    final k = await kurulumYap();
    final agi = SahteSenkronAgi();
    final dongu = donguOlustur(k, agi);
    await dongu.cekmeTuruCalistir();
    expect(agi.alinanIstekler, hasLength(1));
    await Future<void>.delayed(const Duration(milliseconds: 200)); // [DESIGN-LITERAL: test zamanlama gecikmesi, tasarim tokeni degil]
    expect(agi.alinanIstekler, hasLength(1), reason: 'SenkronDongusunun kendi Timeri YOK -- zaman TEK BASINA istek uretmez');
    await k.db.close();
  });

  test(
    'D0: bekleyen op VARKEN turCalistir() baslar, Future beklenmeden cekmeTuruCalistir() cagirilir -- '
    'ilk tur devam ederken IKINCI ISTEK GITMEZ; ilk tur bitince K3 BIR KEZ kosar (toplam istek IKI)',
    () async {
      final k = await kurulumYap();
      await k.depo.ekle('bekleyen op');
      final agi = SahteSenkronAgi(
        davranis: (govde, cagriNo) async {
          await Future<void>.delayed(const Duration(milliseconds: 80)); // [DESIGN-LITERAL: test zamanlama gecikmesi, tasarim tokeni degil] // [DESIGN-LITERAL: test zamanlama gecikmesi, tasarim tokeni degil]
          final ops = (govde['ops'] as List).cast<Map<String, Object?>>();
          return SenkronBasarili(jsonEncode({
            'serverHlc': null, 'nextCursor': null, 'hasMore': false, 'resyncRequired': false,
            'applied': [for (final op in ops) {'operationId': op['operationId'], 'code': 'Applied', 'effectiveOpHlc': op['opHlc']}],
            'changes': [], 'snapshot': [],
          }));
        },
      );
      final dongu = donguOlustur(k, agi);

      final pushFuture = dongu.turCalistir();
      final cekmeFuture = dongu.cekmeTuruCalistir(); // Future beklenmeden cagirilir.

      await Future<void>.delayed(const Duration(milliseconds: 15)); // [DESIGN-LITERAL: test zamanlama gecikmesi, tasarim tokeni degil]
      expect(agi.alinanIstekler, hasLength(1), reason: 'ilk tur devam ederken ikinci istek gitmemeli');

      await pushFuture;
      await cekmeFuture;
      expect(agi.alinanIstekler, hasLength(2), reason: 'K3: yutulan cekme BIR KEZ kosmali; ucuncu istek yok');
      await k.db.close();
    },
  );

  test('D0: tek tur devam ederken UC cekme tetikleyicisi yutulur -- tur bitince yalniz BIR ek istek (bayrak sayac DEGIL)', () async {
    final k = await kurulumYap();
    await k.depo.ekle('bekleyen op');
    final agi = SahteSenkronAgi(
      davranis: (govde, cagriNo) async {
        await Future<void>.delayed(const Duration(milliseconds: 80)); // [DESIGN-LITERAL: test zamanlama gecikmesi, tasarim tokeni degil]
        final ops = (govde['ops'] as List).cast<Map<String, Object?>>();
        return SenkronBasarili(jsonEncode({
          'serverHlc': null, 'nextCursor': null, 'hasMore': false, 'resyncRequired': false,
          'applied': [for (final op in ops) {'operationId': op['operationId'], 'code': 'Applied', 'effectiveOpHlc': op['opHlc']}],
          'changes': [], 'snapshot': [],
        }));
      },
    );
    final dongu = donguOlustur(k, agi);
    final pushFuture = dongu.turCalistir();
    unawaited(dongu.cekmeTuruCalistir());
    unawaited(dongu.cekmeTuruCalistir());
    unawaited(dongu.cekmeTuruCalistir());
    await pushFuture;
    expect(agi.alinanIstekler, hasLength(2), reason: 'uc yutulan tetikleyici TEK bir ek tur uretmeli');
    await k.db.close();
  });

  // ================= D7 =================

  test('D7: hasMore true -> true -> false -- UC istek; her istek oncekinin nextCursorunu tasir', () async {
    final k = await kurulumYap();
    var cagri = 0;
    final agi = SahteSenkronAgi(
      davranis: (govde, cagriNo) async {
        cagri++;
        final hasMore = cagri < 3;
        return SenkronBasarili(jsonEncode({
          'serverHlc': null, 'nextCursor': {'xid': cagri, 'seq': 0}, 'hasMore': hasMore, 'resyncRequired': false,
          'applied': [], 'changes': [_degisiklikGirdisi('e$cagri', cagri)], 'snapshot': [],
        }));
      },
    );
    final dongu = donguOlustur(k, agi);
    await dongu.cekmeTuruCalistir();
    expect(agi.alinanIstekler, hasLength(3));
    expect(agi.alinanIstekler[0]['sinceCursor'], isNull);
    expect(agi.alinanIstekler[1]['sinceCursor'], {'xid': 1, 'seq': 0});
    expect(agi.alinanIstekler[2]['sinceCursor'], {'xid': 2, 'seq': 0});
    await k.db.close();
  });

  test('D7: hasMore DAIMA true, sayfalar DOLU -- tur 20de durur, sonsuza gitmez', () async {
    final k = await kurulumYap();
    final agi = SahteSenkronAgi(
      davranis: (govde, cagriNo) async => SenkronBasarili(jsonEncode({
        'serverHlc': null, 'nextCursor': {'xid': cagriNo, 'seq': 0}, 'hasMore': true, 'resyncRequired': false,
        'applied': [], 'changes': [_degisiklikGirdisi('e$cagriNo', cagriNo)], 'snapshot': [],
      })),
    );
    final dongu = donguOlustur(k, agi);
    await dongu.cekmeTuruCalistir();
    expect(agi.alinanIstekler, hasLength(20));
    await k.db.close();
  });

  test('D7: hasMore true AMA changes BOS -- dongu ILK TURDA durur (tek istek; yirmi bos tur YOK)', () async {
    final k = await kurulumYap();
    final agi = SahteSenkronAgi(
      davranis: (govde, cagriNo) async => SenkronBasarili(jsonEncode({
        'serverHlc': null, 'nextCursor': {'xid': 1, 'seq': 0}, 'hasMore': true, 'resyncRequired': false,
        'applied': [], 'changes': [], 'snapshot': [],
      })),
    );
    final dongu = donguOlustur(k, agi);
    await dongu.cekmeTuruCalistir();
    expect(agi.alinanIstekler, hasLength(1));
    await k.db.close();
  });

  test('D7: resyncRequired true -- saklanan imlec silinir; sonraki istek sinceCursor:null; UzakAlanDurumu AYNEN durur', () async {
    final k = await kurulumYap();
    final agi1 = SahteSenkronAgi(
      davranis: (govde, cagriNo) async => SenkronBasarili(jsonEncode({
        'serverHlc': null, 'nextCursor': {'xid': 1, 'seq': 0}, 'hasMore': false, 'resyncRequired': false,
        'applied': [], 'changes': [_degisiklikGirdisi('e-resync', 1)], 'snapshot': [],
      })),
    );
    final dongu1 = donguOlustur(k, agi1);
    await dongu1.cekmeTuruCalistir();
    final metaOncesi = await k.db.select(k.db.uzakAlanDurumu).get();
    expect(metaOncesi, isNotEmpty);

    final ayarSatiri1 = await (k.db.select(k.db.ayarlar)..where((t) => t.id.equals(1))).getSingle();
    final agi2 = SahteSenkronAgi(
      davranis: (govde, cagriNo) async => SenkronBasarili(jsonEncode({
        'serverHlc': null, 'nextCursor': {'xid': 999, 'seq': 0}, 'hasMore': false, 'resyncRequired': true,
        'applied': [], 'changes': [], 'snapshot': [],
      })),
    );
    final dongu2 = donguOlustur(k, agi2, baslangicCursorJson: ayarSatiri1.nextCursorJson);
    await dongu2.cekmeTuruCalistir();
    final ayarSatiri2 = await (k.db.select(k.db.ayarlar)..where((t) => t.id.equals(1))).getSingle();
    expect(ayarSatiri2.nextCursorJson, isNull);

    final agi3 = SahteSenkronAgi();
    final dongu3 = donguOlustur(k, agi3, baslangicCursorJson: ayarSatiri2.nextCursorJson);
    await dongu3.cekmeTuruCalistir();
    expect(agi3.alinanIstekler.single['sinceCursor'], isNull);

    final metaSonrasi = await k.db.select(k.db.uzakAlanDurumu).get();
    expect(metaSonrasi.length, metaOncesi.length, reason: 'UzakAlanDurumu resyncRequired dalinda AYNEN durmali');
    await k.db.close();
  });

  test('D7: devUserIdDegistir sonrasi DB kapatilip acilir -- nextCursorJson null, UzakAlanDurumu bos, imlecSahibi=yeni devUserId', () async {
    final k = await kurulumYap();
    final agi = SahteSenkronAgi(
      davranis: (govde, cagriNo) async => SenkronBasarili(jsonEncode({
        'serverHlc': null, 'nextCursor': {'xid': 1, 'seq': 0}, 'hasMore': false, 'resyncRequired': false,
        'applied': [], 'changes': [_degisiklikGirdisi('e-devuser', 1)], 'snapshot': [],
      })),
    );
    final dongu = donguOlustur(k, agi);
    await dongu.cekmeTuruCalistir();
    expect((await (k.db.select(k.db.ayarlar)..where((t) => t.id.equals(1))).getSingle()).nextCursorJson, isNotNull);
    expect(await k.db.select(k.db.uzakAlanDurumu).get(), isNotEmpty);

    await k.ayarlarDeposu.devUserIdDegistir('yeni-dev-user-id');
    await k.db.close();

    final db2 = Veritabani(NativeDatabase(File('${gecici.path}/m.sqlite')));
    final ayarlarDeposu2 = AyarlarDeposu(db2, idUret: uretimIdUret);
    final ayarlar2 = await ayarlarDeposu2.yukleVeyaOlustur();
    expect(ayarlar2.nextCursorJson, isNull);
    expect(ayarlar2.imlecSahibi, 'yeni-dev-user-id');
    expect(await db2.select(db2.uzakAlanDurumu).get(), isEmpty);
    await db2.close();
  });

  test('D7: imlecSahibi == null (migrationdan gelen) satir -- imlec SILINIR (sahipsiz guvenilmez)', () async {
    final verifier = SchemaVerifier(GeneratedHelper());
    final schema = await verifier.schemaAt(3);
    final eskiDb = v3.DatabaseAtV3(schema.newConnection());
    await eskiDb.customStatement(
      "INSERT INTO ayarlar (id, client_id, son_wall, son_counter, dev_user_id, next_cursor_json) VALUES (1, ?, 0, 0, ?, ?)",
      ['c-eski', 'd-eski', '{"xid":1,"seq":0}'],
    );
    await eskiDb.close();

    final yeniDb = Veritabani(schema.newConnection());
    final ayarlarDeposu = AyarlarDeposu(yeniDb, idUret: uretimIdUret);
    final ayarlar = await ayarlarDeposu.yukleVeyaOlustur();
    expect(ayarlar.nextCursorJson, isNull, reason: 'sahipsiz (imlecSahibi==null) imlec guvenilmez, silinir');
    await yeniDb.close();
  });

  // ================= IS-EMRI-o86-E: G7/G8/G9 =================

  test(
    'G7 (PAZARLIKSIZ): resyncRequired true (bos govde) -- AYNI dongu cagrisinda IKINCI istek '
    'sinceCursor TASIMAZ, ve donen snapshot UYGULANIR (varlik yerel depoda gorunur)',
    () async {
      final k = await kurulumYap();
      const entityId = 'e-g7-snap';
      final agi = SahteSenkronAgi(
        davranis: (govde, cagriNo) async {
          if (cagriNo == 1) {
            return SenkronBasarili(jsonEncode({
              'serverHlc': null, 'nextCursor': {'xid': 999, 'seq': 0}, 'hasMore': false,
              'resyncRequired': true, 'applied': [], 'changes': [], 'snapshot': [],
            }));
          }
          return SenkronBasarili(jsonEncode({
            'serverHlc': null, 'nextCursor': {'xid': 1000, 'seq': 0}, 'hasMore': false,
            'resyncRequired': false, 'applied': [], 'changes': [],
            'snapshot': [_snapshotGorevGirdisi(entityId, 'Proje X gorevi')],
          }));
        },
      );
      final dongu = donguOlustur(k, agi, baslangicCursorJson: '{"xid":1,"seq":0}');

      await dongu.cekmeTuruCalistir();

      expect(agi.alinanIstekler, hasLength(2), reason: 'resync bayragi AYNI dongu cagrisinda ikinci bir istegi tetiklemeli');
      expect(agi.alinanIstekler[0]['sinceCursor'], {'xid': 1, 'seq': 0}, reason: 'ilk istek ESKI imleci tasir');
      expect(agi.alinanIstekler[1]['sinceCursor'], isNull, reason: 'imlec resyncRequired dalinda silindi -- ikinci istek sinceCursor TASIMAMALI');

      final gorev = await (k.db.select(k.db.gorevler)..where((t) => t.id.equals(entityId))).getSingleOrNull();
      expect(gorev, isNotNull, reason: 'ikinci istegin donen snapshotu GERCEKTEN uygulanmali (varlik yerel depoda gorunur)');
      expect(gorev!.baslik, 'Proje X gorevi');
      await k.db.close();
    },
  );

  test(
    'G8 (NEGATIF): resyncRequired false + bos sayfa -- dongu ESKISI GIBI durur (D7/2 korunur, '
    'bu satir olmadan G7 sahte gecerdi)',
    () async {
      final k = await kurulumYap();
      final agi = SahteSenkronAgi(
        davranis: (govde, cagriNo) async => SenkronBasarili(jsonEncode({
          'serverHlc': null, 'nextCursor': {'xid': 1, 'seq': 0}, 'hasMore': true,
          'resyncRequired': false, 'applied': [], 'changes': [], 'snapshot': [],
        })),
      );
      final dongu = donguOlustur(k, agi);
      await dongu.cekmeTuruCalistir();
      expect(agi.alinanIstekler, hasLength(1), reason: 'resyncRequired:false + bos sayfa -- ikinci istek GITMEMELI');
      await k.db.close();
    },
  );

  test(
    'G9 (SONSUZ DONGU KORUMASI): sahte ag HER yanitta resyncRequired true donerse dongu DURUR '
    '(zincir EN FAZLA BIR KEZ, kosum sayisi mekanik olarak sinanir)',
    () async {
      final k = await kurulumYap();
      final agi = SahteSenkronAgi(
        davranis: (govde, cagriNo) async => SenkronBasarili(jsonEncode({
          'serverHlc': null, 'nextCursor': {'xid': 999 + cagriNo, 'seq': 0}, 'hasMore': false,
          'resyncRequired': true, 'applied': [], 'changes': [], 'snapshot': [],
        })),
      );
      final dongu = donguOlustur(k, agi, baslangicCursorJson: '{"xid":1,"seq":0}');
      await dongu.cekmeTuruCalistir();
      expect(agi.alinanIstekler, hasLength(2), reason: 'resync HER yanitta true donse bile zincir EN FAZLA bir kez uzamali (toplam: ilk istek + TEK zincirlenen istek)');
      await k.db.close();
    },
  );

  // ================= D8 =================

  test('D8: dort yazma yolunun urettigi operationIdler -- tiresiz 13. hane (indeks 12) "7"', () async {
    final k = await kurulumYap();
    await k.depo.ekle('v7 test 1');
    final id = (await k.db.select(k.db.gorevler).get()).single.id;
    await k.depo.duzenle(id, 'v7 test 2');
    await k.depo.tamamlaGeriAl(id, tamamlandi: true);
    await k.depo.sil(id);

    final kuyruk = await k.db.select(k.db.senkronKuyrugu).get();
    expect(kuyruk, hasLength(4));
    for (final satir in kuyruk) {
      final tiresiz = satir.opId.replaceAll('-', '');
      expect(tiresiz[12], '7', reason: 'opId=${satir.opId} v7 olmali');
    }
    await k.db.close();
  });
}

void unawaited(Future<void> f) {}

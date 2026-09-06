// ignore_for_file: prefer_initializing_formals -- alanlar bilerek PRIVATE
// (`_db` vb.), yapici parametreleri ise PUBLIC adlandirilmis (`db` vb.);
// `this._db` seklinde yazmak parametre adini da private yapip disaridan
// adlandirilmis argumanla cagirmayi imkansizlastirirdi.

import 'dart:convert';
import 'dart:math';

import 'package:drift/drift.dart';

import '../ag/senkron_agi.dart';
import '../senkron/kuyruk_tabani.dart';
import '../senkron/uzak_degisiklik_uygulayici.dart';
import 'ayarlar_deposu.dart';
import 'hlc.dart';
import 'itme_yeniden_deneme.dart';
import 'veritabani.dart';

/// GOREV-slice-3c T6 + slice-3d T6/T7: senkron dongusu -- D4 (tek uçuş,
/// toplu gönderim tavanı 100) + D8/2 (`gonderildi` kurtarma) + D5 (sonuç
/// işleme) + D9 (yanıt sınıflandırma) + slice-3d `D0`/`D6`/`D7` (çekme
/// turu, echo, boşaltma döngüsü, atomik sayfa) BURADA birlesir.
class SenkronDongusu {
  static const int _denemeTavani = 8;
  static const int _bosaltmaTavani = 20; // D7/2 -- sonsuz boş-tur riskine karşı.

  final Veritabani _db;
  final SenkronAgi _agi;
  final AyarlarDeposu _ayarlarDeposu;
  final HlcUretici _hlc;
  final String _clientId;
  final String _devUserId;
  late final UzakDegisiklikUygulayici _uygulayici;
  late final ItmeYenidenDeneme _itmeYenidenDeneme;

  String? _mevcutCursorJson;

  // GOREV-SS2 D-SS2-11: turun BASINDA (itme yaniti islenmeden ONCE) alinan
  // anlik goruntu -- Ö8'in kapadigi pencereyi acar: Applied/Duplicate kuyruk
  // satirlari bu goruntuden SONRA silinir, canli sorgu (kuyrukEnBuyuk) bu
  // turda basariyla uygulanan bir yazimi KACIRIR. Tur bitince anlamsizlasir,
  // yeniden atanir -- eskisi TASINMAZ.
  List<SenkronKuyruguRow>? _turBasiAnlikGoruntu;

  Future<void>? _devamEdenTur;
  // K3 (Onur, kilitli): tek-uçuş kilidi devam eden turu döndürdüğünde
  // yutulan çekme tetikleyicisi BU bayrakla hatırlanır -- sayaç DEĞİLDİR,
  // devam eden tur bitince BİR KEZ yeniden koşar ve temizlenir.
  bool _cekmeBekliyor = false;
  // GOREV-A11 D-A11-6: `_itmeYenidenDeneme`'nin KENDİ tetiklediği turCalistir()
  // çağrısını DIŞARIDAN gelen (taze niyet taşıyan) çağrılardan ayırt eder --
  // yalnız bu bayrak `false` iken çizelge sıfırlanır (aksi hâlde retry KENDİ
  // ilerlemesini her seferinde 2 s'ye sıfırlardı).
  bool _yenidenDenemeIcinden = false;

  SenkronDongusu({
    required Veritabani db,
    required SenkronAgi agi,
    required AyarlarDeposu ayarlarDeposu,
    required HlcUretici hlc,
    required String clientId,
    required String devUserId,
    String? baslangicCursorJson,
    // Test-görünür (G5 D7/D0 ayakları çağrı sırasını/atomikliği gözlemlemek
    // için bir gözlemci sarmalayıcı enjekte eder) -- üretimde her zaman null,
    // SenkronDongusu KENDİ örneğini kurar.
    UzakDegisiklikUygulayici? uygulayici,
    // Test-görünür (GOREV-A11 G22, D-A11-2/1 -- seed sabit ⇒ kesin eşitlik
    // ölçülsün, pencere değil) -- üretimde her zaman null, ItmeYenidenDeneme
    // KENDİ Random()'ini kurar.
    Random? itmeYenidenDenemeRastgele,
  }) : _db = db,
       _agi = agi,
       _ayarlarDeposu = ayarlarDeposu,
       _hlc = hlc,
       _clientId = clientId,
       _devUserId = devUserId,
       _mevcutCursorJson = baslangicCursorJson {
    _uygulayici = uygulayici ??
        UzakDegisiklikUygulayici(
          db,
          clientId: clientId,
          kuyrukTabaniSaglayici: (entityId, alan) => kuyrukEnBuyuk(db, entityId, alan),
          // D-SS2-11: canli sorgu DEGIL -- turun basinda alinan anlik
          // goruntuden cevaplar (asagida _anlikGoruntudenBekliyorMu).
          bekleyenYerelYazimVarMi: _anlikGoruntudenBekliyorMu,
        );
    _itmeYenidenDeneme = ItmeYenidenDeneme(
      turCalistir: () {
        _yenidenDenemeIcinden = true;
        return turCalistir();
      },
      rastgele: itmeYenidenDenemeRastgele,
    );
  }

  /// D4 PAZARLIKSIZ: aynı anda en fazla BİR tur (itme YA DA çekme). Zaten
  /// devam eden bir tur varsa (hangi türden olursa olsun) yeni bir tur
  /// BAŞLATMAZ, aynı Future'ı döndürür.
  ///
  /// GOREV-A11 D-A11-2/3: bu çağrı `_itmeYenidenDeneme`'nin KENDİ zamanlayıcı
  /// tetiklemesi DEĞİLSE (yani DIŞARIDAN -- açılış/elleYenile/onYerelYazma --
  /// geldiyse) taze niyet sayılır ve bekleyen retry çizelgesi SIFIRLANIR.
  Future<void> turCalistir() {
    final yenidenDenemeIcindenGeldi = _yenidenDenemeIcinden;
    _yenidenDenemeIcinden = false;
    final devamEden = _devamEdenTur;
    if (devamEden != null) return devamEden;
    if (!yenidenDenemeIcindenGeldi) {
      _itmeYenidenDeneme.sifirla();
    }
    return _kilitliBaslat(() => _yuvarlakDongusu(kuyrugaBak: true));
  }

  /// GOREV-A11 D-A11-2/4 (DURMA) + G22/i. [SINIR, GOREV §9/3] üretimde bunu
  /// çağıran bir yaşam döngüsü kancası YOK -- bugün yalnız testte anlamlıdır.
  void durdur() {
    _itmeYenidenDeneme.durdur();
  }

  /// slice-3d D0: kuyrukta bekleyen satır olup olmadığına BAKMAZ -- gövdeyi
  /// DAİMA `"ops":[]` ile kurar. İtme turuyla AYNI tek-uçuş kilidini
  /// paylaşır; kilit doluysa K3 bayrağını kurar ve kendi istek ATMAZ.
  Future<void> cekmeTuruCalistir() {
    if (_devamEdenTur != null) {
      _cekmeBekliyor = true;
      return _devamEdenTur!;
    }
    return _kilitliBaslat(() => _yuvarlakDongusu(kuyrugaBak: false));
  }

  Future<void> _kilitliBaslat(Future<void> Function() ic) {
    final tur = ic();
    _devamEdenTur = tur;
    return tur.whenComplete(() async {
      _devamEdenTur = null;
      if (_cekmeBekliyor) {
        _cekmeBekliyor = false;
        await cekmeTuruCalistir(); // K3: yutulan tetikleyici BİR KEZ yeniden koşar.
      }
    });
  }

  /// IS-EMRI-o86-B §C adim 6: bir op'un kuyruk SATIRINI dogrudan `opId` ile
  /// okur -- caGiran `turCalistir()`i BEKLEDIKTEN SONRA cagirir; satir
  /// YOK/`zehirli`/`bekliyor` uc dalli diyalog kararini besler (Z7/Z8'in
  /// kapsadigi `Project` op'u icin BASKA gorunurluk yolu yoktur).
  Future<SenkronKuyruguRow?> kuyrukSatiriniOku(String opId) {
    return (_db.select(
      _db.senkronKuyrugu,
    )..where((t) => t.opId.equals(opId))).getSingleOrNull();
  }

  /// D8/2: uçuş işareti olan (`gonderildi`) TÜM satırları `bekliyor`e
  /// döndürür. Uygulama açılışında AYRICA (tur beklemeden) çağrılmalıdır --
  /// `turCalistir()` zaten her turun başında bunu yapar.
  Future<void> gonderildiKurtar() async {
    await (_db.update(
      _db.senkronKuyrugu,
    )..where((t) => t.durum.equals('gonderildi'))).write(
      const SenkronKuyruguCompanion(durum: Value('bekliyor')),
    );
  }

  /// D-SS2-11: "o turda gönderilen oplar ∪ hâlâ bekliyor olanlar" -- Ö7 ile
  /// AYNI sınır (`zehirli` HARİÇ, `bekliyor`+`gonderildi` dâhil). Bu sorgu
  /// `_bekleyenleriSec()`'in sayfalama/işaretleme adımından ÖNCE koşar; o
  /// anda hem bu round'da seçilecek satırlar hem de sayfa dışında kalanlar
  /// hâlâ `bekliyor`dur.
  Future<List<SenkronKuyruguRow>> _turBasiSatirlariGetir() {
    return (_db.select(
      _db.senkronKuyrugu,
    )..where((t) => t.durum.isIn(['bekliyor', 'gonderildi']))).get();
  }

  /// `UzakDegisiklikUygulayici`'ya `bekleyenYerelYazimVarMi` olarak enjekte
  /// edilir -- CANLI sorgu DEĞİL, `_turBasiAnlikGoruntu`'ya bakar (D-SS2-11).
  Future<bool> _anlikGoruntudenBekliyorMu(String entityId, String alan) async {
    final satirlar = _turBasiAnlikGoruntu;
    if (satirlar == null) return false;
    for (final satir in satirlar) {
      if (satir.entityId != entityId) continue;
      if (hamAlanHlcCikar(satir.govdeJson, alan) != null) return true;
    }
    return false;
  }

  /// Ortak yuvarlak döngüsü -- `kuyrugaBak=true` (itme): pending satırları
  /// seçer/gönderir. `kuyrugaBak=false` (çekme, D0): HER round `ops:[]`
  /// gönderir. D7/2 boşaltma: bir round `hasMore` + dolu sayfa dönerse
  /// döngü (aynı tek-uçuş kilidi içinde) devam eder; boş sayfa `hasMore`'a
  /// BAKILMAKSIZIN döngüyü durdurur; tavan `_bosaltmaTavani`.
  Future<void> _yuvarlakDongusu({required bool kuyrugaBak}) async {
    if (kuyrugaBak) {
      await gonderildiKurtar();
    }
    // D-SS2-11: TURUN BASINDA, itme yaniti islenmeden ONCE alinir -- bu
    // yuvarlak dongusu cagrisinin TAMAMI (ic bosaltma yuvarlaklari dahil)
    // AYNI goruntuyu kullanir.
    _turBasiAnlikGoruntu = await _turBasiSatirlariGetir();
    var bosaltmaSayaci = 0;
    // Cekme turu (kuyrugaBak=false) HER ZAMAN en az bir istek atar (D0).
    // Itme turu (kuyrugaBak=true) once kuyruga bakar -- bekleyen YOKSA VE
    // henuz bir boşaltma devami da GEREKMIYORSA hicbir istek atmadan doner
    // (mevcut/eski davranis: kuyruk bos ise sifir istek).
    var devamGerekli = !kuyrugaBak;
    // IS-EMRI-o86-E SONSUZ DONGU KORUMASI PAZARLIKSIZ: resync bir TUR icinde
    // EN FAZLA BIR KEZ zincirlenir -- sunucu ART ARDA resyncRequired:true
    // donse bile (beklenmeyen/bozuk durum), bu tur ikinci bir resync-tetikli
    // ek istek ATMAZ (D7/2'nin `_bosaltmaTavani`si BASKA bir senaryoyu --
    // hasMore zincirini -- sinirlar, resync zincirini AYRICA sinirlamaz).
    var resyncZincirlendiMi = false;

    while (true) {
      final secilenler = kuyrugaBak
          ? await _bekleyenleriSec()
          : const <SenkronKuyruguRow>[];

      // Gonderilecek satir YOK ve onceki round bir devam istemiyorsa -- BITTI.
      if (secilenler.isEmpty && !devamGerekli) return;

      if (secilenler.isNotEmpty) {
        await (_db.update(_db.senkronKuyrugu)..where(
              (t) => t.opId.isIn(secilenler.map((s) => s.opId)),
            ))
            .write(const SenkronKuyruguCompanion(durum: Value('gonderildi')));
      }

      final govde = _istekGovdesiOlustur(secilenler);
      final sonuc = await _agi.gonder(govde);

      switch (sonuc) {
        case SenkronBasarili(:final govdeJson):
          if (kuyrugaBak) {
            // GOREV-A11 D-A11-2/3: basarili itme -- retry cizelgesi SIFIRLANIR.
            _itmeYenidenDeneme.sifirla();
          }
          final sonucBilgisi = await _basariliYanitIsle(govdeJson, secilenler);
          devamGerekli = sonucBilgisi.devamGerekli;
          if (sonucBilgisi.resyncTetiklendi) {
            if (resyncZincirlendiMi) return; // SONSUZ DONGU KORUMASI: resync ZATEN bir kez zincirlendi.
            resyncZincirlendiMi = true;
          }
          if (devamGerekli) {
            bosaltmaSayaci++;
            if (bosaltmaSayaci >= _bosaltmaTavani) return; // [KIRMIZI] tavan -- sonsuz donguye girmez.
          }
        // devam -- sonraki turda kuyrugaBak ise _bekleyenleriSec() yeniden sorgulanir.
        case SenkronHttpHatasi(:final durumKodu):
          await _httpHatasiIsle(durumKodu, secilenler, itmeBaglamindaMi: kuyrugaBak);
          return; // D9: 4xx/401/5xx sonrasi devam etmenin anlami yok.
        case SenkronAgHatasi():
          // GOREV-A11 D-A11-4: TASIMA HATASI -- op sunucuya hic ulasmamistir,
          // degerlendirilmemistir ⇒ denemeSayisi ARTMAZ.
          await _bekliyorGeriDondurVeDenemeArtir(
            secilenler,
            basariRozeti: 'cevrimdisi',
            sayaciArtir: false,
          );
          if (kuyrugaBak && secilenler.isNotEmpty) {
            // D-A11-1: kuyrukta bekleyen satir varken basarisiz bir ITME
            // turu -- tavanli geri cekilmeyle yeniden denenir. Cekme HICBIR
            // kosulda buraya girmez (D0 daraltmasinin TEK istisnasi budur).
            _itmeYenidenDeneme.planla();
          }
          return;
      }
    }
  }

  Future<List<SenkronKuyruguRow>> _bekleyenleriSec() {
    return (_db.select(_db.senkronKuyrugu)
          ..where((t) => t.durum.equals('bekliyor'))
          ..orderBy([
            (t) => OrderingTerm(expression: t.hlcWallMs),
            (t) => OrderingTerm(expression: t.hlcCounter),
            (t) => OrderingTerm(expression: t.opId),
          ])
          ..limit(100))
        .get();
  }

  /// D6 PAZARLIKSIZ: `sinceCursor`/her opun `govdeJson`'u HAM METİN olarak
  /// gömülür -- decode/re-encode YOK (D1: govde yeniden üretilmez; D6:
  /// `Xid` `ulong`, sayıya çevirmek yasak). slice-3d D0: `secilenler` boşsa
  /// `"ops":[]` doğal olarak üretilir (çekme turu da bu YOLDAN gider).
  String _istekGovdesiOlustur(List<SenkronKuyruguRow> secilenler) {
    final opsJoined = secilenler.map((s) => s.govdeJson).join(',');
    final cursor = _mevcutCursorJson ?? 'null';
    return '{"clientId":${jsonEncode(_clientId)},"clientHlc":null,'
        '"sinceCursor":$cursor,"ops":[$opsJoined]}';
  }

  /// slice-3d K2/D7-1: itme turu DA `changes`/`snapshot`'ı AYNI
  /// `UzakDegisiklikUygulayici`'ya uygular; sayfa uygulaması VE imleç
  /// yazımı TEK `_db.transaction()` içindedir, imleç EN SON yazılır (bir
  /// sayfa ATOMİKTİR -- yarım sayfa senaryosunda tüm işlem geri sarılır).
  /// Döner: `devamGerekli` -- D7/2 boşaltma döngüsünün DEVAM etmesi mi
  /// gerekiyor (`hasMore == true` VE bu sayfa boş DEĞİLDİ) VEYA
  /// `resyncRequired` (IS-EMRI-o86-E §2) -- imleç zaten SİLİNDİ (aşağıda),
  /// bir sonraki istek `sinceCursor`suz (snapshot) gitmeli, bu YÜZDEN devam
  /// GEREKİR; `resyncTetiklendi` -- çağıranın (SONSUZ DÖNGÜ KORUMASI için)
  /// bu turda resync'in ZATEN bir kez zincirlenip zincirlenmediğini
  /// izleyebilmesi için ayrıca taşınır.
  Future<({bool devamGerekli, bool resyncTetiklendi})> _basariliYanitIsle(
    String govdeJson,
    List<SenkronKuyruguRow> gonderilenler,
  ) async {
    final govde = jsonDecode(govdeJson) as Map<String, Object?>;
    final appliedListesi = (govde['applied'] as List? ?? const [])
        .cast<Map<String, Object?>>();
    final resyncRequired = govde['resyncRequired'] as bool? ?? false;
    final hasMore = govde['hasMore'] as bool? ?? false;
    final changes = (govde['changes'] as List? ?? const [])
        .cast<Map<String, Object?>>();
    final snapshot = (govde['snapshot'] as List? ?? const [])
        .cast<Map<String, Object?>>();
    final opIdToRow = {for (final r in gonderilenler) r.opId: r};

    await _db.transaction(() async {
      final serverHlcJson = govde['serverHlc'] as Map<String, Object?>?;
      if (serverHlcJson != null) {
        _hlc.yanitIsle(serverHlc: _hlcFromJson(serverHlcJson));
      }

      for (final sonuc in appliedListesi) {
        final opId = sonuc['operationId'] as String;
        final satir = opIdToRow[opId];
        if (satir == null) continue; // beklenmedik -- guvenlik agi

        final code = sonuc['code'] as String;
        final effectiveOpHlcJson =
            sonuc['effectiveOpHlc'] as Map<String, Object?>?;
        if (effectiveOpHlcJson != null) {
          _hlc.yanitIsle(effectiveOpHlc: _hlcFromJson(effectiveOpHlcJson));
        }

        await _tekSonucIsle(satir, code);
      }

      await _ayarlarDeposu.hlcKalicilastir(_hlc.sonWall, _hlc.sonCounter);

      // slice-3d D2/D6: iki dal ayrıştırıcı, snapshot BİRLEŞTİRİCİ, echo
      // ATLANMAZ -- ikisi de VERİ olarak (imleçten ÖNCE) uygulanır.
      if (snapshot.isNotEmpty) {
        await _uygulayici.snapshotUygula(snapshot);
      }
      if (changes.isNotEmpty) {
        await _uygulayici.changesUygula(changes);
      }

      // D7/1 PAZARLIKSIZ: imleç EN SON yazılır -- veri uygulamasından SONRA,
      // AYNI transaction içinde.
      if (resyncRequired) {
        _mevcutCursorJson = null;
      } else {
        _mevcutCursorJson = _hamCursorCikar(govdeJson);
      }
      await _ayarlarDeposu.nextCursorKalicilastir(_mevcutCursorJson, devUserId: _devUserId);
    });

    // D7/2: boş sayfa hasMore'a BAKILMAKSIZIN döngüyü durdurur -- AMA
    // IS-EMRI-o86-E §2: resyncRequired bu kuraldan MUAF'tır. `resyncRequired`
    // (bayrak-only: `changes:[]`, `snapshot:null`) D7/2'nin "boş sayfa"
    // taniminA UYAR ve önceki davranışta döngüyü durdururdu -- ama imleç
    // yukarıda ZATEN silindi, bir sonraki istek sinceCursor'suz (snapshot)
    // gitmesi GEREKİR; aksi halde bu tur biter ve o istek bir SONRAKİ dış
    // tetiğe kadar hiç atılmaz (§F adım 6'nın canlı kusuru, KANIT/o86F).
    return (
      devamGerekli: resyncRequired || (hasMore && (changes.isNotEmpty || snapshot.isNotEmpty)),
      resyncTetiklendi: resyncRequired,
    );
  }

  /// D5: op bazında sonuç işleme + `cakisma` kilidi.
  Future<void> _tekSonucIsle(SenkronKuyruguRow satir, String code) async {
    switch (code) {
      case 'Applied':
      case 'Duplicate':
        await (_db.delete(
          _db.senkronKuyrugu,
        )..where((t) => t.opId.equals(satir.opId))).go();
        final zehirliSayisi =
            await (_db.selectOnly(_db.senkronKuyrugu)
                  ..addColumns([_db.senkronKuyrugu.opId.count()])
                  ..where(
                    _db.senkronKuyrugu.entityId.equals(satir.entityId) &
                        _db.senkronKuyrugu.durum.equals('zehirli'),
                  ))
                .map((r) => r.read(_db.senkronKuyrugu.opId.count())!)
                .getSingle();
        // D5 PAZARLIKSIZ ("cakisma KİLİTLENİR"): bu görev için başka bir
        // zehirli satır varsa Applied/Duplicate onu senkronize YAPAMAZ.
        if (zehirliSayisi == 0) {
          await _rozetYaz(satir.entityId, 'senkronize');
        }
      default:
        // RejectedRegistryViolation / RejectedAbsurdHlc / RejectedInvalid /
        // RejectedSetCapExceeded / tanınmayan kod -- HEPSİ aynı: zehirli,
        // satır SİLİNMEZ (sessiz veri kaybı yok), sonHataKodu ham kod.
        await (_db.update(_db.senkronKuyrugu)..where(
              (t) => t.opId.equals(satir.opId),
            ))
            .write(
              SenkronKuyruguCompanion(
                durum: const Value('zehirli'),
                sonHataKodu: Value(code),
              ),
            );
        await _rozetYaz(satir.entityId, 'cakisma');
    }
  }

  /// D9: 401 hariç HER 4xx -- tur DURUR, `denemeSayisi` ARTMAZ, satırlar
  /// `bekliyor` kalır. 401 ve 5xx ise ağ hatasıyla AYNI muameleyi görür.
  ///
  /// GOREV-A11 D-A11-4 [B3]: bu sınıflandırma DARALTILDI -- `5xx` (sunucu
  /// op'u reddetmedi, hizmet VEREMEDİ) artık `denemeSayisi`'nı ARTIRMAZ
  /// (öncesinde 401 ile aynı yoldan artıyordu; `D-A11-2` çizelgesiyle 9.
  /// başarısızlık ~5 dakikada gelip satırı KALICI zehirlerdi). `401` bu
  /// dilimin KAPSAMI DIŞINDADIR -- eski (artıran) davranışı KORUR. 4xx
  /// (401 hariç) sınıflandırması D9'un KİLİTLİ hâliyle DEĞİŞMEDEN kalır.
  /// D-A11-2/5: yalnız taşıma hatası ve 5xx yeniden DENENİR -- 401 ve diğer
  /// 4xx (408/429 dâhil) `ItmeYenidenDeneme.planla()`yı HİÇ görmez.
  Future<void> _httpHatasiIsle(
    int durumKodu,
    List<SenkronKuyruguRow> gonderilenler, {
    required bool itmeBaglamindaMi,
  }) async {
    if (durumKodu == 401) {
      await _bekliyorGeriDondurVeDenemeArtir(
        gonderilenler,
        basariRozeti: 'cevrimdisi',
        sonHataKodu: 'http-$durumKodu',
        sayaciArtir: true,
      );
    } else if (durumKodu >= 500) {
      await _bekliyorGeriDondurVeDenemeArtir(
        gonderilenler,
        basariRozeti: 'cevrimdisi',
        sonHataKodu: 'http-$durumKodu',
        sayaciArtir: false, // D-A11-4
      );
      if (itmeBaglamindaMi && gonderilenler.isNotEmpty) {
        _itmeYenidenDeneme.planla(); // D-A11-2/5
      }
    } else {
      await _db.transaction(() async {
        for (final satir in gonderilenler) {
          await (_db.update(_db.senkronKuyrugu)..where(
                (t) => t.opId.equals(satir.opId),
              ))
              .write(
                SenkronKuyruguCompanion(
                  durum: const Value('bekliyor'),
                  sonHataKodu: Value('http-$durumKodu'),
                ),
              );
          await _rozetYaz(satir.entityId, 'cakisma');
        }
      });
    }
  }

  /// D9: ağ hatası/zaman aşımı VE 401/5xx için ortak yol -- `bekliyor`,
  /// `denemeSayisi++`; `denemeSayisi > 8` ⇒ `zehirli` + `deneme-tavani`
  /// (D9 kirmizi uyari: denemeSayisi ÖLÜ SAYAÇ OLAMAZ).
  ///
  /// GOREV-A11 D-A11-4 [B3]: `sayaciArtir: false` -- taşıma hatası/5xx için --
  /// satır `bekliyor` kalır, `denemeSayisi` VE tavan kontrolü hiç DEVREYE
  /// GİRMEZ (sunucu op'u DEĞERLENDİRMEMİŞTİR ⇒ sayaç bu bilgiyi taşımaz).
  Future<void> _bekliyorGeriDondurVeDenemeArtir(
    List<SenkronKuyruguRow> satirlar, {
    required String basariRozeti,
    String? sonHataKodu,
    required bool sayaciArtir,
  }) async {
    if (satirlar.isEmpty) return; // cekme turu (ops:[]) icin denenecek satir yok.
    await _db.transaction(() async {
      for (final satir in satirlar) {
        if (!sayaciArtir) {
          await (_db.update(_db.senkronKuyrugu)..where(
                (t) => t.opId.equals(satir.opId),
              ))
              .write(
                SenkronKuyruguCompanion(
                  durum: const Value('bekliyor'),
                  sonHataKodu: Value(sonHataKodu),
                ),
              );
          await _rozetYaz(satir.entityId, basariRozeti);
          continue;
        }
        final yeniDeneme = satir.denemeSayisi + 1;
        if (yeniDeneme > _denemeTavani) {
          await (_db.update(_db.senkronKuyrugu)..where(
                (t) => t.opId.equals(satir.opId),
              ))
              .write(
                SenkronKuyruguCompanion(
                  durum: const Value('zehirli'),
                  denemeSayisi: Value(yeniDeneme),
                  sonHataKodu: const Value('deneme-tavani'),
                ),
              );
          await _rozetYaz(satir.entityId, 'cakisma');
        } else {
          await (_db.update(_db.senkronKuyrugu)..where(
                (t) => t.opId.equals(satir.opId),
              ))
              .write(
                SenkronKuyruguCompanion(
                  durum: const Value('bekliyor'),
                  denemeSayisi: Value(yeniDeneme),
                  sonHataKodu: Value(sonHataKodu),
                ),
              );
          await _rozetYaz(satir.entityId, basariRozeti);
        }
      }
    });
  }

  Future<void> _rozetYaz(String entityId, String senkronDurumu) async {
    await (_db.update(
      _db.gorevler,
    )..where((t) => t.id.equals(entityId))).write(
      GorevlerCompanion(senkronDurumu: Value(senkronDurumu)),
    );
  }

  Hlc _hlcFromJson(Map<String, Object?> json) => Hlc(
    wallMs: json['wallMs'] as int,
    counter: json['counter'] as int,
    clientId: json['clientId'] as String,
  );

  /// D6 PAZARLIKSIZ: `nextCursor`'ın HAM alt-metnini (decode etmeden)
  /// çıkarır -- `WireCursor` yalnız iki düz alan taşır (`Xid`,`Seq`),
  /// iç içe süslü parantez YOKTUR, bu yüzden `[^}]*` güvenlidir.
  String? _hamCursorCikar(String govdeJson) {
    final eslesme = RegExp(
      r'"nextCursor"\s*:\s*(null|\{[^}]*\})',
    ).firstMatch(govdeJson);
    if (eslesme == null) return null;
    final deger = eslesme.group(1)!;
    return deger == 'null' ? null : deger;
  }
}

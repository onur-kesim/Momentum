import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_driver/driver_extension.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'ag/gercek_zamanli_sinyal.dart';
import 'ag/http_auth_agi.dart';
import 'ag/http_kullanici_arama_agi.dart';
import 'ag/http_senkron_agi.dart';
import 'ag/kullanici_arama_agi.dart';
import 'ag/signalr_json_sinyal.dart';
import 'design/tema.dart';
import 'sunum/giris_ekrani.dart';
import 'sunum/gorev_listesi_ekrani.dart';
import 'veri/ayarlar_deposu.dart';
import 'veri/ayarlari_hazirla.dart';
import 'veri/depolama_durumu.dart';
import 'veri/gorev_deposu.dart';
import 'veri/hlc.dart';
import 'veri/kimlik_deposu.dart';
import 'veri/oturum_yoneticisi.dart';
import 'veri/senkron_dongusu.dart';
import 'veri/veritabani.dart';
import 'vitrin/durum_vitrini.dart';

/// Android emulator'dan host makinenin localhost'una ozel takma ad (real
/// cihaz/masaustu/web icin --dart-define=SENKRON_SUNUCU_URL ile ezilir).
/// K41: bu dilim gelistirme-ortami olcum iskelesidir (D0), gercek sunucu
/// kesfi/yapilandirmasi kapsam disidir.
const String _senkronSunucuUrl = String.fromEnvironment(
  'SENKRON_SUNUCU_URL',
  defaultValue: 'http://10.0.2.2:5298',
);

/// GOREV-A10 Y3 (o71-o81 arasi): derleme-zamani `DEV_USER_ID` ezmesi -- iki
/// cihazi ayni kullanici yapmanin ONCEKI yolu. IS-EMRI-o83 (DILIM 1 KIMLIK)
/// itibariyle giris ekrani KAPIYI TUTAR; bu sabit `dev_user_id_define_test.dart`
/// icin ve docker-compose.yml'nin `--dart-define` ARG'i icin TANIMLI KALIR
/// (is emri s5 DOKUNMA listesi: "docker-compose.yml'deki DEV_USER_ID sabiti
/// dilim 3'e kadar demo kimligi olarak kalir") ama asagidaki YENI giris
/// akisinda ARTIK OKUNMAZ -- gercek kimlik artik OturumYoneticisi'nden gelir.
const String devUserIdEzmesi = String.fromEnvironment('DEV_USER_ID');

void main() async {
  // F7: flutter_driver bayrak korumali -- agac sarsimi bunu surum
  // derlemesinde dusurmelidir (kriter 13'te olculur).
  if (const bool.fromEnvironment('ENABLE_FLUTTER_DRIVER')) {
    enableFlutterDriverExtension();
  }
  // F5: durum vitrini gercek DB/ayarlar/OTURUM bootstrap'ina hic ihtiyac
  // duymaz -- "olu tuzagi engeller" gerekcesi burada da korunur (bkz.
  // durum_vitrini.dart). Giris ekrani devre disi kalir.
  if (const bool.fromEnvironment('DURUM_VITRINI')) {
    runApp(const MomentumUygulamasi(depo: null));
    return;
  }
  final oturumYoneticisi = OturumYoneticisi(
    agi: HttpAuthAgi(sunucuTabanUrl: _senkronSunucuUrl),
    depo: GuvenliKimlikDeposu(),
  );
  runApp(_KimlikKapisi(oturumYoneticisi: oturumYoneticisi));
}

/// IS-EMRI-o83 s2.2/8/11: kok widget. Oturum yoksa [GirisEkrani], varsa
/// (kurulum hazirlanana kadar kisa bir yukleme, sonra) uygulamanin kendisi.
/// Sessiz yenileme dusup oturum sifirlaninca (`OturumYoneticisi.yenile`)
/// BURAYA otomatik doner -- itme kuyrugu Drift'te durur, bu widget onu hic
/// bilmez, bu yuzden "kuyruk korunur" (s2.2/11) EK KOD gerektirmez.
class _KimlikKapisi extends StatefulWidget {
  final OturumYoneticisi oturumYoneticisi;

  const _KimlikKapisi({required this.oturumYoneticisi});

  @override
  State<_KimlikKapisi> createState() => _KimlikKapisiState();
}

class _KimlikKapisiState extends State<_KimlikKapisi> {
  Future<_UretimKurulumu>? _kurulumGelecek;
  String? _kurulumKullaniciId;

  @override
  void initState() {
    super.initState();
    widget.oturumYoneticisi.oturum.addListener(_oturumDegisti);
    unawaited(widget.oturumYoneticisi.baslat());
  }

  @override
  void dispose() {
    widget.oturumYoneticisi.oturum.removeListener(_oturumDegisti);
    super.dispose();
  }

  void _oturumDegisti() {
    final oturum = widget.oturumYoneticisi.oturum.value;
    if (oturum == null) {
      setState(() {
        _kurulumGelecek = null;
        _kurulumKullaniciId = null;
      });
      return;
    }
    // Sessiz yenileme YA DA `baslat()`in yeniden tetiklenmesi ayni kullanici
    // icin oturum.value'yu TEKRAR yazabilir -- kurulum yalniz kullaniciId
    // GERCEKTEN degisince (ilk giris, ya da farkli kullaniciyla yeniden
    // giris) yeniden baslatilir.
    if (_kurulumKullaniciId == oturum.kullaniciId) {
      return;
    }
    _kurulumKullaniciId = oturum.kullaniciId;
    final gelecek = _uretimKurulumOlustur(
      widget.oturumYoneticisi,
      oturum.kullaniciId,
    );
    unawaited(
      gelecek.then((kurulum) async {
        // D8/2 PAZARLIKSIZ: acilista `gonderildi` olan TUM satirlar
        // `bekliyor`e doner (bir onceki koşum ucusun ortasinda cokmus
        // olabilir) -- main()'in ESKI davranisiyla BIREBIR AYNI sira.
        await kurulum.dongu.gonderildiKurtar();
        unawaited(kurulum.dongu.turCalistir());
        unawaited(kurulum.dongu.cekmeTuruCalistir());
      }),
    );
    setState(() {
      _kurulumGelecek = gelecek;
    });
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Momentum',
      debugShowCheckedModeBanner: false,
      // ODEV.md §4(a) canli turunda OLCULDU (o74, Pages demosu): Material'in
      // YERLESIK diyaloglari (`showDatePicker`) INGILIZCE ciziliyordu --
      // delege listesi verilmezse Flutter `DefaultMaterialLocalizations`a
      // duser ve o YALNIZ en_US tasir. 🔴 `locale` SABIT 'tr' PAZARLIKSIZ.
      locale: const Locale('tr'),
      localizationsDelegates: GlobalMaterialLocalizations.delegates,
      supportedLocales: const [Locale('tr')],
      theme: MomentumTema.olustur(Brightness.light),
      darkTheme: MomentumTema.olustur(Brightness.dark),
      home: _govdeOlustur(),
    );
  }

  Widget _govdeOlustur() {
    final oturum = widget.oturumYoneticisi.oturum.value;
    if (oturum == null) {
      return GirisEkrani(oturumYoneticisi: widget.oturumYoneticisi);
    }
    return FutureBuilder<_UretimKurulumu>(
      future: _kurulumGelecek,
      builder: (context, anlik) {
        final kurulum = anlik.data;
        if (kurulum == null) {
          return const Scaffold(
            body: Center(child: CircularProgressIndicator()),
          );
        }
        // G42/c (w2_dikis_kapisi_test.dart, M212 kapisi): ayri bir yerel
        // degisken -- ekrana gecirilen bildirimin GERCEKTEN kurulumdan
        // geldigi statik olarak da izlenebilir kalsin.
        final depolama = kurulum.depolamaBildirimi;
        return GorevListesiEkrani(
          depo: kurulum.depo,
          // K112: "Yenile" bugune kadar YALNIZ cekme kosuyordu.
          onYenile: () => elleYenile(kurulum.dongu),
          // K112: yerel yazma sonrasi ITME turu.
          onYerelYazma: kurulum.dongu.turCalistir,
          depolama: depolama,
          // IS-EMRI-o83 s2.2/12: yenileme token'i sunucuda iptal edilir
          // (OturumYoneticisi.cikisYap). Yerel veriye DOKUNULMAZ -- s2.3'teki
          // karar SADECE farkli kullaniciyla giriste devreye girer
          // (ayarlariHazirla ezme karsilastirmasi bir sonraki girişte kosar).
          onCikisYap: () => unawaited(widget.oturumYoneticisi.cikisYap()),
          // IS-EMRI-o86-B §C: paylas akisinin GEREKSINIMLERI -- F4 dikisi
          // geregi ham Drift satiri (`SenkronKuyruguRow`) UI KATMANINA
          // GECMEZ, burada kucuk bir kayda (record) UYARLANIR.
          kullaniciAramaAgi: kurulum.kullaniciAramaAgi,
          actorId: kurulum.actorId,
          uyeEkle: kurulum.uyeEkle,
          paylasimKuyrukSatiriniOku: (opId) async {
            final satir = await kurulum.dongu.kuyrukSatiriniOku(opId);
            return satir == null
                ? null
                : (durum: satir.durum, sonHataKodu: satir.sonHataKodu);
          },
        );
      },
    );
  }
}

/// F5: `--dart-define=DURUM_VITRINI=true` yolunda kullanilan minimal kabuk --
/// [depo] `null` iken durum vitrinini gosterir, giris ekranini/OturumYoneticisi'ni
/// hic kurmaz.
class MomentumUygulamasi extends StatelessWidget {
  final GorevDeposu? depo;
  final SenkronDongusu? dongu;
  // GOREV-W2 T5: `null` ise (durum vitrini) GorevListesiEkrani'ne HIC gecer,
  // serit hic cizilmez.
  final DepolamaBildirimi? depolama;

  const MomentumUygulamasi({
    super.key,
    required this.depo,
    this.dongu,
    this.depolama,
  });

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Momentum',
      debugShowCheckedModeBanner: false,
      locale: const Locale('tr'),
      localizationsDelegates: GlobalMaterialLocalizations.delegates,
      supportedLocales: const [Locale('tr')],
      theme: MomentumTema.olustur(Brightness.light),
      darkTheme: MomentumTema.olustur(Brightness.dark),
      // F5: --dart-define=DURUM_VITRINI=true ile durum vitrini acilir.
      home: depo == null
          ? const DurumVitrini()
          : GorevListesiEkrani(
              depo: depo!,
              onYenile: dongu == null ? null : () => elleYenile(dongu!),
              onYerelYazma: dongu?.turCalistir,
              depolama: depolama,
            ),
    );
  }
}

/// K112 (oturum 48 -- cihazda olculdu): elle yenileme bugune kadar YALNIZ
/// `cekmeTuruCalistir`a bagliydi; o tur govdeyi DAIMA `ops:[]` ile kurar
/// (slice-3d D0) ⇒ kuyrukta bekleyen satir varsa kullanici yenilemeye bassa
/// bile GITMIYORDU. Artik once ITME, sonra CEKME. Ikisi ayni tek-ucus
/// kilidini paylasir (D4); ikinci cagri devam eden turu beklerse K3 bayragi
/// zaten bir kez yeniden kosturur.
Future<void> elleYenile(SenkronDongusu dongu) async {
  await dongu.turCalistir();
  await dongu.cekmeTuruCalistir();
}

class _UretimKurulumu {
  final GorevDeposu depo;
  final SenkronDongusu dongu;
  // GOREV-slice-3e-G12 T2: sinyal artik BURADA tutulur -- sahipsiz kalirsa
  // `durdur()`u cagiracak kimse olmaz (K79/0 ikinci bulgu).
  final GercekZamanliSinyal sinyal;
  // GOREV-W2 T5: dikisin (Veritabani.onResult) yazdigi, ekranin dinledigi
  // TEK bildirim.
  final DepolamaBildirimi depolamaBildirimi;
  // IS-EMRI-o86-B §C: paylas akisinin GEREKSINIMLERI. K-o88/4: `uyeEkle`
  // `depo`nun (GorevDeposu, arayuz) DISINDA AYRI tasinir -- `DriftGorevDeposu`
  // somut instance'inin tear-off'udur (gorev_deposu.dart'taki dosya-seviyesi
  // yoruma bkz.).
  final KullaniciAramaAgi kullaniciAramaAgi;
  final String actorId;
  final Future<String> Function(String projeId, String userId) uyeEkle;
  const _UretimKurulumu(
    this.depo,
    this.dongu,
    this.sinyal,
    this.depolamaBildirimi,
    this.kullaniciAramaAgi,
    this.actorId,
    this.uyeEkle,
  );
}

/// GOREV-slice-3c T3/T4/T5/T6 (IS-EMRI-o83 ile GENISLETILDI): ayarlar
/// (clientId/devUserId) once yuklenir/uretilir -- `ezme` ARTIK derleme-zamani
/// `DEV_USER_ID` DEGIL, [kullaniciId] (giris yapan kullanicinin GERCEK
/// kimligi, `OturumYoneticisi`den). AYNI MEKANIZMA YENIDEN KULLANILIR (is
/// emri s2.3): `ezme` yereldeki kimlikten farkliysa `ayarlariHazirla` yerel
/// gorevleri/kuyrugu SESSIZCE TEMIZLER -- yeni bir mekanizma icat edilmedi.
/// HLC ureteci VE senkron dongusu ayni ornegi (`hlc`) PAYLASIR (biri yeni op
/// damgalar, digeri sunucu yanitiyla birlestirir).
Future<_UretimKurulumu> _uretimKurulumOlustur(
  OturumYoneticisi oturumYoneticisi,
  String kullaniciId,
) async {
  // GOREV-W2 T5/D-W2-7: baslangic degeri `olculmedi` SABITINDEDIR -- native
  // yolda dikis (`onResult`) hic cagrilmadigi icin (olculdu: `drift_flutter`
  // native implementasyonu `web:` secenegini hic okumuyor) bu deger DEGISMEZ.
  final depolamaBildirimi = DepolamaBildirimi(const DepolamaDurumu.olculmedi());
  final db = Veritabani(null, depolamaBildirimi);
  final ayarlarDeposu = AyarlarDeposu(db, idUret: uretimIdUret);
  final ayarlar = await ayarlariHazirla(db, ayarlarDeposu, ezme: kullaniciId);
  final hlc = HlcUretici(
    simdiMs: () => DateTime.now().toUtc().millisecondsSinceEpoch,
    clientId: ayarlar.clientId,
    sonWall: ayarlar.sonWall,
    sonCounter: ayarlar.sonCounter,
  );

  final depo = DriftGorevDeposu(
    db,
    saat: () => DateTime.now().toUtc(),
    idUret: uretimIdUret,
    hlc: hlc,
    ayarlarDeposu: ayarlarDeposu,
    actorId: ayarlar.devUserId,
  );

  final dongu = SenkronDongusu(
    db: db,
    agi: HttpSenkronAgi(
      senkronUcNoktasi: Uri.parse('$_senkronSunucuUrl/v1/sync'),
      actorId: ayarlar.devUserId,
      // IS-EMRI-o83 s2.2/10: 401'de sessiz yenileme -- ikisi de
      // OturumYoneticisi'ne devredilir, HttpSenkronAgi JWT/refresh'in
      // HICBIR AYRINTISINI bilmez (tek-yon bagimlilik).
      erisimJetonuAl: oturumYoneticisi.gecerliErisimJetonuAl,
      jetonuYenile: oturumYoneticisi.yenile,
    ),
    ayarlarDeposu: ayarlarDeposu,
    hlc: hlc,
    clientId: ayarlar.clientId,
    devUserId: ayarlar.devUserId,
    baslangicCursorJson: ayarlar.nextCursorJson,
  );

  // GOREV-slice-3e T3: gercek zamanli sinyal -- K77/5 dogrudan esleme, EK
  // debounce/zamanlayici/kuyruk YOK. `_senkronSunucuUrl`den YENIDEN turetilir,
  // SENKRON_SUNUCU_URL icin IKINCI bir derleme-zamani ortam okumasi EKLENMEZ.
  final sinyal = SignalrJsonSinyal(
    sunucuTabanUrl: _senkronSunucuUrl,
    actorId: ayarlar.devUserId,
  );
  sinyal.olaylar.listen((_) => unawaited(dongu.cekmeTuruCalistir()));
  unawaited(sinyal.baslat());

  // IS-EMRI-o86-B §B (D-B1): jeton isleme HttpSenkronAgi ile AYNI deseni --
  // `_senkronSunucuUrl`den YENIDEN turetilir, ikinci bir derleme-zamani ortam
  // okumasi EKLENMEZ.
  final kullaniciAramaAgi = HttpKullaniciAramaAgi(
    lookupUcNoktasi: Uri.parse('$_senkronSunucuUrl/v1/users/lookup'),
    actorId: ayarlar.devUserId,
    erisimJetonuAl: oturumYoneticisi.gecerliErisimJetonuAl,
    jetonuYenile: oturumYoneticisi.yenile,
  );

  return _UretimKurulumu(
    depo,
    dongu,
    sinyal,
    depolamaBildirimi,
    kullaniciAramaAgi,
    ayarlar.devUserId,
    depo.uyeEkle,
  );
}

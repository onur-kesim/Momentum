import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;

import 'kullanici_arama_agi.dart';

/// IS-EMRI-o86-B §B (D-B1 PAZARLIKSIZ): `KullaniciAramaAgi`nin gercek HTTP
/// uygulamasi -- `POST {taban}/v1/users/lookup` (Z2/Z3). Jeton isleme
/// `HttpSenkronAgi`nin BIREBIR AYNI deseni: [erisimJetonuAl]/[jetonuYenile]
/// enjekte edilir, verildiginde istek `Authorization: Bearer` tasir, 401
/// gelirse [jetonuYenile] TEK KEZ denenir ve basariliysa istek AYNEN
/// TEKRARLANIR. `AuthAgi`ye DOKUNULMAZ -- kimlik dilimi kapali, o `AuthSonucu`
/// doner, bu uc `userId` doner (ayri sozlesme).
///
/// D-B2: yanitta `userId` yoksa/`null` ise ⇒ `KullaniciBulunamadi`. HAM govde
/// ASLA loglanmaz (e-posta PII, D-B2 pazarliksiz).
class HttpKullaniciAramaAgi implements KullaniciAramaAgi {
  static const String _devKullaniciBasligi = 'X-Momentum-Dev-User';

  final http.Client _istemci;
  final Uri lookupUcNoktasi;
  final String actorId;
  final Duration zamanAsimi;
  final Future<String?> Function()? erisimJetonuAl;
  final Future<bool> Function()? jetonuYenile;

  HttpKullaniciAramaAgi({
    required this.lookupUcNoktasi,
    required this.actorId,
    http.Client? istemci,
    this.zamanAsimi = const Duration(seconds: 20),
    this.erisimJetonuAl,
    this.jetonuYenile,
  }) : _istemci = istemci ?? http.Client();

  @override
  Future<KullaniciAramaSonucu> ara(String eposta) async {
    var yanit = await _tekIstekGonder(eposta);
    if (yanit != null && yanit.statusCode == 401 && jetonuYenile != null) {
      final yenilendiMi = await jetonuYenile!();
      if (yenilendiMi) {
        yanit = await _tekIstekGonder(eposta);
      }
    }
    return _sonucaCevir(yanit);
  }

  Future<http.Response?> _tekIstekGonder(String eposta) async {
    try {
      final basliklar = {
        'Content-Type': 'application/json',
        _devKullaniciBasligi: actorId,
      };
      final jeton = erisimJetonuAl == null ? null : await erisimJetonuAl!();
      if (jeton != null) {
        basliklar['Authorization'] = 'Bearer $jeton';
      }
      return await _istemci
          .post(
            lookupUcNoktasi,
            headers: basliklar,
            body: jsonEncode({'email': eposta}),
          )
          .timeout(zamanAsimi);
    } catch (_) {
      // D9'daki "ag hatasi / zaman asimi" siniflandirmasiyla AYNI: HTTP durum
      // kodu YOKLUGUNA bakilir, istisna TIPINE degil.
      return null;
    }
  }

  /// D-B2: ham govde burada bile SATIRA DOKULMEZ (log YOK) -- yalniz
  /// `userId` alani okunur, geri kalan govde ATILIR.
  KullaniciAramaSonucu _sonucaCevir(http.Response? yanit) {
    if (yanit == null) {
      return const KullaniciAramaHatasi();
    }
    if (yanit.statusCode == 404) {
      return const KullaniciBulunamadi();
    }
    if (yanit.statusCode != 200) {
      return const KullaniciAramaHatasi();
    }
    try {
      final govde = jsonDecode(yanit.body) as Map<String, Object?>;
      final userId = govde['userId'] as String?;
      return userId == null
          ? const KullaniciBulunamadi()
          : KullaniciBulundu(userId);
    } catch (_) {
      return const KullaniciAramaHatasi();
    }
  }
}

/// IS-EMRI-o86-B §B: `POST /v1/users/lookup` soyutlamasi -- davet akisi (§C)
/// bunun ARKASINDA calisir, gercek HTTP'yi hic bilmez. `senkron_agi.dart`nin
/// AYNI deseni (sealed sonuc + soyut kapi).
///
/// D-B3: uc sonuc AYRI SINIFLARDIR -- `null` ile "bulunamadi"yi ayirt etmek
/// caGiranin isi OLMAZ (o84'un golgelenme sinifiyla AYNI ders: bir dizge/deger
/// birden fazla anlam tasimaz).
sealed class KullaniciAramaSonucu {
  const KullaniciAramaSonucu();
}

/// Sunucu `200` + `userId` dondurdu (`LookupUserQuery`, Z2).
class KullaniciBulundu extends KullaniciAramaSonucu {
  final String userId;
  const KullaniciBulundu(this.userId);
}

/// Sunucu `404` dondurdu (kayitli kullanici yok) YA DA `200` govdesinde
/// `userId` alani yok/`null` (D-B2, savunmaci -- gercek sunucu bugun yalniz
/// 404 doner, ama sinif bu ayrimi caGirana YIKMAZ).
class KullaniciBulunamadi extends KullaniciAramaSonucu {
  const KullaniciBulunamadi();
}

/// Ag/zaman asimi/5xx/401-sonrasi (jetonuYenile de dusmusse) -- D9'daki
/// "ag hatasi / zaman asimi" siniflandirmasiyla AYNI ruh.
class KullaniciAramaHatasi extends KullaniciAramaSonucu {
  const KullaniciAramaHatasi();
}

abstract class KullaniciAramaAgi {
  /// [eposta] zaten yerel dogrulamadan GECMIS olmalidir (§C adim 1) --
  /// bu kapi kendi basina bos/bicimsiz dizgeyi REDDETMEZ, sunucuya sorar.
  Future<KullaniciAramaSonucu> ara(String eposta);
}

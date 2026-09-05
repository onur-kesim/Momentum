import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import '../ag/kullanici_arama_agi.dart';
import '../design/metinler.dart';
import '../design/tokens.dart';
import '../veri/depolama_durumu.dart';
import '../veri/gorev_deposu.dart';
import 'arama_eslestirme.dart';
import 'bos_durum.dart';
import 'depolama_seridi.dart';
import 'gorev_ekle_alani.dart';
import 'gorev_satiri.dart';
import 'hata_durumu.dart';
import 'yukleme_durumu.dart';

/// SS3.1 -- tek vitrin ekrani (gercek, akis-tabanli ekran; durum_vitrini.dart
/// ile KARISTIRILMAZ, o statik/deterministik bir gosterimdir).
///
/// F5 PAZARLIKSIZ: G5 bu ekranin uzerinde de kosar (bos * yerel * hata).
/// GOREV-R10 D5: rozet turetme (`rozetDikisi`) artik `veri/gorev_deposu.dart`
/// katmanindadir -- bu ekran `GorevGorunum.senkronDurumu`/`cakismaVarMi`yi
/// DOGRUDAN okur, kendisi turetme yapmaz (F4 dikisi: widget'lar tasima
/// katmani durumunu gormez).
class GorevListesiEkrani extends StatefulWidget {
  final GorevDeposu depo;
  // slice-3d D0: KAPALI LISTE'deki dort tetikleyiciden biri -- "kullanici
  // elle yenilediginde bir cekme turu". `null` ise (mevcut testler/durum
  // vitrini) yenile dugmesi HIC gosterilmez -- geriye donuk uyumlu.
  final Future<void> Function()? onYenile;

  // K112 (oturum 48 -- CIHAZDA OLCULDU): yerel yazma ITMEYI tetiklemiyordu.
  // Gorev ekleniyor, "Bu cihazda" rozetiyle duruyor ve uygulama YENIDEN
  // BASLATILANA kadar sunucuya gitmiyordu (olcum: 60 s + elle yenileme 40 s
  // bekledi, gelmedi; yeniden baslatinca 14,4 s ve 23,5 s'te geldi).
  // Bu geri cagri her YEREL YAZMA'dan SONRA bir itme turu ister.
  // slice-3d D0'i IHLAL ETMEZ: zamanlayici degil, OLAY tetiklidir --
  // periyodik yoklama yasagi yerinde durur.
  // `null` ise (mevcut testler / durum vitrini) hicbir sey tetiklenmez.
  final Future<void> Function()? onYerelYazma;

  // GOREV-W2 T4: `null` ise (mevcut testler/durum vitrini) SERIT HIC CIZILMEZ
  // -- geriye donuk uyumlu, DepolamaSeridi'ye hic ulasilmaz.
  final ValueListenable<DepolamaDurumu>? depolama;

  // IS-EMRI-o83 s2.2/12: `null` ise (mevcut testler/durum vitrini) cikis
  // dugmesi HIC gosterilmez -- geriye donuk uyumlu (onYenile/onYerelYazma/
  // depolama'nin AYNI deseni). Yerel veriye ne olacagi main.dart'taki
  // OturumYoneticisi.cikisYap ile AYNI karardadir (s2.3).
  final VoidCallback? onCikisYap;

  // IS-EMRI-o86-B §C/§D: paylas akisinin `GorevDeposu`nun DISINDA kalan
  // gereksinimleri -- kullanici arama (HTTP) ve kuyruk-satiri-okuma (§C
  // adim 6) `SenkronDongusu`/HTTP katmanina aittir, veri katmani BUNLARI
  // BILMEZ (F4 dikisi). K-o88/4: `uyeEkle` de BILEREK BURADA (AYRI geri
  // cagri) -- `GorevDeposu` arayuzune EKLENMEDI (gorev_deposu.dart'taki
  // dosya-seviyesi yoruma bkz., 13 sahte test deposunu ETKILEMEMEK icin).
  // Hicbiri `null` ise (mevcut testler/durum vitrini) paylas DIYALOGU HICBIR
  // SEY YAPMAZ -- onYenile/onCikisYap'in AYNI geriye-donuk-uyumlu deseni;
  // DUGMENIN KENDISI YINE DE GORUNUR (D-D4: sahiplik istemcide bilinemez, Z9).
  final KullaniciAramaAgi? kullaniciAramaAgi;
  final String? actorId;
  final Future<({String durum, String? sonHataKodu})?> Function(String opId)?
  paylasimKuyrukSatiriniOku;
  final Future<String> Function(String projeId, String userId)? uyeEkle;

  const GorevListesiEkrani({
    super.key,
    required this.depo,
    this.onYenile,
    this.onYerelYazma,
    this.depolama,
    this.onCikisYap,
    this.kullaniciAramaAgi,
    this.actorId,
    this.paylasimKuyrukSatiriniOku,
    this.uyeEkle,
  });

  @override
  State<GorevListesiEkrani> createState() => _GorevListesiEkraniState();
}

class _GorevListesiEkraniState extends State<GorevListesiEkrani> {
  late Stream<List<GorevGorunum>> _akis;

  /// IS-EMRI-o85-A D1: Drawer'i besleyen liste akisi -- `_akis`tan AYRI
  /// stream (Drift `.watch()` -- ayri tablo, ayri degisiklik kaynagi).
  late Stream<List<Proje>> _listelerAkisi;

  /// IS-EMRI-o85-A K1/D4: AKTIF liste -- `null` = Gelen Kutusu (K4). 🔴
  /// D4 PAZARLIKSIZ: bu bir SUZGEC DEGIL, BAGLAMDIR -- `_suzgecleriTemizle`
  /// (asagida) BUNA DOKUNMAZ. DURUM sinir 28'in "ekleme suzgecleri
  /// SIFIRLAR" kurali arama/etiket cipleri icindir; aktif liste sifirlanirsa
  /// kullanici bir listeye gorev ekleyip Gelen Kutusu'na firlar.
  String? _secilenListeId;

  /// ODEV.md §4(a): TEK SECIM (Onur kilidi ④ -- coklu secimin AND/OR
  /// semantigi ve arama alani KAPSAM DISI). `null` = suzme yok.
  /// 🔴 SUZME DART TARAFINDA: AYNI stream kullanilir, yeniden ABONE
  /// OLUNMAZ -- yeni bir sorgu acmak ara karede bos liste (ve BosDurum
  /// yanip sonmesi) dogururdu.
  String? _secilenEtiket;

  /// ODEV.md §4(a) ARAMA (Onur kilidi -- 15 Agu): her tusta CANLI, debounce
  /// YOK; suzme AYNI stream uzerinde, etiket cipiyle CARPILARAK yapilir.
  /// Durum yalniz bellektedir ⇒ sekme kapaninca KENDILIGINDEN sifirlanir
  /// (cip seciminin o76'da olculen davranisinin AYNISI).
  String _arama = '';
  final _aramaDenetleyici = TextEditingController();

  @override
  void initState() {
    super.initState();
    _akis = widget.depo.gorevlerGorunur();
    _listelerAkisi = widget.depo.listelerGorunur();
  }

  @override
  void dispose() {
    _aramaDenetleyici.dispose();
    super.dispose();
  }

  /// Bos sonuc ekranindaki tek dokunus: aramayi VE cip secimini birlikte
  /// sifirlar (kilit maddesi 3 -- kullanici cikmazda birakilmaz).
  void _suzgecleriTemizle() {
    _aramaDenetleyici.clear();
    setState(() {
      _arama = '';
      _secilenEtiket = null;
    });
  }

  /// K112: once YEREL YAZMA tamamlanir, SONRA itme tetiklenir. Sira
  /// pazarliksizdir -- tersi olursa itme turu kuyrugu HENUZ BOS gorur ve
  /// tetikleyici sessizce hicbir sey yapmaz (bu, kapinin mutantidir).
  Future<void> _yerelYaz(Future<void> Function() yazma) async {
    await yazma();
    final tetik = widget.onYerelYazma;
    if (tetik != null) await tetik();
  }

  void _yenidenDene() {
    setState(() {
      _akis = widget.depo.gorevlerGorunur();
    });
  }

  /// IS-EMRI-o85-A C4: `_secilenListeId` GECERLI (silinmemis) bir listeyi
  /// gosteriyorsa AYNEN kullanilir; degilse (hic secilmedi ya da secili
  /// liste SILINDI -- baska bir cihazdan bile olsa) Gelen Kutusu'na duser.
  /// "Etkin secim" TEK KAYNAKTIR: bos durum adi, Drawer vurgusu, suzme ve
  /// yeni gorevin dogacagi liste HEP BUNU okur -- ikinci bir karar noktasi
  /// acilmaz.
  String? _etkinSecim(List<Proje> listeler) {
    final id = _secilenListeId;
    if (id == null) return null;
    return listeler.any((p) => p.id == id) ? id : null;
  }

  String _aktifListeAdi(List<Proje> listeler, String? etkinSecim) {
    if (etkinSecim == null) return Metinler.gelenKutusu;
    for (final proje in listeler) {
      if (proje.id == etkinSecim) return proje.ad;
    }
    return Metinler.gelenKutusu;
  }

  Future<void> _listeEkleDiyaloguAc(BuildContext context) async {
    final ad = await showDialog<String>(
      context: context,
      builder: (_) =>
          const _ListeAdiDiyalogu(baslik: Metinler.listeYeniDugmesi),
    );
    if (ad == null) return;
    unawaited(_yerelYaz(() => widget.depo.listeEkle(ad)));
  }

  Future<void> _listeYenidenAdlandirDiyaloguAc(
    BuildContext context,
    Proje proje,
  ) async {
    final ad = await showDialog<String>(
      context: context,
      builder: (_) => _ListeAdiDiyalogu(
        baslik: Metinler.listeYenidenAdlandirDugmesi,
        baslangicAdi: proje.ad,
      ),
    );
    if (ad == null || ad == proje.ad) return;
    unawaited(_yerelYaz(() => widget.depo.listeDuzenle(proje.id, ad)));
  }

  /// K5: silinen liste O ANDA secili idiyse Gelen Kutusu'na donulur --
  /// gorevlerin `projeId`si DEGISTIRILMEZ (C4), yalniz ekran baglami duser.
  Future<void> _listeSilOnayDiyaloguAc(
    BuildContext context,
    Proje proje,
  ) async {
    final onaylandi = await showDialog<bool>(
      context: context,
      builder: (dialogBaglami) => AlertDialog(
        title: Text(
          Metinler.listeSil,
          overflow: TextOverflow.ellipsis,
          maxLines: 1,
        ),
        content: Text(
          Metinler.listeSilOnay,
          overflow: TextOverflow.ellipsis,
          maxLines: 1,
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogBaglami).pop(),
            child: Text(
              Metinler.iptalDugmesi,
              overflow: TextOverflow.ellipsis,
              maxLines: 1,
            ),
          ),
          TextButton(
            onPressed: () => Navigator.of(dialogBaglami).pop(true),
            child: Text(
              Metinler.listeSil,
              overflow: TextOverflow.ellipsis,
              maxLines: 1,
            ),
          ),
        ],
      ),
    );
    if (onaylandi != true) return;
    if (_secilenListeId == proje.id) {
      setState(() => _secilenListeId = null);
    }
    unawaited(_yerelYaz(() => widget.depo.listeSil(proje.id)));
  }

  /// IS-EMRI-o86-B §C/§D: davet diyalogunu acar -- `_ListeAdiDiyalogu`nun
  /// AYNI deseni (diyalog bir DEGER doner, YAZMA/bildirim BURADA olur), ama
  /// diyalogun KENDISI §C'nin uc-adimli akisini (lookup->op->tur->kuyruk
  /// okuma) ICINDE yurutur -- ara durumlar (yukleniyor/hata) diyalog ACIK
  /// KALIRKEN gosterilmelidir, `_ListeAdiDiyalogu` gibi TEK deger donup
  /// kapanamaz. Dort yetenek `null`sa (mevcut testler/durum vitrini) HICBIR
  /// SEY yapmaz -- dugme YINE DE gorunur kalir (D-D4).
  Future<void> _listePaylasDiyaloguAc(BuildContext context, Proje proje) async {
    final aramaAgi = widget.kullaniciAramaAgi;
    final actorId = widget.actorId;
    final kuyrukOku = widget.paylasimKuyrukSatiriniOku;
    final uyeEkle = widget.uyeEkle;
    if (aramaAgi == null || actorId == null || kuyrukOku == null ||
        uyeEkle == null) {
      return;
    }

    final eposta = await showDialog<String>(
      context: context,
      builder: (_) => _ListePaylasDiyalogu(
        projeId: proje.id,
        actorId: actorId,
        kullaniciAramaAgi: aramaAgi,
        uyeEkle: uyeEkle,
        turCalistir: widget.onYerelYazma,
        kuyrukSatiriniOku: kuyrukOku,
      ),
    );
    if (eposta == null || !context.mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
          Metinler.listePaylasBasarili(eposta),
          overflow: TextOverflow.ellipsis,
          maxLines: 1,
        ),
      ),
    );
  }

  /// IS-EMRI-o85-A D1: `[Gelen Kutusu] + listeler`. Secim ekranin
  /// BAGLAMIDIR (D4) -- dokunus hem `_secilenListeId`yi degistirir hem
  /// Drawer'i kapatir.
  Widget _drawer(
    BuildContext context,
    List<Proje> listeler,
    String? etkinSecim,
  ) {
    return Drawer(
      child: SafeArea(
        child: Column(
          children: [
            Expanded(
              child: ListView(
                children: [
                  ListTile(
                    key: const ValueKey('liste_gelen_kutusu'),
                    leading: const Icon(Icons.inbox_outlined),
                    title: Text(
                      Metinler.gelenKutusu,
                      overflow: TextOverflow.ellipsis,
                      maxLines: 1,
                    ),
                    selected: etkinSecim == null,
                    onTap: () {
                      Navigator.of(context).pop();
                      setState(() => _secilenListeId = null);
                    },
                  ),
                  if (listeler.isNotEmpty)
                    Divider(height: 1, color: MRenk.ayirici(context)),
                  for (final proje in listeler)
                    ListTile(
                      key: ValueKey('liste_${proje.id}'),
                      leading: const Icon(Icons.list_outlined),
                      title: Text(
                        proje.ad,
                        overflow: TextOverflow.ellipsis,
                        maxLines: 1,
                      ),
                      selected: etkinSecim == proje.id,
                      onTap: () {
                        Navigator.of(context).pop();
                        setState(() => _secilenListeId = proje.id);
                      },
                      // IS-EMRI-o86-B K-o88/4 (Onur kilidi, 4 Eyl -- D-D2
                      // kirmizisi sonrasi): uc AYRI IconButton yerine TEK
                      // PopupMenuButton -- `liste_baglam_test.dart`nin iki
                      // testini kiran ROW GENISLIGI boylece ORTADAN kalkar
                      // (o dosyanin KENDISI degismedi, PAZARLIKSIZ).
                      // TUZAK olculdu: `showMenu` KENDI route'unu `onSelected`
                      // CAGRILMADAN ONCE zaten kapatir (PopupMenuButton'in
                      // ic mekanigi) -- bu yuzden burada TEK `Navigator.pop()`
                      // yeterlidir (Drawer'i kapatir), ikinci bir pop YANLIS
                      // olurdu. Kanit: paylasim_dilimi_test.dart'taki menu
                      // testi + §F/9 canli tur.
                      trailing: PopupMenuButton<String>(
                        key: ValueKey('liste_menu_${proje.id}'),
                        icon: Icon(Icons.more_vert, size: MOlcu.ikon),
                        tooltip: Metinler.listeMenuAc,
                        onSelected: (secim) {
                          Navigator.of(context).pop();
                          switch (secim) {
                            case 'duzenle':
                              _listeYenidenAdlandirDiyaloguAc(context, proje);
                            case 'paylas':
                              // D-D4: HER LISTEDE gorunur -- sahiplik
                              // istemcide bilinemez (Z9), red dali basarsa
                              // dogal olarak calisir (§C/6, §F/9).
                              _listePaylasDiyaloguAc(context, proje);
                            case 'sil':
                              _listeSilOnayDiyaloguAc(context, proje);
                          }
                        },
                        itemBuilder: (context) => [
                          PopupMenuItem(
                            key: ValueKey('liste_menu_duzenle_${proje.id}'),
                            value: 'duzenle',
                            child: Text(
                              Metinler.listeYenidenAdlandirDugmesi,
                              overflow: TextOverflow.ellipsis,
                              maxLines: 1,
                            ),
                          ),
                          PopupMenuItem(
                            key: ValueKey('liste_menu_paylas_${proje.id}'),
                            value: 'paylas',
                            child: Text(
                              Metinler.listePaylasDugmesi,
                              overflow: TextOverflow.ellipsis,
                              maxLines: 1,
                            ),
                          ),
                          PopupMenuItem(
                            key: ValueKey('liste_menu_sil_${proje.id}'),
                            value: 'sil',
                            child: Text(
                              Metinler.listeSil,
                              overflow: TextOverflow.ellipsis,
                              maxLines: 1,
                            ),
                          ),
                        ],
                      ),
                    ),
                ],
              ),
            ),
            Divider(height: 1, color: MRenk.ayirici(context)),
            ListTile(
              key: const ValueKey('liste_ekle_dugmesi'),
              leading: const Icon(Icons.add),
              title: Text(
                Metinler.listeYeniDugmesi,
                overflow: TextOverflow.ellipsis,
                maxLines: 1,
              ),
              onTap: () {
                Navigator.of(context).pop();
                _listeEkleDiyaloguAc(context);
              },
            ),
          ],
        ),
      ),
    );
  }

  /// ODEV.md §4(a): arama alani -- CIP SERIDININ USTUNDE (Onur kilidi ④:
  /// okuma sirasi ara -> daralt -> listele; AppBar acilmaz).
  ///
  /// `hintStyle` BILEREK verilir (varsayilan ipucu rengi tema turevidir ve
  /// kontrasti dusuk kalabilir). 🔴 OLCULDU (denetim, 15 Agu): bu alani
  /// HICBIR mevcut kapi olcmuyor -- `hintText` bir `Text(` olmadigi icin
  /// R1/R2/R4 statik tarayicisina gorunmez, alanin semantik dugumunde
  /// label/value BOS oldugu icin `textContrastGuideline` o dugumu ATLAR
  /// (`accessibility.dart` `shouldSkipNode`). Ipucu metni, ipucu rengi ve
  /// odak halkasinin TEK pini `test/arama_dilimi_test.dart`tedir; kapi
  /// butcesi ihlalde oldugu icin yeni kapi DOSYASI acilmadi.
  Widget _aramaAlani(BuildContext context) {
    return Padding(
      padding: EdgeInsets.fromLTRB(MBosluk.m, MBosluk.s, MBosluk.m, 0),
      child: TextField(
        key: const ValueKey('arama_alani'),
        controller: _aramaDenetleyici,
        onChanged: (deger) => setState(() => _arama = deger),
        style: MTipo.govdeM.copyWith(color: MRenk.metin(context)),
        decoration: InputDecoration(
          isDense: true,
          hintText: Metinler.aramaIpucu,
          hintStyle: MTipo.govdeM.copyWith(color: MRenk.metinIkincil(context)),
          // Dekoratiftir, DOKUNULABILIR DEGIL ⇒ A11Y-3 (ikon-yalniz BUTON
          // yasagi) ve dokunma hedefi kapisi kapsamina GIRMEZ.
          prefixIcon: Icon(
            Icons.search,
            size: MOlcu.ikon,
            color: MRenk.metinIkincil(context),
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(MRadius.m),
            borderSide: BorderSide(color: MRenk.kenarlikEtkilesim(context)),
          ),
          focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(MRadius.m),
            borderSide: BorderSide(
              width: MOlcu.odakKalinlik,
              color: MRenk.birincil(context),
            ),
          ),
        ),
      ),
    );
  }

  /// ODEV.md §4(a): süzme çip şeridi -- listenin ÜSTÜNDE, yatay kaydirilir.
  /// "Tümü" cipi suzmeyi kaldirir; SECIM metne gore degil `secili == null`
  /// durumuna gore verilir (bir kullanici "Tümü" adli bir etiket yazsa bile
  /// davranis bozulmaz).
  Widget _etiketSeridi(List<String> etiketler, String? secili) {
    return Padding(
      padding: EdgeInsets.symmetric(
        horizontal: MBosluk.m,
        vertical: MBosluk.xs,
      ),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(
          children: [
            ChoiceChip(
              key: const ValueKey('etiket_cipi_tumu'),
              label: Text(
                Metinler.etiketTumu,
                overflow: TextOverflow.ellipsis,
                maxLines: 1,
              ),
              selected: secili == null,
              onSelected: (_) => setState(() => _secilenEtiket = null),
            ),
            for (final etiket in etiketler) ...[
              SizedBox(width: MBosluk.xs),
              ChoiceChip(
                key: ValueKey('etiket_cipi_$etiket'),
                label: Text(
                  etiket,
                  overflow: TextOverflow.ellipsis,
                  maxLines: 1,
                ),
                selected: secili == etiket,
                // Ikinci dokunus suzmeyi KALDIRIR (secili cipe tekrar
                // dokunmak kullanicinin bekledigi geri alma yoludur).
                onSelected: (_) => setState(
                  () => _secilenEtiket = secili == etiket ? null : etiket,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    // IS-EMRI-o85-A D1: TUM Scaffold (Drawer + govde) TEK `List<Proje>`
    // aninagorunumune sarilir -- Drawer'in kendisi VE govdenin bos-durum
    // adi/suzme/yeni-gorev-listesi karari AYNI `etkinSecim`i (C4) okur,
    // ikinci bir karar noktasi acilmaz.
    return StreamBuilder<List<Proje>>(
      stream: _listelerAkisi,
      builder: (context, listelerAnlik) {
        final listeler = listelerAnlik.data ?? const <Proje>[];
        final etkinSecim = _etkinSecim(listeler);
        return Scaffold(
          drawer: _drawer(context, listeler, etkinSecim),
          backgroundColor: MRenk.yuzey(context),
          appBar: (widget.onYenile == null && widget.onCikisYap == null)
              ? null
              : AppBar(
                  backgroundColor: MRenk.yuzey(context),
                  elevation: 0,
                  actions: [
                    if (widget.onYenile != null)
                      IconButton(
                        key: const ValueKey('elle_yenile_dugmesi'),
                        icon: const Icon(Icons.refresh),
                        tooltip: 'Yenile',
                        onPressed: () => widget.onYenile!(),
                      ),
                    if (widget.onCikisYap != null)
                      IconButton(
                        key: const ValueKey('cikis_yap_dugmesi'),
                        icon: const Icon(Icons.logout),
                        tooltip: Metinler.cikisYapDugmesi,
                        onPressed: () => widget.onCikisYap!(),
                      ),
                  ],
                ),
          body: SafeArea(
            child: Column(
              children: [
                // GOREV-W2 T4: listenin USTUNE -- `depolama == null` iken
                // ValueListenableBuilder hic kurulmaz, DepolamaSeridi hic
                // olusmaz (geriye donuk uyumlu).
                if (widget.depolama != null)
                  ValueListenableBuilder<DepolamaDurumu>(
                    valueListenable: widget.depolama!,
                    builder: (context, durum, _) =>
                        DepolamaSeridi(durum: durum),
                  ),
                Expanded(
                  child: StreamBuilder<List<GorevGorunum>>(
                    stream: _akis,
                    builder: (context, anlik) {
                      if (anlik.hasError) {
                        return HataDurumu(onYenidenDene: _yenidenDene);
                      }
                      if (!anlik.hasData) {
                        return const YuklenmeDurumu();
                      }
                      final gorunumler = anlik.data!;
                      // IS-EMRI-o85-A D4/C4: liste BAGLAMDIR, en DIS suzgec --
                      // etiket/arama'dan ONCE uygulanir ve BosDurum'un "hic
                      // gorev yok" karari BUNA gore verilir (aktif listede/
                      // Gelen Kutusu'nda gercekten tek gorev yoksa). Gelen
                      // Kutusu = projeId YOK YA DA projeId ARTIK GECERSIZ
                      // (liste silindi, C4 -- `etkinSecim` zaten bu kurala gore
                      // hesaplandi, TEK KAYNAK).
                      final aktifListeIdleri = listeler
                          .map((p) => p.id)
                          .toSet();
                      final listedekiler = gorunumler.where((g) {
                        if (etkinSecim != null) {
                          return g.gorev.projeId == etkinSecim;
                        }
                        return g.gorev.projeId == null ||
                            !aktifListeIdleri.contains(g.gorev.projeId);
                      }).toList();
                      if (listedekiler.isEmpty) {
                        // D5: liste adi YALNIZ gercek bir liste secili VE
                        // BOSKEN eklenir -- Gelen Kutusu'nun (etkinSecim ==
                        // null) bos durumu MEVCUT "Henüz görev yok." metnini
                        // AYNEN korur (agac BIREBIR ESKISI, a11y/vitrin
                        // testleri ETKILENMEZ).
                        return BosDurum(
                          listeAdi: etkinSecim == null
                              ? null
                              : _aktifListeAdi(listeler, etkinSecim),
                        );
                      }
                      final tumEtiketler = <String>{
                        for (final g in listedekiler) ...g.gorev.etiketler,
                      }.toList()..sort();
                      // Secili etiket listeden KAYBOLDUYSA (son tasiyicisi
                      // silindi/etiketi kaldirildi) suzme KENDILIGINDEN duser.
                      // `setState` BUILD ICINDE CAGRILMAZ -- yerel degiskenle
                      // cozulur, alan bir sonraki dokunusta zaten guncellenir.
                      final secili = tumEtiketler.contains(_secilenEtiket)
                          ? _secilenEtiket
                          : null;
                      // 🔴 TEK SUZME YOLU: etiket cipi ile arama AYNI `where`da
                      // CARPILIR. Ikinci bir suzme yolu acmak `kanonik-kopya`
                      // riskidir; eslesme kurali da burada DEGIL, saf
                      // `aramaEslesir`dedir (bos sorgu = suzme yok orada karara
                      // baglanir, burada IKINCI KEZ kontrol EDILMEZ).
                      final suzulmus = listedekiler
                          .where(
                            (g) =>
                                (secili == null ||
                                    g.gorev.etiketler.contains(secili)) &&
                                aramaEslesir(
                                  baslik: g.gorev.baslik,
                                  sorgu: _arama,
                                ),
                          )
                          .toList();
                      return Column(
                        children: [
                          _aramaAlani(context),
                          // Etiketsiz projede serit HIC CIZILMEZ ⇒ mevcut duzen
                          // testleri (bos/yerel/hata vitrinleri dahil) ETKILENMEZ.
                          if (tumEtiketler.isNotEmpty)
                            _etiketSeridi(tumEtiketler, secili),
                          Expanded(
                            child: suzulmus.isEmpty
                                // Gorev VAR ama suzgecler hicbirini gecirmiyor:
                                // "Henüz görev yok." demek YALAN olurdu.
                                ? BosDurum.eslesmeYok(
                                    onSuzgecleriTemizle: _suzgecleriTemizle,
                                  )
                                : ListView.builder(
                                    padding: EdgeInsets.symmetric(
                                      horizontal: MBosluk.m,
                                    ),
                                    itemCount: suzulmus.length,
                                    itemBuilder: (context, i) {
                                      final gorunum = suzulmus[i];
                                      return GorevSatiri(
                                        key: ValueKey(
                                          'gorev_satiri_${gorunum.gorev.id}',
                                        ),
                                        gorev: gorunum.gorev,
                                        onTamamlaDegisti: (deger) => unawaited(
                                          _yerelYaz(
                                            () => widget.depo.tamamlaGeriAl(
                                              gorunum.gorev.id,
                                              tamamlandi: deger,
                                            ),
                                          ),
                                        ),
                                        senkronDurumu: gorunum.senkronDurumu,
                                        cakismaVarMi: gorunum.cakismaVarMi,
                                        depo: widget.depo,
                                        listeler: listeler,
                                        // IS-EMRI-o68 kriter 3 + ODEV.md §4(a):
                                        // duzenleme boylece URUN YOLUNDAN cagrilir --
                                        // `onTamamlaDegisti`'nin BIREBIR AYNI deseni
                                        // (K112: once YEREL YAZMA, sonra itme --
                                        // `_yerelYaz` sarmalayicisi ATLANMAZ).
                                        onAyrintilarDuzenlendi: (degisiklik) =>
                                            unawaited(
                                              _yerelYaz(
                                                () => widget.depo
                                                    .ayrintilariGuncelle(
                                                      gorunum.gorev.id,
                                                      baslik: degisiklik.baslik,
                                                      oncelik:
                                                          degisiklik.oncelik,
                                                      sonTarih:
                                                          degisiklik.sonTarih,
                                                      projeId:
                                                          degisiklik.projeId,
                                                      etiketEklenen: degisiklik
                                                          .etiketEklenen,
                                                      etiketSilinen: degisiklik
                                                          .etiketSilinen,
                                                    ),
                                              ),
                                            ),
                                        // IS-EMRI-o72: duzenleme kablosunun BIREBIR
                                        // AYNI deseni (K112: once YEREL YAZMA, sonra
                                        // itme -- `_yerelYaz` sarmalayicisi ATLANMAZ).
                                        onSil: () => unawaited(
                                          _yerelYaz(
                                            () => widget.depo.sil(
                                              gorunum.gorev.id,
                                            ),
                                          ),
                                        ),
                                      );
                                    },
                                  ),
                          ),
                        ],
                      );
                    },
                  ),
                ),
                GorevEkleAlani(
                  // K112: ONCE yerel yazma, SONRA itme (`_yerelYaz` sirayi
                  // pazarliksiz tutar). Dort alan TEK `ekle()` cagrisina gider ⇒
                  // TEK `WireOp`.
                  // 🔴 EKLEME SUZGECLERI SIFIRLAR [Onur kilidi, 16 Agu 2026].
                  // OLCULDU (bagimsiz denetim, 15 Agu): suzgec acikken eklenen
                  // gorev listeye GIRIYOR ama ekranda GORUNMUYORDU ve hicbir
                  // geri bildirim yoktu (depoda 2 gorev / ekranda 1 satir /
                  // bildirim 0) -- kullanici yazdigini kaybettigini sanardi.
                  // SENKRON sifirlanir: akis yeni degeri yayinlamadan once
                  // suzgec duser, ara karede satir KAYBOLMAZ. `onEkle` yalnizca
                  // dogrulamayi GECEN girdide atesler (`gorevBasligiDogrula`) ⇒
                  // bos/gecersiz girdi suzgeci SIFIRLAMAZ.
                  onEkle: (istek) {
                    _suzgecleriTemizle();
                    unawaited(
                      _yerelYaz(
                        () => widget.depo.ekle(
                          istek.baslik,
                          oncelik: istek.oncelik,
                          sonTarih: istek.sonTarih,
                          etiketler: istek.etiketler.toSet(),
                          // IS-EMRI-o85-A B3: gorev AKTIF listede dogar --
                          // `etkinSecim` (C4) TEK KAYNAKTIR, silinmis bir
                          // listeye yazilmaz.
                          projeId: etkinSecim,
                        ),
                      ),
                    );
                  },
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}

/// IS-EMRI-o85-A D2: liste yarat/yeniden adlandir icin TEK diyalog --
/// `_GorevDuzenleDiyalogu`nun (gorev_satiri.dart) controller-dispose
/// deseninin AYNISI (o68'de olculen dispose yarisina karsi ayni koruma).
class _ListeAdiDiyalogu extends StatefulWidget {
  final String baslik;
  final String baslangicAdi;

  const _ListeAdiDiyalogu({required this.baslik, this.baslangicAdi = ''});

  @override
  State<_ListeAdiDiyalogu> createState() => _ListeAdiDiyaloguState();
}

class _ListeAdiDiyaloguState extends State<_ListeAdiDiyalogu> {
  late final TextEditingController _denetleyici;

  @override
  void initState() {
    super.initState();
    _denetleyici = TextEditingController(text: widget.baslangicAdi);
  }

  @override
  void dispose() {
    _denetleyici.dispose();
    super.dispose();
  }

  // Bos ad kaydedilmez -- diyalog ACIK KALIR (gorevBasligiDogrula'nin bos
  // girdide diyalogu acik birakan AYNI deseni, gorev_ekle_alani.dart).
  void _kaydet() {
    final ad = _denetleyici.text.trim();
    if (ad.isEmpty) return;
    Navigator.of(context).pop(ad);
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(widget.baslik, overflow: TextOverflow.ellipsis, maxLines: 1),
      content: TextField(
        key: const ValueKey('liste_adi_alani'),
        controller: _denetleyici,
        autofocus: true,
        onSubmitted: (_) => _kaydet(),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: Text(
            Metinler.iptalDugmesi,
            overflow: TextOverflow.ellipsis,
            maxLines: 1,
          ),
        ),
        TextButton(
          onPressed: _kaydet,
          child: Text(
            Metinler.kaydetDugmesi,
            overflow: TextOverflow.ellipsis,
            maxLines: 1,
          ),
        ),
      ],
    );
  }
}

/// IS-EMRI-o86-B §C PAZARLIKSIZ akis (sira dahil, adim numaralari yorumlarda):
/// `_ListeAdiDiyalogu`dan FARKLI olarak bu diyalog TEK bir deger DONUP
/// KAPANMAZ -- lookup/op/tur/kuyruk-okuma ARASI durumlar (yukleniyor, red,
/// yeniden-dene) diyalog ACIK KALIRKEN gosterilir; yalniz GERCEK basari
/// (§C/6 "satir YOK") diyalogu KAPATIR ve davet edilen e-postayi DONDURUR
/// (parent -- `_listePaylasDiyaloguAc` -- SnackBar'i ORADA gosterir).
class _ListePaylasDiyalogu extends StatefulWidget {
  final String projeId;
  final String actorId;
  final KullaniciAramaAgi kullaniciAramaAgi;
  final Future<String> Function(String projeId, String userId) uyeEkle;
  final Future<void> Function()? turCalistir;
  final Future<({String durum, String? sonHataKodu})?> Function(String opId)
  kuyrukSatiriniOku;

  const _ListePaylasDiyalogu({
    required this.projeId,
    required this.actorId,
    required this.kullaniciAramaAgi,
    required this.uyeEkle,
    required this.turCalistir,
    required this.kuyrukSatiriniOku,
  });

  @override
  State<_ListePaylasDiyalogu> createState() => _ListePaylasDiyaloguState();
}

class _ListePaylasDiyaloguState extends State<_ListePaylasDiyalogu> {
  late final TextEditingController _denetleyici;
  bool _gonderiliyor = false;
  String? _hataMetni;
  Color? _hataRengi;

  @override
  void initState() {
    super.initState();
    _denetleyici = TextEditingController();
  }

  @override
  void dispose() {
    _denetleyici.dispose();
    super.dispose();
  }

  bool _epostaGecerliMi(String eposta) =>
      eposta.isNotEmpty && eposta.contains('@');

  Future<void> _davetEt() async {
    // §C adim 1: yerel dogrulama -- AG BURADA HIC CAGRILMAZ.
    final eposta = _denetleyici.text.trim();
    if (!_epostaGecerliMi(eposta)) {
      setState(() {
        _hataMetni = Metinler.listePaylasGecersizEposta;
        _hataRengi = MRenk.tehlike(context);
      });
      return;
    }

    setState(() {
      _gonderiliyor = true;
      _hataMetni = null;
    });

    // §C adim 2: lookup.
    final sonuc = await widget.kullaniciAramaAgi.ara(eposta);
    if (!mounted) return;
    switch (sonuc) {
      case KullaniciBulunamadi():
        setState(() {
          _gonderiliyor = false;
          _hataMetni = Metinler.listePaylasKullaniciYok;
          _hataRengi = MRenk.tehlike(context);
        });
        return;
      case KullaniciAramaHatasi():
        setState(() {
          _gonderiliyor = false;
          _hataMetni = Metinler.listePaylasBaglantiYok;
          _hataRengi = MRenk.tehlike(context);
        });
        return;
      case KullaniciBulundu(userId: final userId):
        // §C adim 3: kendi kendini davet.
        if (userId == widget.actorId) {
          setState(() {
            _gonderiliyor = false;
            _hataMetni = Metinler.listePaylasKendiniDavetEdemezsin;
            _hataRengi = MRenk.tehlike(context);
          });
          return;
        }
        await _opUretVeSonucuIsle(userId, eposta);
    }
  }

  /// §C adim 4-6: op uretimi + itme turu + kuyruk satiri okuma -- UC dalli
  /// karar (satir YOK/`zehirli`/`bekliyor`).
  Future<void> _opUretVeSonucuIsle(String userId, String eposta) async {
    // adim 4: operationId SAKLANIR.
    final opId = await widget.uyeEkle(widget.projeId, userId);
    if (!mounted) return;

    // adim 5.
    final tur = widget.turCalistir;
    if (tur != null) await tur();
    if (!mounted) return;

    // adim 6: kuyruk satiri AYNI opId ile okunur.
    final satir = await widget.kuyrukSatiriniOku(opId);
    if (!mounted) return;

    if (satir == null) {
      // ✅ basari -- diyalog KAPANIR, e-posta parent'a doner.
      Navigator.of(context).pop(eposta);
      return;
    }
    if (satir.durum == 'zehirli') {
      // D-C1 PAZARLIKSIZ: ham sonHataKodu METINDE GORUNUR.
      setState(() {
        _gonderiliyor = false;
        _hataMetni = Metinler.listePaylasYetkiYok(satir.sonHataKodu ?? '?');
        _hataRengi = MRenk.tehlike(context);
      });
      return;
    }
    // durum 'bekliyor'/'gonderildi' -- op kuyrukta KALIR, silinmez.
    setState(() {
      _gonderiliyor = false;
      _hataMetni = Metinler.listePaylasYenidenDene;
      _hataRengi = MRenk.uyari(context);
    });
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(
        Metinler.listePaylasDugmesi,
        overflow: TextOverflow.ellipsis,
        maxLines: 1,
      ),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          TextField(
            key: const ValueKey('liste_paylas_eposta_alani'),
            controller: _denetleyici,
            enabled: !_gonderiliyor,
            autofocus: true,
            keyboardType: TextInputType.emailAddress,
            decoration: InputDecoration(labelText: Metinler.epostaEtiketi),
            onSubmitted: (_) => _davetEt(),
          ),
          if (_hataMetni != null) ...[
            SizedBox(height: MBosluk.xs),
            Text(
              _hataMetni!,
              key: const ValueKey('liste_paylas_hata_metni'),
              style: TextStyle(color: _hataRengi),
              overflow: TextOverflow.ellipsis,
              maxLines: 2,
            ),
          ],
        ],
      ),
      actions: [
        TextButton(
          onPressed: _gonderiliyor
              ? null
              : () => Navigator.of(context).pop(),
          child: Text(
            Metinler.iptalDugmesi,
            overflow: TextOverflow.ellipsis,
            maxLines: 1,
          ),
        ),
        TextButton(
          key: const ValueKey('liste_paylas_davet_dugmesi'),
          onPressed: _gonderiliyor ? null : _davetEt,
          child: _gonderiliyor
              ? SizedBox(
                  width: MOlcu.ikon,
                  height: MOlcu.ikon,
                  child: const CircularProgressIndicator(strokeWidth: 2),
                )
              : Text(
                  Metinler.listePaylasDavetDugmesi,
                  overflow: TextOverflow.ellipsis,
                  maxLines: 1,
                ),
        ),
      ],
    );
  }
}

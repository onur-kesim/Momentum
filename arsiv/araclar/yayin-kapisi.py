# -*- coding: utf-8 -*-
"""GOREV-W3b T4 -- yayina alma kapisi.

Dort statik kapi TEK betikte kosar:
  G48 -- appsettings.json (Istemci.KokDizin)
  G49 -- .gitignore (wwwroot tam yolu)
  G50 -- wwwroot build ciktisi butunlugu
  G51 -- bayrak izi (useLocalCanvasKit / canvasKitBaseUrl / gstatic)

CIKIS KODU SOZLESMESI (spec §5, PAZARLIKSIZ):
  0 = YESIL · 1 = SARI (yalniz G51/b2 ve/veya G51/c sapmasi) · 2 = KIRMIZI (bulgu) ·
  3 = ORTAM HATASI / OLCULEMEDI. Birden fazla sinif olusursa EN YUKSEK kod doner (3>2>1>0).
  OLCULEMEDI asla EXIT 0 vermez (W3b/G51/e).
"""
import json
import hashlib
import os
import re
import shutil
import sys
import tempfile
from datetime import datetime

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

KOK = r"C:\dev\Momentum"
PIN_VARSAYILAN = os.path.join(KOK, "araclar", "yayin-kapisi-pin.json")

CANVASKITBASEURL_ATAMA = re.compile(
    r'canvasKitBaseUrl["\']?\s*[:=]\s*["\'](?:https?:)?//[^"\']*["\']'
)
# 🔴 OLCULDU (gercek --release build, minifikasyon): anahtar bazen TIRNAKLI
# gecer -- `"useLocalCanvasKit":true` (JSON.stringify benzeri govde), bazen
# tirnaksiz -- `useLocalCanvasKit:true` (nesne literali). Ikisi de PAZARLIKSIZ
# olcut olan ATAMAdir; opsiyonel tirnak KARAKTERI regex'e eklendi.
USELOCALCANVASKIT_TRUE = re.compile(r'useLocalCanvasKit["\']?\s*[:=]\s*true\b')
GSTATIC_DIZGE = "www.gstatic.com/flutter-canvaskit"


def kaynak_sha_hesapla(depo_kok):
    """D-W3b-4 -- kaynakSha URETIM KURALININ TANIMI.

    🔴 BU FONKSIYON `araclar/web-yayina-al.py`'DEKI `kaynak_sha_hesapla` ILE
    BAYT-BAYT AYNI OLMAK ZORUNDADIR. Degisirse IKISI BIRDEN degistirilir.
    """
    dosyalar = []
    for taban in ("src/client/lib", "src/client/web"):
        tam_taban = os.path.join(depo_kok, *taban.split("/"))
        if not os.path.isdir(tam_taban):
            continue
        for kok, dizinler, dosya_adlari in os.walk(tam_taban):
            dizinler[:] = sorted(
                d
                for d in dizinler
                if not d.startswith(".") and d not in ("build", ".dart_tool")
            )
            for ad in sorted(dosya_adlari):
                if ad.startswith("."):
                    continue
                tam_yol = os.path.join(kok, ad)
                goreli = os.path.relpath(tam_yol, depo_kok).replace(os.sep, "/")
                dosyalar.append(goreli)
    for tekil in ("src/client/pubspec.yaml", "src/client/pubspec.lock"):
        if os.path.isfile(os.path.join(depo_kok, *tekil.split("/"))):
            dosyalar.append(tekil)
    dosyalar = sorted(set(dosyalar))

    toplam = hashlib.sha256()
    for goreli in dosyalar:
        tam_yol = os.path.join(depo_kok, *goreli.split("/"))
        with open(tam_yol, "rb") as f:
            icerik = f.read()
        tekil_hash = hashlib.sha256(goreli.encode("utf-8") + b"\n" + icerik).digest()
        toplam.update(tekil_hash)
    return toplam.hexdigest(), len(dosyalar)


# ============================ G48 -- appsettings.json ==========================

def g48_appsettings(yol):
    if not os.path.isfile(yol):
        return [], [], "appsettings.json bulunamadi: %s" % yol
    try:
        with open(yol, "r", encoding="utf-8") as f:
            veri = json.load(f)
    except Exception as ex:
        return [], [], "appsettings.json JSON olarak ayristirilamadi: %r" % ex
    if not isinstance(veri, dict) or "Logging" not in veri:
        return [], [], "pozitif kontrol dustu -- 'Logging' anahtari yok (G48/c)"

    bulgular = []
    istemci = veri.get("Istemci")
    if not isinstance(istemci, dict) or "KokDizin" not in istemci:
        bulgular.append(("G48/a", "KIRMIZI", "Istemci.KokDizin anahtari YOK"))
    else:
        deger = istemci.get("KokDizin")
        if not deger or deger != "wwwroot":
            bulgular.append(
                ("G48/b", "KIRMIZI", "KokDizin degeri 'wwwroot' DEGIL: %r" % deger)
            )
    return bulgular, [], None


# ============================ G49 -- .gitignore =================================

def g49_gitignore(yol):
    if not os.path.isfile(yol):
        return [], [], ".gitignore bulunamadi: %s" % yol
    with open(yol, "r", encoding="utf-8") as f:
        satirlar = f.read().splitlines()
    if not any(s.strip() == "bin/" for s in satirlar):
        return [], [], "pozitif kontrol dustu -- 'bin/' satiri yok (G49/b)"

    bulgular = []
    tam_yol_var = any(
        s.strip() == "src/backend/Momentum.Api/wwwroot/" for s in satirlar
    )
    if not tam_yol_var:
        bulgular.append(("G49/a", "KIRMIZI", "tam yol satiri YOK"))
    ciplak_var = any(s.strip() in ("wwwroot", "wwwroot/") for s in satirlar)
    if ciplak_var:
        bulgular.append(("G49/c", "KIRMIZI", "ciplak 'wwwroot' deseni VAR"))
    return bulgular, [], None


# ============================ G50 -- wwwroot butunlugu ==========================

def g50_wwwroot(wwwroot_yol, depo_kok):
    if not os.path.isdir(wwwroot_yol):
        return [], [], "wwwroot YOK: %s (G50/f)" % wwwroot_yol

    bulgular = []
    notlar = []

    # a)
    index_yol = os.path.join(wwwroot_yol, "index.html")
    if not os.path.isfile(index_yol):
        bulgular.append(("G50/a", "KIRMIZI", "index.html YOK"))
    else:
        with open(index_yol, "r", encoding="utf-8", errors="replace") as f:
            icerik = f.read()
        if "flutter_bootstrap.js" not in icerik:
            bulgular.append(
                ("G50/a", "KIRMIZI", "index.html icinde flutter_bootstrap.js referansi YOK")
            )

    # b)
    fb_yol = os.path.join(wwwroot_yol, "flutter_bootstrap.js")
    b_kirmizi = False
    if not os.path.isfile(fb_yol) or os.path.getsize(fb_yol) == 0:
        bulgular.append(("G50/b", "KIRMIZI", "flutter_bootstrap.js YOK ya da BOS"))
        b_kirmizi = True
    else:
        with open(fb_yol, "r", encoding="utf-8", errors="replace") as f:
            fb_icerik = f.read()
        if "_flutter" not in fb_icerik:
            bulgular.append(
                ("G50/b", "KIRMIZI", "flutter_bootstrap.js icinde '_flutter' dizgesi YOK")
            )
            b_kirmizi = True

    # c)
    ck_dir = os.path.join(wwwroot_yol, "canvaskit")
    if not os.path.isdir(ck_dir) or not any(
        ad.endswith(".wasm") for ad in os.listdir(ck_dir)
    ):
        bulgular.append(("G50/c", "KIRMIZI", "canvaskit/ yok ya da icinde .wasm yok"))

    # d)
    build_json_yol = os.path.join(wwwroot_yol, "_BUILD.json")
    if not os.path.isfile(build_json_yol):
        bulgular.append(("G50/d", "KIRMIZI", "_BUILD.json YOK"))
    else:
        bj = None
        try:
            with open(build_json_yol, "r", encoding="utf-8") as f:
                bj = json.load(f)
        except Exception as ex:
            bulgular.append(("G50/d", "KIRMIZI", "_BUILD.json JSON degil: %r" % ex))
        if bj is not None:
            if "kaynakSha" not in bj:
                bulgular.append(("G50/d", "KIRMIZI", "_BUILD.json'da kaynakSha alani YOK"))
            else:
                beklenen_sha, _n = kaynak_sha_hesapla(depo_kok)
                if bj.get("kaynakSha") != beklenen_sha:
                    bulgular.append(
                        (
                            "G50/d",
                            "KIRMIZI",
                            "kaynakSha UYUSMUYOR (beklenen=%s, dosyada=%s)"
                            % (beklenen_sha, bj.get("kaynakSha")),
                        )
                    )
            zaman = bj.get("zaman")
            try:
                datetime.fromisoformat(str(zaman).replace("Z", "+00:00"))
            except Exception:
                bulgular.append(
                    ("G50/d", "KIRMIZI", "'zaman' ISO 8601 olarak ayristirilamiyor: %r" % zaman)
                )

    # e)
    on10022 = []
    for kok, _dirs, dosya_adlari in os.walk(wwwroot_yol):
        for ad in dosya_adlari:
            if ad.lower().endswith((".js", ".mjs", ".html", ".json")):
                tam = os.path.join(kok, ad)
                try:
                    with open(tam, "r", encoding="utf-8", errors="replace") as f:
                        icerik = f.read()
                except Exception:
                    continue
                if "10.0.2.2" in icerik:
                    on10022.append(os.path.relpath(tam, wwwroot_yol))
    if on10022:
        bulgular.append(("G50/e", "KIRMIZI", "10.0.2.2 bulundu: %s" % ", ".join(on10022)))
    elif b_kirmizi:
        notlar.append(
            "G50/e temiz ama G50/b KIRMIZI oldugu icin bu yesil HUKUMSUZDUR (spec §5/G50/e)"
        )

    return bulgular, notlar, None


# ============================ G51 -- bayrak izi ==================================

def g51_bayrak_izi(wwwroot_yol, pin_yolu=PIN_VARSAYILAN):
    fb_yol = os.path.join(wwwroot_yol, "flutter_bootstrap.js")
    fjs_yol = os.path.join(wwwroot_yol, "flutter.js")

    if not os.path.isfile(fb_yol):
        return [], [], "flutter_bootstrap.js YOK (G51/d)"
    with open(fb_yol, "r", encoding="utf-8", errors="replace") as f:
        fb_icerik = f.read()
    if not fb_icerik.strip() or "_flutter" not in fb_icerik:
        return [], [], "pozitif kontrol dustu -- flutter_bootstrap.js bos ya da '_flutter' yok (G51/d)"

    bulgular = []
    notlar = []

    # a)
    if not USELOCALCANVASKIT_TRUE.search(fb_icerik):
        bulgular.append(("G51/a", "KIRMIZI", "useLocalCanvasKit true ATANMAMIS"))

    # b) -- salt okuma (uclu islec) ISIRMAZ, yalniz ATAMA isirir.
    if CANVASKITBASEURL_ATAMA.search(fb_icerik):
        bulgular.append(("G51/b", "KIRMIZI", "canvasKitBaseUrl CAPRAZ-KOKENE ATANMIS"))

    # b2) taban pini -- ilk kosumda YAZILIR, sapma SARI verir.
    gecis_sayisi = fb_icerik.count("canvasKitBaseUrl")
    pin = None
    if os.path.isfile(pin_yolu):
        try:
            with open(pin_yolu, "r", encoding="utf-8") as f:
                pin = json.load(f).get("canvasKitBaseUrlGecisSayisi")
        except Exception:
            pin = None
    if pin is None:
        with open(pin_yolu, "w", encoding="utf-8") as f:
            json.dump({"canvasKitBaseUrlGecisSayisi": gecis_sayisi}, f)
        notlar.append("G51/b2 TABAN PINI ILK KEZ YAZILDI: %d" % gecis_sayisi)
    elif gecis_sayisi != pin:
        bulgular.append(
            (
                "G51/b2",
                "SARI",
                "canvasKitBaseUrl gecis sayisi pinden SAPTI: pin=%d, olculen=%d"
                % (pin, gecis_sayisi),
            )
        )

    # c) gstatic -- iki dosya BIRLIKTE sayilir, KIRMIZI vermez, SARI verir.
    gstatic_toplam = fb_icerik.count(GSTATIC_DIZGE)
    if os.path.isfile(fjs_yol):
        with open(fjs_yol, "r", encoding="utf-8", errors="replace") as f:
            fjs_icerik = f.read()
        gstatic_toplam += fjs_icerik.count(GSTATIC_DIZGE)
    else:
        notlar.append("flutter.js bulunamadi -- gstatic sayimi yalniz flutter_bootstrap.js'ten")
    if gstatic_toplam != 2:
        bulgular.append(("G51/c", "SARI", "gstatic gecis sayisi 2 DEGIL: %d" % gstatic_toplam))

    return bulgular, notlar, None


# ============================ ORKESTRASYON =======================================

def tum_kapilari_kos(depo_kok=None, appsettings_yol=None, gitignore_yol=None,
                      wwwroot_yol=None, pin_yolu=None):
    depo_kok = depo_kok or KOK
    appsettings_yol = appsettings_yol or os.path.join(
        depo_kok, "src", "backend", "Momentum.Api", "appsettings.json"
    )
    gitignore_yol = gitignore_yol or os.path.join(depo_kok, ".gitignore")
    wwwroot_yol = wwwroot_yol or os.path.join(
        depo_kok, "src", "backend", "Momentum.Api", "wwwroot"
    )
    pin_yolu = pin_yolu or os.path.join(depo_kok, "araclar", "yayin-kapisi-pin.json")

    tum_bulgular = []
    tum_notlar = []
    tum_ortam_hatalari = []

    for fn, args in (
        (g48_appsettings, (appsettings_yol,)),
        (g49_gitignore, (gitignore_yol,)),
        (g50_wwwroot, (wwwroot_yol, depo_kok)),
        (g51_bayrak_izi, (wwwroot_yol, pin_yolu)),
    ):
        b, n, h = fn(*args)
        tum_bulgular += b
        tum_notlar += n
        if h:
            tum_ortam_hatalari.append("%s: %s" % (fn.__name__, h))

    return tum_bulgular, tum_notlar, tum_ortam_hatalari


def cikis_kodu_hesapla(bulgular, ortam_hatalari):
    if ortam_hatalari:
        return 3
    if any(etiket == "KIRMIZI" for _a, etiket, _m in bulgular):
        return 2
    if any(etiket == "SARI" for _a, etiket, _m in bulgular):
        return 1
    return 0


def rapor_yazdir(bulgular, notlar, ortam_hatalari):
    print("=" * 74)
    print("YAYIN KAPISI -- W3b/G48-G51")
    print("=" * 74)
    if not bulgular and not ortam_hatalari:
        print("bulgu yok.")
    for ayak, etiket, mesaj in bulgular:
        print("  [%s] %s: %s" % (etiket, ayak, mesaj))
    for hata in ortam_hatalari:
        print("  [ORTAM HATASI] %s" % hata)
    for not_ in notlar:
        print("  [NOT] %s" % not_)
    print("-" * 74)


ETIKETLER = {0: "YESIL", 1: "SARI", 2: "KIRMIZI", 3: "ORTAM HATASI / OLCULEMEDI"}


# ============================ ALTIN KUME =========================================

def _temiz_appsettings_yaz(yol):
    with open(yol, "w", encoding="utf-8") as f:
        json.dump(
            {
                "Logging": {"LogLevel": {"Default": "Information"}},
                "AllowedHosts": "*",
                "Istemci": {"KokDizin": "wwwroot"},
            },
            f,
        )


def _temiz_gitignore_yaz(yol):
    with open(yol, "w", encoding="utf-8") as f:
        f.write("bin/\nobj/\nsrc/backend/Momentum.Api/wwwroot/\n")


def _fb_icerik_uret(useLocalCanvasKit_true=True, canvaskitbaseurl_atanmis=None,
                     gstatic_gecis_fb=1):
    ulck = "true" if useLocalCanvasKit_true else "false"
    if canvaskitbaseurl_atanmis:
        cku = 'canvasKitBaseUrl:%s' % canvaskitbaseurl_atanmis
    else:
        cku = (
            'canvasKitBaseUrl:i.canvasKitBaseUrl ? i.canvasKitBaseUrl : '
            '(!e.useLocalCanvasKit ? "https://www.gstatic.com/flutter-canvaskit/x/" : null)'
        )
    gstatic_ekstra = ""
    if gstatic_gecis_fb > 1:
        gstatic_ekstra = " /* %s */" % (GSTATIC_DIZGE * (gstatic_gecis_fb - 1))
    return '_flutter.buildConfig = {useLocalCanvasKit:%s,%s};%s' % (ulck, cku, gstatic_ekstra)


def _temiz_wwwroot_kur(taban, depo_kok, kaynak_sha=None, fb_icerik=None,
                        build_json_ekle=True, canvaskit_ekle=True, index_html_ekle=True,
                        flutter_js_ekle=True):
    os.makedirs(taban, exist_ok=True)
    if canvaskit_ekle:
        ck = os.path.join(taban, "canvaskit")
        os.makedirs(ck, exist_ok=True)
        with open(os.path.join(ck, "canvaskit.wasm"), "wb") as f:
            f.write(b"\x00asm-fake")
    if index_html_ekle:
        with open(os.path.join(taban, "index.html"), "w", encoding="utf-8") as f:
            f.write('<html><body><script src="flutter_bootstrap.js"></script></body></html>')
    if fb_icerik is None:
        fb_icerik = _fb_icerik_uret()
    with open(os.path.join(taban, "flutter_bootstrap.js"), "w", encoding="utf-8") as f:
        f.write(fb_icerik)
    if flutter_js_ekle:
        with open(os.path.join(taban, "flutter.js"), "w", encoding="utf-8") as f:
            f.write('// flutter.js\nvar x = "%s/y/";\n' % GSTATIC_DIZGE)
    if build_json_ekle:
        if kaynak_sha is None:
            kaynak_sha, _n = kaynak_sha_hesapla(depo_kok)
        with open(os.path.join(taban, "_BUILD.json"), "w", encoding="utf-8") as f:
            json.dump(
                {
                    "kaynakSha": kaynak_sha,
                    "zaman": "2026-08-08T12:00:00+00:00",
                    "flutterSurum": "3.44.6",
                    "bayraklar": ["--release", "--no-web-resources-cdn"],
                },
                f,
            )


def _sahte_depo_kur(kok):
    """kaynak_sha_hesapla'nin gercekten calisabilecegi minimal bir depo agaci."""
    lib = os.path.join(kok, "src", "client", "lib")
    web = os.path.join(kok, "src", "client", "web")
    os.makedirs(lib, exist_ok=True)
    os.makedirs(web, exist_ok=True)
    with open(os.path.join(lib, "main.dart"), "w", encoding="utf-8") as f:
        f.write("void main() {}\n")
    with open(os.path.join(web, "index.html"), "w", encoding="utf-8") as f:
        f.write("<html></html>\n")
    with open(os.path.join(kok, "src", "client", "pubspec.yaml"), "w", encoding="utf-8") as f:
        f.write("name: client\n")


def altin_kume_kos():
    vakalar = []

    def vaka(no, aciklama, fn):
        try:
            ok, detay = fn()
        except Exception as ex:
            ok, detay = False, "ISTISNA: %r" % ex
        vakalar.append((no, aciklama, ok, detay))
        etiket = "GECTI" if ok else "DUSTU"
        print("[%s] %d) %s%s" % (etiket, no, aciklama, ("" if ok else " -- %s" % detay)))

    # ---- G48 ----
    def g48_temiz():
        with tempfile.TemporaryDirectory() as td:
            yol = os.path.join(td, "appsettings.json")
            _temiz_appsettings_yaz(yol)
            b, n, h = g48_appsettings(yol)
            return (not b and not h), (b, h)

    def g48_a_kirli():
        with tempfile.TemporaryDirectory() as td:
            yol = os.path.join(td, "appsettings.json")
            with open(yol, "w", encoding="utf-8") as f:
                json.dump({"Logging": {}, "AllowedHosts": "*"}, f)  # Istemci YOK (M246)
            b, n, h = g48_appsettings(yol)
            return any(a == "G48/a" for a, _e, _m in b), b

    def g48_b_kirli():
        with tempfile.TemporaryDirectory() as td:
            yol = os.path.join(td, "appsettings.json")
            with open(yol, "w", encoding="utf-8") as f:
                json.dump(
                    {"Logging": {}, "AllowedHosts": "*", "Istemci": {"KokDizin": "wwwroot2"}}, f
                )  # M247
            b, n, h = g48_appsettings(yol)
            return any(a == "G48/b" for a, _e, _m in b), b

    def g48_c_ortam_hatasi():
        with tempfile.TemporaryDirectory() as td:
            yol = os.path.join(td, "appsettings.json")
            with open(yol, "w", encoding="utf-8") as f:
                f.write("{ gecersiz json")  # M248
            b, n, h = g48_appsettings(yol)
            return (h is not None), h

    vaka(1, "G48 temiz appsettings.json -> bulgu yok", g48_temiz)
    vaka(2, "G48/a: Istemci blogu YOK (M246) -> KIRMIZI", g48_a_kirli)
    vaka(3, "G48/b: KokDizin='wwwroot2' (M247) -> KIRMIZI", g48_b_kirli)
    vaka(4, "G48: gecersiz JSON (M248) -> ORTAM HATASI", g48_c_ortam_hatasi)

    # ---- G49 ----
    def g49_temiz():
        with tempfile.TemporaryDirectory() as td:
            yol = os.path.join(td, ".gitignore")
            _temiz_gitignore_yaz(yol)
            b, n, h = g49_gitignore(yol)
            return (not b and not h), (b, h)

    def g49_ac_kirli():
        with tempfile.TemporaryDirectory() as td:
            yol = os.path.join(td, ".gitignore")
            with open(yol, "w", encoding="utf-8") as f:
                f.write("bin/\nobj/\nwwwroot\n")  # M249: tam yol -> ciplak
            b, n, h = g49_gitignore(yol)
            kodlar = {a for a, _e, _m in b}
            return ("G49/a" in kodlar and "G49/c" in kodlar), b

    def g49_b_ortam_hatasi():
        with tempfile.TemporaryDirectory() as td:
            yol = os.path.join(td, ".gitignore")
            with open(yol, "w", encoding="utf-8") as f:
                f.write("")  # M250: bosaltilir
            b, n, h = g49_gitignore(yol)
            return (h is not None), h

    vaka(5, "G49 temiz .gitignore -> bulgu yok", g49_temiz)
    vaka(6, "G49/a+c: tam yol -> ciplak wwwroot (M249) -> ikisi de KIRMIZI", g49_ac_kirli)
    vaka(7, "G49: .gitignore BOSALTILDI (M250) -> ORTAM HATASI", g49_b_ortam_hatasi)

    # ---- G50 ----
    def g50_temiz():
        with tempfile.TemporaryDirectory() as td:
            _sahte_depo_kur(td)
            wwwroot = os.path.join(td, "src", "backend", "Momentum.Api", "wwwroot")
            _temiz_wwwroot_kur(wwwroot, td)
            b, n, h = g50_wwwroot(wwwroot, td)
            return (not b and not h), (b, h)

    def g50_a_kirli():
        with tempfile.TemporaryDirectory() as td:
            _sahte_depo_kur(td)
            wwwroot = os.path.join(td, "src", "backend", "Momentum.Api", "wwwroot")
            _temiz_wwwroot_kur(wwwroot, td)
            with open(os.path.join(wwwroot, "index.html"), "w", encoding="utf-8") as f:
                f.write("<html>yer tutucu</html>")  # M251
            b, n, h = g50_wwwroot(wwwroot, td)
            return any(a == "G50/a" for a, _e, _m in b), b

    def g50_b_kirli():
        with tempfile.TemporaryDirectory() as td:
            _sahte_depo_kur(td)
            wwwroot = os.path.join(td, "src", "backend", "Momentum.Api", "wwwroot")
            _temiz_wwwroot_kur(wwwroot, td)
            with open(os.path.join(wwwroot, "flutter_bootstrap.js"), "w", encoding="utf-8") as f:
                f.write("")  # M252
            b, n, h = g50_wwwroot(wwwroot, td)
            kodlar = {a for a, _e, _m in b}
            return "G50/b" in kodlar and any("HUKUMSUZ" in x for x in n), (b, n)

    def g50_c_kirli():
        with tempfile.TemporaryDirectory() as td:
            _sahte_depo_kur(td)
            wwwroot = os.path.join(td, "src", "backend", "Momentum.Api", "wwwroot")
            _temiz_wwwroot_kur(wwwroot, td)
            shutil.move(os.path.join(wwwroot, "canvaskit"), os.path.join(wwwroot, "canvaskit-x"))  # M253
            b, n, h = g50_wwwroot(wwwroot, td)
            return any(a == "G50/c" for a, _e, _m in b), b

    def g50_d_kirli():
        with tempfile.TemporaryDirectory() as td:
            _sahte_depo_kur(td)
            wwwroot = os.path.join(td, "src", "backend", "Momentum.Api", "wwwroot")
            _temiz_wwwroot_kur(wwwroot, td)
            with open(os.path.join(wwwroot, "_BUILD.json"), "w", encoding="utf-8") as f:
                json.dump({"zaman": "2026-08-08T12:00:00+00:00"}, f)  # M254: kaynakSha silindi
            b, n, h = g50_wwwroot(wwwroot, td)
            return any(a == "G50/d" for a, _e, _m in b), b

    def g50_d_kirli_b():
        """B-W3b-4 borcu geregi ZORUNLU ek vaka: sha DOLU ama YANLIS."""
        with tempfile.TemporaryDirectory() as td:
            _sahte_depo_kur(td)
            wwwroot = os.path.join(td, "src", "backend", "Momentum.Api", "wwwroot")
            _temiz_wwwroot_kur(wwwroot, td, kaynak_sha="0" * 64)  # yanlis ama dolu sha
            b, n, h = g50_wwwroot(wwwroot, td)
            return any(a == "G50/d" for a, _e, _m in b), b

    def g50_e_kirli():
        with tempfile.TemporaryDirectory() as td:
            _sahte_depo_kur(td)
            wwwroot = os.path.join(td, "src", "backend", "Momentum.Api", "wwwroot")
            _temiz_wwwroot_kur(wwwroot, td)
            with open(os.path.join(wwwroot, "main.dart.js"), "w", encoding="utf-8") as f:
                f.write('var url = "http://10.0.2.2:5298";')  # M255
            b, n, h = g50_wwwroot(wwwroot, td)
            return any(a == "G50/e" for a, _e, _m in b), b

    def g50_f_ortam_hatasi():
        with tempfile.TemporaryDirectory() as td:
            _sahte_depo_kur(td)
            wwwroot = os.path.join(td, "src", "backend", "Momentum.Api", "wwwroot")
            # M256: wwwroot HIC olusturulmadi (yeniden-adlandirilmis gibi)
            b, n, h = g50_wwwroot(wwwroot, td)
            return (h is not None), h

    vaka(8, "G50 temiz wwwroot -> bulgu yok", g50_temiz)
    vaka(9, "G50/a: index.html yer tutucu (M251) -> KIRMIZI", g50_a_kirli)
    vaka(10, "G50/b: flutter_bootstrap.js BOS (M252) -> KIRMIZI + e hukumsuz notu", g50_b_kirli)
    vaka(11, "G50/c: canvaskit/ yeniden adlandirildi (M253) -> KIRMIZI", g50_c_kirli)
    vaka(12, "G50/d: kaynakSha SILINDI (M254) -> KIRMIZI", g50_d_kirli)
    vaka(13, "G50/d: kaynakSha DOLU ama YANLIS (B-W3b-4 ek vaka) -> KIRMIZI", g50_d_kirli_b)
    vaka(14, "G50/e: 10.0.2.2 enjekte edildi (M255) -> KIRMIZI", g50_e_kirli)
    vaka(15, "G50/f: wwwroot YOK (M256) -> ORTAM HATASI", g50_f_ortam_hatasi)

    # ---- G51 ----
    def g51_temiz():
        with tempfile.TemporaryDirectory() as td:
            wwwroot = os.path.join(td, "wwwroot")
            _temiz_wwwroot_kur(wwwroot, td, build_json_ekle=False)
            pin = os.path.join(td, "pin.json")
            b, n, h = g51_bayrak_izi(wwwroot, pin)
            # ilk kosum pini YAZAR (nota dusar), bulgu URETMEZ
            return (not b and not h), (b, h, n)

    def g51_a_kirli():
        with tempfile.TemporaryDirectory() as td:
            wwwroot = os.path.join(td, "wwwroot")
            _temiz_wwwroot_kur(
                wwwroot, td, build_json_ekle=False,
                fb_icerik=_fb_icerik_uret(useLocalCanvasKit_true=False),  # M257
            )
            pin = os.path.join(td, "pin.json")
            b, n, h = g51_bayrak_izi(wwwroot, pin)
            return any(a == "G51/a" for a, _e, _m in b), b

    def g51_b_kirli(atama):
        def _fn():
            with tempfile.TemporaryDirectory() as td:
                wwwroot = os.path.join(td, "wwwroot")
                _temiz_wwwroot_kur(
                    wwwroot, td, build_json_ekle=False,
                    fb_icerik=_fb_icerik_uret(canvaskitbaseurl_atanmis=atama),
                )
                pin = os.path.join(td, "pin.json")
                b, n, h = g51_bayrak_izi(wwwroot, pin)
                return any(a == "G51/b" for a, _e, _m in b), b
        return _fn

    def g51_c_kirli():
        with tempfile.TemporaryDirectory() as td:
            wwwroot = os.path.join(td, "wwwroot")
            _temiz_wwwroot_kur(wwwroot, td, build_json_ekle=False, flutter_js_ekle=False)  # M261: tek gecis
            pin = os.path.join(td, "pin.json")
            b, n, h = g51_bayrak_izi(wwwroot, pin)
            return any(a == "G51/c" and e == "SARI" for a, e, _m in b), b

    def g51_b2_kirli():
        with tempfile.TemporaryDirectory() as td:
            wwwroot = os.path.join(td, "wwwroot")
            _temiz_wwwroot_kur(wwwroot, td, build_json_ekle=False)
            pin = os.path.join(td, "pin.json")
            g51_bayrak_izi(wwwroot, pin)  # ilk kosum pini yazar
            # M263: bir gecis silinir -- fb icerigini AZALTILMIS dizgeyle DEGISTIR
            fb_yol = os.path.join(wwwroot, "flutter_bootstrap.js")
            with open(fb_yol, "r", encoding="utf-8") as f:
                icerik = f.read()
            # yalniz ILK "canvasKitBaseUrl" gecisini kaldirarak sayiyi 1 azalt
            icerik2 = icerik.replace("canvasKitBaseUrl", "XX", 1)
            with open(fb_yol, "w", encoding="utf-8") as f:
                f.write(icerik2)
            b, n, h = g51_bayrak_izi(wwwroot, pin)
            return any(a == "G51/b2" and e == "SARI" for a, e, _m in b), b

    def g51_d_ortam_hatasi():
        with tempfile.TemporaryDirectory() as td:
            wwwroot = os.path.join(td, "wwwroot")
            os.makedirs(wwwroot, exist_ok=True)
            pin = os.path.join(td, "pin.json")
            b, n, h = g51_bayrak_izi(wwwroot, pin)  # M262: dosya hic yok
            return (h is not None), h

    def mw23_susmali():
        with tempfile.TemporaryDirectory() as td:
            wwwroot = os.path.join(td, "wwwroot")
            fb = _fb_icerik_uret().replace(
                "canvasKitBaseUrl:i.canvasKitBaseUrl",
                "// canvasKitBaseUrl\ncanvasKitBaseUrl:i.canvasKitBaseUrl",
            )
            _temiz_wwwroot_kur(wwwroot, td, build_json_ekle=False, fb_icerik=fb)
            pin = os.path.join(td, "pin.json")
            b, n, h = g51_bayrak_izi(wwwroot, pin)
            return (not any(e == "KIRMIZI" for _a, e, _m in b)), b

    vaka(16, "G51 temiz flutter_bootstrap.js -> bulgu yok (pin ilk kez yazilir)", g51_temiz)
    vaka(17, "G51/a: useLocalCanvasKit=false (M257, cekirdek kusur) -> KIRMIZI", g51_a_kirli)
    vaka(18, "G51/b: canvasKitBaseUrl:\"https://x/\" (M258) -> KIRMIZI", g51_b_kirli('"https://x/"'))
    vaka(19, "G51/b: canvasKitBaseUrl:'https://x/' (M259, tek tirnak) -> KIRMIZI", g51_b_kirli("'https://x/'"))
    vaka(20, "G51/b: canvasKitBaseUrl:\"//x/\" (M260, protokol-goreli) -> KIRMIZI", g51_b_kirli('"//x/"'))
    vaka(21, "G51/c: gstatic gecisi 1'e dustu (M261) -> SARI", g51_c_kirli)
    vaka(22, "G51/b2: canvasKitBaseUrl gecis sayisi pinden sapti (M263) -> SARI", g51_b2_kirli)
    vaka(23, "G51/d: flutter_bootstrap.js SILINDI (M262) -> ORTAM HATASI", g51_d_ortam_hatasi)
    vaka(24, "MW23 SUSMALI: '// canvasKitBaseUrl' yorumu -> hicbir KIRMIZI olusmamali", mw23_susmali)

    # ---- MW24 (appsettings.Development.json alakasiz anahtar -- G48 kapsami DISI) ----
    def mw24_susmali():
        with tempfile.TemporaryDirectory() as td:
            yol = os.path.join(td, "appsettings.Development.json")
            with open(yol, "w", encoding="utf-8") as f:
                json.dump({"Cors": {"AllowedOrigins": []}, "AlakasizAnahtar": True}, f)
            # G48'in kapsami YALNIZ appsettings.json'dur (Development degil) -- bu dosya
            # hic taranmadigi icin dogal olarak SUSAR; MW24'un ISPATI budur.
            appsettings_yol = os.path.join(td, "appsettings.json")
            _temiz_appsettings_yaz(appsettings_yol)
            b, n, h = g48_appsettings(appsettings_yol)
            return (not b and not h), (b, h)

    vaka(25, "MW24 SUSMALI: appsettings.Development.json'a alakasiz anahtar -> kapi SUSAR", mw24_susmali)

    # ---- exit-kodu sozlesmesi + notlar duzeni ----
    def cikis_kodu_kirmizi_baskin():
        bulgular = [("G48/a", "KIRMIZI", "x"), ("G51/c", "SARI", "y")]
        return cikis_kodu_hesapla(bulgular, []) == 2, None

    def cikis_kodu_sari_yalniz():
        bulgular = [("G51/c", "SARI", "y")]
        return cikis_kodu_hesapla(bulgular, []) == 1, None

    def cikis_kodu_ortam_hatasi_baskin():
        bulgular = [("G48/a", "KIRMIZI", "x")]
        return cikis_kodu_hesapla(bulgular, ["G50: wwwroot yok"]) == 3, None

    def cikis_kodu_temiz():
        return cikis_kodu_hesapla([], []) == 0, None

    vaka(26, "cikis kodu: KIRMIZI+SARI birlikte -> 2 (KIRMIZI baskin)", cikis_kodu_kirmizi_baskin)
    vaka(27, "cikis kodu: yalniz SARI -> 1", cikis_kodu_sari_yalniz)
    vaka(28, "cikis kodu: ORTAM HATASI her zaman baskin -> 3", cikis_kodu_ortam_hatasi_baskin)
    vaka(29, "cikis kodu: bulgu yok -> 0", cikis_kodu_temiz)

    print("-" * 74)
    gecen = sum(1 for _n, _a, ok, _d in vakalar if ok)
    toplam = len(vakalar)
    print("ALTIN KUME: %d/%d GECTI" % (gecen, toplam))
    return 0 if gecen == toplam else 1


def main():
    if "--altin-kume" in sys.argv:
        return altin_kume_kos()

    kok = KOK
    for arg in sys.argv[1:]:
        if not arg.startswith("--"):
            kok = os.path.abspath(arg)

    bulgular, notlar, ortam_hatalari = tum_kapilari_kos(depo_kok=kok)
    rapor_yazdir(bulgular, notlar, ortam_hatalari)
    kod = cikis_kodu_hesapla(bulgular, ortam_hatalari)
    print("HUKUM: %s (EXIT %d)" % (ETIKETLER[kod], kod))
    return kod


if __name__ == "__main__":
    sys.exit(main())

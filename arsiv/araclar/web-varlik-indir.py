# -*- coding: utf-8 -*-
"""web-varlik-indir.py -- Drift'in web ikililerini (sqlite3.wasm, drift_worker.js)
INDIRIR ve sha256'sini araclar/web-varlik.sha256'daki PINLI degerle OLCER.

NEDEN VAR (Z11, GOREV-slice-3b T5): drift_flutter web'de calismak icin iki
ikili ister ve bunlar pub.dev paketinde DEGIL, GitHub release'inde yasar.
sqlite3.wasm paket SURUMUNU tasimaz (icindeki tek surum dizgesi SQLite C
kutuphanesinindir, "3.53.3" gibi) ⇒ "surum <= paket surumu" diye bir dogrulama
YAZILAMAZ; kimlik SADECE pinli sha256'dir. Surum karsilastirmasi YAPILMAZ.

TOFU (Trust On First Use): pinin ILK KAYNAGI ILK INDIRMEDIR. Bu bir GUVEN
VARSAYIMIDIR -- ilk pinleme aninda acikca BASILIR, gizlenmez. Sonraki her
kosum mevcut pini SESSIZCE GUNCELLEMEZ; yalnizca KARSILASTIRIR.

Kullanim:
  python araclar\\web-varlik-indir.py              -- gercek indirme + olcum
  python araclar\\web-varlik-indir.py --altin-kume  -- betigin KENDI mantigi
                                                        (agsiz, fixture ile)

Cikis: 0 = hepsi pinle eslesti (veya ilk kez TOFU ile pinlendi)
       2 = en az biri PINLE UYUSMADI (bütünlük ihlali)
       3 = indirme/ag hatasi
"""
import hashlib
import os
import sys
import urllib.request

UA = {"User-Agent": "momentum-olcum/1.0"}

KOK = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PIN_DOSYASI = os.path.join(KOK, "araclar", "web-varlik.sha256")

VARLIKLAR = [
    {
        "ad": "sqlite3.wasm",
        "url": "https://github.com/simolus3/sqlite3.dart/releases/download/sqlite3-3.5.0/sqlite3.wasm",
        "repo": "simolus3/sqlite3.dart",
        "tag": "sqlite3-3.5.0",
        "hedef": os.path.join(KOK, "src", "client", "web", "sqlite3.wasm"),
    },
    {
        "ad": "drift_worker.js",
        "url": "https://github.com/simolus3/drift/releases/download/drift-2.34.3/drift_worker.js",
        "repo": "simolus3/drift",
        "tag": "drift-2.34.3",
        "hedef": os.path.join(KOK, "src", "client", "web", "drift_worker.js"),
    },
]


def sha256_hesapla(bayt):
    return hashlib.sha256(bayt).hexdigest()


def pin_oku(yol):
    """Pin dosyasini {ad: {"sha256":..., "repo":..., "tag":...}} olarak okur."""
    pinler = {}
    if not os.path.isfile(yol):
        return pinler
    for satir in open(yol, "r", encoding="utf-8"):
        satir = satir.strip()
        if not satir or satir.startswith("#"):
            continue
        parcalar = satir.split()
        if len(parcalar) < 2:
            continue
        ad, sha = parcalar[0], parcalar[1]
        ekstra = dict(p.split("=", 1) for p in parcalar[2:] if "=" in p)
        pinler[ad] = {"sha256": sha, **ekstra}
    return pinler


def pin_yaz(yol, pinler):
    with open(yol, "w", encoding="utf-8", newline="\n") as f:
        f.write("# TOFU (Trust On First Use) -- ilk kaynak ilk indirmedir; GUVEN VARSAYIMIDIR.\n")
        f.write("# Surum karsilastirmasi YAPILMAZ (Z11) -- sqlite3.wasm paket surumunu tasimaz.\n")
        f.write("# Bu dosya web-varlik-indir.py tarafindan uretilir; ELLE DUZENLEME onerilmez.\n")
        for ad in sorted(pinler):
            p = pinler[ad]
            ekstra = " ".join(
                "%s=%s" % (k, v) for k, v in p.items() if k != "sha256"
            )
            f.write("%s %s %s\n" % (ad, p["sha256"], ekstra))


def indir(url):
    r = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(r, timeout=60) as f:
        return f.read()


def calistir(varliklar, pin_yolu, indir_fn=indir):
    pinler_oncesi = pin_oku(pin_yolu)
    pinler_sonrasi = dict(pinler_oncesi)
    hata = False
    uyusmazlik = False

    print("%-20s %-10s %-10s %s" % ("VARLIK", "BAYT", "HUKUM", "SHA256"))
    print("-" * 100)

    for v in varliklar:
        try:
            veri = indir_fn(v["url"])
        except Exception as e:
            print("%-20s %s" % (v["ad"], "INDIRME HATASI: " + str(e)))
            hata = True
            continue

        sha = sha256_hesapla(veri)
        mevcut_pin = pinler_oncesi.get(v["ad"])

        if mevcut_pin is None:
            hukum = "TOFU-PINLENDI"
            print(
                "TOFU UYARISI: '%s' icin daha once pin YOK -- bu indirme GUVEN "
                "VARSAYIMI olarak pinleniyor (kaynak: %s @ %s)."
                % (v["ad"], v["repo"], v["tag"])
            )
            pinler_sonrasi[v["ad"]] = {
                "sha256": sha,
                "repo": v["repo"],
                "tag": v["tag"],
            }
        elif mevcut_pin["sha256"] == sha:
            hukum = "ESLESTI"
        else:
            hukum = "UYUSMADI"
            uyusmazlik = True
            print(
                "BUTUNLUK IHLALI: '%s' pinli=%s indirilen=%s"
                % (v["ad"], mevcut_pin["sha256"], sha)
            )

        os.makedirs(os.path.dirname(v["hedef"]), exist_ok=True)
        with open(v["hedef"], "wb") as f:
            f.write(veri)

        print("%-20s %-10d %-10s %s" % (v["ad"], len(veri), hukum, sha))

    print("-" * 100)

    if pinler_sonrasi != pinler_oncesi:
        pin_yaz(pin_yolu, pinler_sonrasi)

    if hata:
        print("HUKUM: INDIRME HATASI")
        return 3
    if uyusmazlik:
        print("HUKUM: PIN UYUSMAZLIGI -- exit 2")
        return 2
    print("HUKUM: TUMU PINLE ESLESTI (veya ilk kez TOFU ile pinlendi)")
    return 0


# ------------------------------------------------------------------
# ALTIN KUME -- betigin KENDI karsilastirma/TOFU mantigini agsiz kanitlar
# (K44-a: arac once kendini kanitlar). Gercek indirme fonksiyonu sahte bir
# fonksiyonla degistirilir; sha256 hesaplama ve pin karsilastirma/yazma
# mantigi GERCEKTIR.
# ------------------------------------------------------------------
def altin_kume():
    import shutil
    import tempfile

    gecti = 0
    toplam = 0
    gecici = tempfile.mkdtemp(prefix="web-varlik-altin-")
    try:
        pin_yolu = os.path.join(gecici, "web-varlik.sha256")
        hedef_a = os.path.join(gecici, "a.bin")
        hedef_b = os.path.join(gecici, "b.bin")

        icerik_v1 = b"surum-1-icerigi"
        icerik_v2 = b"surum-2-FARKLI-icerik"

        def sahte_indir_v1(url):
            return icerik_v1

        def sahte_indir_v2(url):
            return icerik_v2

        varlik = [
            {
                "ad": "ornek.bin",
                "url": "https://ornek.invalid/ornek.bin",
                "repo": "ornek/repo",
                "tag": "v1",
                "hedef": hedef_a,
            }
        ]

        # 1) ILK KOSUM -- pin YOK -- TOFU ile pinlenmeli, exit 0
        toplam += 1
        cikis = calistir(varlik, pin_yolu, indir_fn=sahte_indir_v1)
        pinler = pin_oku(pin_yolu)
        if cikis == 0 and "ornek.bin" in pinler:
            print("[GECTI] 1) ilk kosum TOFU ile pinledi, exit 0")
            gecti += 1
        else:
            print("[KALDI] 1) ilk kosum TOFU basarisiz, exit=%d" % cikis)

        # 2) AYNI ICERIK TEKRAR -- pin VAR ve eslesiyor -- exit 0, pin DEGISMEMELI
        toplam += 1
        pin_once = pin_oku(pin_yolu)
        cikis = calistir(varlik, pin_yolu, indir_fn=sahte_indir_v1)
        pin_sonra = pin_oku(pin_yolu)
        if cikis == 0 and pin_once == pin_sonra:
            print("[GECTI] 2) eslesen icerik exit 0, pin degismedi")
            gecti += 1
        else:
            print("[KALDI] 2) eslesen icerik basarisiz, exit=%d" % cikis)

        # 3) FARKLI ICERIK (surum degisti gibi) -- pinle UYUSMAMALI -- exit 2
        toplam += 1
        cikis = calistir(varlik, pin_yolu, indir_fn=sahte_indir_v2)
        if cikis == 2:
            print("[GECTI] 3) uyusmayan icerik exit 2 verdi (butunluk ihlali yakalandi)")
            gecti += 1
        else:
            print("[KALDI] 3) uyusmayan icerik yakalanmadi, exit=%d" % cikis)

        # 4) INDIRME HATASI -- exit 3
        toplam += 1

        def patlayan_indir(url):
            raise RuntimeError("ag yok (fixture)")

        cikis = calistir(varlik, os.path.join(gecici, "yok.sha256"), indir_fn=patlayan_indir)
        if cikis == 3:
            print("[GECTI] 4) indirme hatasi exit 3 verdi")
            gecti += 1
        else:
            print("[KALDI] 4) indirme hatasi yakalanmadi, exit=%d" % cikis)

    finally:
        shutil.rmtree(gecici, ignore_errors=True)

    print("=" * 60)
    print("ALTIN KUME: %d/%d GECTI" % (gecti, toplam))
    return 0 if gecti == toplam else 1


def main(argv):
    if "--altin-kume" in argv:
        return altin_kume()
    return calistir(VARLIKLAR, PIN_DOSYASI)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

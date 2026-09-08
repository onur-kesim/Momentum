# -*- coding: utf-8 -*-
"""M-2 -- KOD SAGLIGI KAPISI (satir ekseni).

Kilit [Onur, 8 Eyl 2026]: bu dilim YALNIZ satir eksenini kapatir. Karmasiklik
(fonksiyon uzunlugu/dallanma) ayri bir dilimdir, ayri bir kilitle acilir --
Dart/C# icin `ast` tabanli bir dis tanik (hafiza-kur/faz0/karmasiklik.py'nin
Python'a ozgu kalibi) yok; onsuz bir sezgisel yazip CC demek olcut degildir.

OLCUM KURALI (git ls-files, taban `1f602c6` sonrasi agacta olculdu):
  kapsam   = git ls-files -- '*.dart' '*.cs'
  haric    = *.g.dart, *.freezed.dart, *Designer.cs, *ModelSnapshot.cs,
             yol parcasi 'Migrations' olan her sey, `arsiv/` alti, yol
             parcasi 'test' veya 'tests' olan her sey (testler OLCULUR ama
             kapiyi YAKMAZ -- İŞLEYİŞ md.3).
  satir    = sum(1 for _ in open(yol, encoding="utf-8")) -- PowerShell
             `Measure-Object -Line` BOS SATIRLARI SAYMAZ, bu yuzden dedigi
             degil PYTHON'un saydigi esas alinir (8 Eyl olcum kusuru, %8 sapma).

TABAN SOZLESMESI (`kod_sagligi_taban.json`): SAYI degil KUME dondurulur --
tek sayi delik birakir (bir 1135'lik dosya ikiye bolunup baska biri esigin
altina inse sayi sabit kalir, kapi kordur). Uc kirmizi kural (hicbiri digerini
gereksiz kilmaz, hepsi bagimsiz kosulur):
  1) tabanda OLMAYAN bir dosya esigi asarsa           -> KIRMIZI
  2) ihlal SAYISI taban sayisini asarsa                -> KIRMIZI (ikinci savunma hatti)
  3) tabandaki bir dosya esigin ALTINA inerse/kaybolursa -> KIRMIZI (cirCir
     ileri gider, geri donmez -- tabana yazilacak tam JSON satiri mesajda verilir)

CIKIS KODU SOZLESMESI (karmasiklik.py'nin sozlesmesi birebir korunur):
  0 = gecti (ya da RAPOR kipi -- --kapi verilmezse olcum her zaman 0 doner)
  1 = ihlal (YALNIZ --kapi ile)
  2 = OLCULEMEDI (git yok, dosya okunamadi, taban yok/bozuk) -- ASLA "temiz"
      demek DEGILDIR, --kapi olsun olmasin sabittir.
"""
import argparse
import json
import os
import subprocess
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
TABAN_VARSAYILAN = os.path.join(SCRIPT_DIR, "kod_sagligi_taban.json")

UZANTILAR = ("*.dart", "*.cs")
URETILEN_SONEKLER = (".g.dart", ".freezed.dart", "Designer.cs", "ModelSnapshot.cs")


class OlculemediHatasi(Exception):
    """git/dosya/taban okunamadi -- exit 2 (KIRMIZI/ihlal DEGIL)."""


def haric_mi(goreli_yol):
    if goreli_yol.startswith("arsiv/"):
        return True
    if any(goreli_yol.endswith(sonek) for sonek in URETILEN_SONEKLER):
        return True
    parcalar = goreli_yol.split("/")
    if "Migrations" in parcalar:
        return True
    if "test" in parcalar or "tests" in parcalar:
        return True
    return False


def git_dosyalarini_getir(kok):
    try:
        sonuc = subprocess.run(
            ["git", "-C", kok, "ls-files", "--"] + list(UZANTILAR),
            capture_output=True,
            text=True,
            encoding="utf-8",
        )
    except FileNotFoundError:
        raise OlculemediHatasi("git bulunamadi (PATH'te yok)")
    if sonuc.returncode != 0:
        raise OlculemediHatasi(
            f"git ls-files basarisiz (kok={kok}): {sonuc.stderr.strip() or sonuc.returncode}"
        )
    return sorted(s for s in sonuc.stdout.splitlines() if s and not haric_mi(s))


def satir_say(kok, goreli_yol):
    tam_yol = os.path.join(kok, *goreli_yol.split("/"))
    try:
        with open(tam_yol, encoding="utf-8") as f:
            return sum(1 for _ in f)
    except (OSError, UnicodeDecodeError) as exc:
        raise OlculemediHatasi(f"dosya okunamadi: {goreli_yol} ({exc})")


def taban_yukle(taban_yolu):
    if not os.path.isfile(taban_yolu):
        raise OlculemediHatasi(f"taban dosyasi yok: {taban_yolu}")
    try:
        with open(taban_yolu, encoding="utf-8") as f:
            veri = json.load(f)
    except (OSError, json.JSONDecodeError) as exc:
        raise OlculemediHatasi(f"taban dosyasi bozuk: {taban_yolu} ({exc})")
    esik = veri.get("esik_satir")
    dosyalar = veri.get("dosyalar")
    if not isinstance(esik, int) or esik <= 0:
        raise OlculemediHatasi(f"taban dosyasi bozuk: esik_satir gecersiz ({taban_yolu})")
    if not isinstance(dosyalar, dict):
        raise OlculemediHatasi(f"taban dosyasi bozuk: dosyalar gecersiz ({taban_yolu})")
    return veri


def olc(kok, taban_yolu):
    """Tam olcumu yapar, kirmizi kurallari degerlendirir. Hata -> OlculemediHatasi."""
    taban = taban_yukle(taban_yolu)
    esik = taban["esik_satir"]
    taban_dosyalar = taban["dosyalar"]

    kapsam = git_dosyalarini_getir(kok)
    satirlar = {yol: satir_say(kok, yol) for yol in kapsam}
    ihlaller = {yol: n for yol, n in satirlar.items() if n > esik}

    kirmizilar = []

    # Kural 1: tabanda olmayan bir dosya esigi asiyor.
    for yol, n in sorted(ihlaller.items()):
        if yol not in taban_dosyalar:
            kirmizilar.append(
                f'YENI IHLAL: "{yol}" {n} satir (>{esik}), tabanda YOK -- '
                f'kod_sagligi_taban.json "dosyalar" kumesine eklenmeden gecemez.'
            )

    # Kural 2: ihlal sayisi taban sayisini asiyor (ikinci savunma hatti).
    if len(ihlaller) > len(taban_dosyalar):
        kirmizilar.append(
            f"IHLAL SAYISI ARTTI: simdi {len(ihlaller)} dosya >{esik} satir, "
            f"taban {len(taban_dosyalar)} dosya taniyordu."
        )

    # Kural 3: tabandaki bir dosya esigin altina indi (veya kayboldu) -- cirCir.
    for yol, taban_n in sorted(taban_dosyalar.items()):
        simdiki_n = satirlar.get(yol)
        if simdiki_n is None:
            kirmizilar.append(
                f'TABAN DOSYASI KAYIP: "{yol}" (taban {taban_n} satir) artik kapsamda yok '
                f"(silindi/tasindi). kod_sagligi_taban.json \"dosyalar\" kumesinden SU SATIRI SIL:\n"
                f'      "{yol}": {taban_n},'
            )
        elif simdiki_n <= esik:
            kirmizilar.append(
                f'TABAN GERI ADIM: "{yol}" artik {simdiki_n} satir (<= {esik}), taban {taban_n} '
                f"satir biliyordu. Iyilesme -- cirCir GERI DONMEZ, kod_sagligi_taban.json "
                f'"dosyalar" kumesinden SU SATIRI SIL:\n      "{yol}": {taban_n},'
            )

    buyuyenler = {
        yol: n - taban_dosyalar[yol]
        for yol, n in ihlaller.items()
        if yol in taban_dosyalar and n > taban_dosyalar[yol]
    }

    return {
        "kok": kok,
        "taban_yolu": taban_yolu,
        "olculdu": taban.get("olculdu"),
        "esik_satir": esik,
        "dosya_sayisi": len(kapsam),
        "toplam_satir": sum(satirlar.values()),
        "ihlaller": [{"yol": y, "satir": n} for y, n in sorted(ihlaller.items())],
        "buyuyenler": buyuyenler,
        "kirmizilar": kirmizilar,
    }


def metin_raporu(sonuc, kapi_modu):
    satirlar = []
    satirlar.append("=== Momentum kod sagligi kapisi (M-2, satir ekseni) ===")
    satirlar.append(f"kok={sonuc['kok']}  taban={sonuc['taban_yolu']} (olculdu={sonuc['olculdu']})")
    satirlar.append(
        f"OZET: {sonuc['dosya_sayisi']} dosya tarandi (git ls-files; .dart+.cs, "
        f"uretilenler+testler haric), toplam {sonuc['toplam_satir']} satir, "
        f"{len(sonuc['ihlaller'])} ihlal (esik={sonuc['esik_satir']} satir)."
    )
    if sonuc["ihlaller"]:
        satirlar.append(f"{sonuc['esik_satir']} satiri asan dosyalar:")
        for kayit in sonuc["ihlaller"]:
            ek = ""
            if kayit["yol"] in sonuc["buyuyenler"]:
                ek = f"  (+{sonuc['buyuyenler'][kayit['yol']]} satir, taban ile kiyasla)"
            satirlar.append(f"  {kayit['satir']:>5}  {kayit['yol']}{ek}")
    if sonuc["kirmizilar"]:
        satirlar.append("KIRMIZI BULGULAR:")
        for k in sonuc["kirmizilar"]:
            for i, parca in enumerate(k.split("\n")):
                on_ek = "  - " if i == 0 else "    "
                satirlar.append(on_ek + parca)
    if kapi_modu:
        sonuc_sozu = "KIRMIZI" if sonuc["kirmizilar"] else "YESIL"
        satirlar.append(f"SONUC (--kapi): {sonuc_sozu}")
    else:
        satirlar.append("SONUC: rapor kipi -- olcumdur, kapi degil, exit 0.")
    return "\n".join(satirlar)


def main(argv):
    ap = argparse.ArgumentParser(
        description="Momentum kod sagligi kapisi (M-2, satir ekseni)"
    )
    ap.add_argument(
        "--kapi",
        action="store_true",
        help="ihlal varsa exit 1 doner (yoksa rapor kipi, olcum, her zaman 0)",
    )
    ap.add_argument("--json", action="store_true", help="JSON cikti (varsayilan: metin)")
    ap.add_argument(
        "--kok",
        default=None,
        help="repo koku (varsayilan: mevcut dizin -- CI'da is adiminin working-directory'si)",
    )
    ap.add_argument(
        "--taban",
        default=None,
        help="taban json yolu (varsayilan: araclar/kod_sagligi_taban.json)",
    )
    args = ap.parse_args(argv)

    kok = os.path.abspath(args.kok) if args.kok else os.getcwd()
    taban_yolu = os.path.abspath(args.taban) if args.taban else TABAN_VARSAYILAN

    try:
        sonuc = olc(kok, taban_yolu)
    except OlculemediHatasi as exc:
        if args.json:
            print(json.dumps({"hata": str(exc), "kod": 2}, ensure_ascii=False, indent=2))
        else:
            print(f"OLCULEMEDI: {exc}", file=sys.stderr)
        return 2

    if args.json:
        cikti = dict(sonuc)
        cikti["kapi_modu"] = args.kapi
        cikti["sonuc"] = "KIRMIZI" if (args.kapi and sonuc["kirmizilar"]) else "YESIL"
        print(json.dumps(cikti, ensure_ascii=False, indent=2))
    else:
        print(metin_raporu(sonuc, args.kapi))

    if args.kapi and sonuc["kirmizilar"]:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

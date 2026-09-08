# -*- coding: utf-8 -*-
"""M-2 -- kod_sagligi.py'nin kapiyi GERCEKTEN isirdigini kanitlar.

Dort kol, HER BIRI kendi gecici sahte git deposunu kurar (kum havuzu
PAYLASILMAZ -- bir kolun dosyasi baskasini etkilemez):

  POZITIF KONTROL -- hepsi esigin altinda -> kapi YESIL (exit 0). Bu kol
    olmadan asagidaki mutantlarin kirmizisi "kapi olcuyor" demek DEGILDIR --
    yalniz "her zaman kirmizi yakiyor" da olabilirdi.
  MUTANT A -- taban DISINDA 401 satirlik (hepsi dolu) yeni dosya -> KIRMIZI.
  MUTANT B -- taban DISINDA 401 satirlik dosya, 200'u BOS SATIR -> yine
    KIRMIZI. PowerShell `Measure-Object -Line` boslugu SAYMAZ ve bunu 201
    sanip kacirirdi (8 Eyl'de gercek olculmus kusur) -- bu kol o siniftan
    bir mutanti YAKALADIGINI kanitlar.
  MUTANT C -- taban dosyasi (kod_sagligi_taban.json) YOK -> exit kodu 2
    (OLCULEMEDI), 0 DEGIL. "Kirmizi yakmiyor" ile "olcemedim" karistirilamaz.

Cikis kodu: 0 -- dordu de kehanetini tuttu. 1 -- en az biri sapti (detay
stdout'ta, hangi kol/ne beklenip ne bulundugu ile).
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
KOD_SAGLIGI = os.path.join(SCRIPT_DIR, "kod_sagligi.py")


def gecici_depo_kur(taban_no):
    """Bos bir git deposu (init + commit yok, index yeterli) doner."""
    kok = tempfile.mkdtemp(prefix=f"kod-sagligi-mutant-{taban_no}-")
    subprocess.run(["git", "init", "-q"], cwd=kok, check=True)
    # Sahte kimlik: commit ATILMAYACAK (ls-files index'i okur), ama bazi git
    # surumleri `git add` icin bile user.email bekleyebiliyor -- ucuz sigorta.
    subprocess.run(["git", "config", "user.email", "mutant@example.invalid"], cwd=kok, check=True)
    subprocess.run(["git", "config", "user.name", "mutant"], cwd=kok, check=True)
    return kok


def dosya_yaz_ve_ekle(kok, goreli_yol, icerik):
    tam_yol = os.path.join(kok, *goreli_yol.split("/"))
    os.makedirs(os.path.dirname(tam_yol), exist_ok=True)
    with open(tam_yol, "w", encoding="utf-8", newline="\n") as f:
        f.write(icerik)
    subprocess.run(["git", "add", goreli_yol], cwd=kok, check=True)


def taban_yaz(kok, dosyalar, esik=400):
    taban_yolu = os.path.join(kok, "taban.json")
    with open(taban_yolu, "w", encoding="utf-8") as f:
        json.dump(
            {
                "olculdu": "test",
                "commit": "test",
                "kapsam": "urun-kodu-testler-haric",
                "esik_satir": esik,
                "dosyalar": dosyalar,
            },
            f,
            ensure_ascii=False,
            indent=2,
        )
    return taban_yolu


def dolu_satirlar(n, on_ek="x"):
    return "\n".join(f"// {on_ek} satir {i}" for i in range(n)) + "\n"


def bosluklu_satirlar(n_dolu, n_bos):
    parcalar = [f"// dolu satir {i}" for i in range(n_dolu)] + [""] * n_bos
    return "\n".join(parcalar) + "\n"


def kod_sagligi_calistir(kok, taban_yolu, ekstra_args=()):
    komut = [sys.executable, KOD_SAGLIGI, "--kok", kok, "--taban", taban_yolu, "--json"] + list(ekstra_args)
    sonuc = subprocess.run(komut, capture_output=True, text=True, encoding="utf-8")
    try:
        gövde = json.loads(sonuc.stdout)
    except json.JSONDecodeError:
        gövde = None
    return sonuc.returncode, gövde, sonuc.stdout, sonuc.stderr


def kol_pozitif_kontrol():
    kok = gecici_depo_kur("poz")
    try:
        dosya_yaz_ve_ekle(kok, "lib/a.dart", dolu_satirlar(50))
        dosya_yaz_ve_ekle(kok, "lib/b.cs", dolu_satirlar(120))
        taban_yolu = taban_yaz(kok, {})
        kod, govde, out, err = kod_sagligi_calistir(kok, taban_yolu, ["--kapi"])
        beklenen = kod == 0 and govde is not None and govde.get("sonuc") == "YESIL"
        detay = f"exit={kod} sonuc={govde.get('sonuc') if govde else None}"
        return beklenen, detay, out, err
    finally:
        shutil.rmtree(kok, ignore_errors=True)


def kol_mutant_a():
    kok = gecici_depo_kur("a")
    try:
        dosya_yaz_ve_ekle(kok, "lib/kucuk.dart", dolu_satirlar(50))
        dosya_yaz_ve_ekle(kok, "lib/asan.dart", dolu_satirlar(401))
        taban_yolu = taban_yaz(kok, {})  # taban BOS -- yeni dosya tabanda YOK
        kod, govde, out, err = kod_sagligi_calistir(kok, taban_yolu, ["--kapi"])
        beklenen = (
            kod == 1
            and govde is not None
            and govde.get("sonuc") == "KIRMIZI"
            and any("lib/asan.dart" in k for k in govde.get("kirmizilar", []))
        )
        detay = f"exit={kod} sonuc={govde.get('sonuc') if govde else None} kirmizilar={govde.get('kirmizilar') if govde else None}"
        return beklenen, detay, out, err
    finally:
        shutil.rmtree(kok, ignore_errors=True)


def kol_mutant_b():
    kok = gecici_depo_kur("b")
    try:
        icerik = bosluklu_satirlar(201, 200)  # 201 dolu + 200 bos = 401
        # Kendi sayimimizla dogrula: mutant BUDUR, yanlissa test kendini yalanlar.
        gercek_satir = sum(1 for _ in icerik.splitlines(keepends=True))
        assert gercek_satir == 401, f"mutant B kurulumu bozuk: {gercek_satir} satir"
        dosya_yaz_ve_ekle(kok, "lib/bosluklu.dart", icerik)
        taban_yolu = taban_yaz(kok, {})
        kod, govde, out, err = kod_sagligi_calistir(kok, taban_yolu, ["--kapi"])
        beklenen = (
            kod == 1
            and govde is not None
            and govde.get("sonuc") == "KIRMIZI"
            and any("lib/bosluklu.dart" in k for k in govde.get("kirmizilar", []))
        )
        detay = f"exit={kod} sonuc={govde.get('sonuc') if govde else None} kirmizilar={govde.get('kirmizilar') if govde else None}"
        return beklenen, detay, out, err
    finally:
        shutil.rmtree(kok, ignore_errors=True)


def kol_mutant_c():
    kok = gecici_depo_kur("c")
    try:
        dosya_yaz_ve_ekle(kok, "lib/a.dart", dolu_satirlar(50))
        taban_yolu = os.path.join(kok, "taban-yok.json")  # HICBIR ZAMAN yazilmiyor
        kod, govde, out, err = kod_sagligi_calistir(kok, taban_yolu, ["--kapi"])
        beklenen = kod == 2
        detay = f"exit={kod} (govde={govde})"
        return beklenen, detay, out, err
    finally:
        shutil.rmtree(kok, ignore_errors=True)


KOLLAR = [
    ("POZITIF KONTROL (hepsi esik alti -> YESIL)", kol_pozitif_kontrol),
    ("MUTANT A (401 dolu satir, tabanda yok -> KIRMIZI)", kol_mutant_a),
    ("MUTANT B (401 satir, 200 bos -- Measure-Object korlugu -> KIRMIZI)", kol_mutant_b),
    ("MUTANT C (taban dosyasi yok -> exit 2)", kol_mutant_c),
]


def main(argv):
    hepsi_tuttu = True
    for isim, fonksiyon in KOLLAR:
        try:
            tuttu, detay, out, err = fonksiyon()
        except Exception as exc:  # kolun kendi kurulumu patladiysa da SAPMA say
            tuttu, detay, out, err = False, f"KOL PATLADI: {exc!r}", "", ""
        durum = "TUTTU" if tuttu else "SAPTI"
        print(f"[{durum}] {isim} -- {detay}")
        if not tuttu:
            hepsi_tuttu = False
            if out:
                print("  --- kod_sagligi.py stdout ---")
                print("  " + out.replace("\n", "\n  "))
            if err:
                print("  --- kod_sagligi.py stderr ---")
                print("  " + err.replace("\n", "\n  "))

    if hepsi_tuttu:
        print("SONUC: dort kolun dordu de kehanetini tuttu.")
        return 0
    print("SONUC: en az bir kol sapti -- kapi kor OLABILIR, yukariya bak.")
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

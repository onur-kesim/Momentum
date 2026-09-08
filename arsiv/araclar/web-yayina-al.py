# -*- coding: utf-8 -*-
"""GOREV-W3b T3 -- web build'ini yayina alma betigi (D-W3b-3/4/5).

Kaynak: src/client/build/web (flutter build web ciktisi)
Hedef : src/backend/Momentum.Api/wwwroot

SIRA ZORUNLUDUR (D-W3b-5): ① hedef VARSA icerigi BOSALTILIR (bayat artik dosya
sinifi -- eski main.dart.js kalirsa tarayici onu yukleyebilir) ② build ciktisi
KOPYALANIR ③ `_BUILD.json` EN SON yazilir -- boylece yarim kalan bir kopyalama
`_BUILD.json`SUZ kalir ve `W3b/G50/d` KIRMIZI verir (yarim build yesil GECEMEZ).

🔴 Bu betik MOUNT'TAN KOSULMAZ (D-W3b-5): `os.remove`/`unlink` mount'ta
YASAKTIR (ORTAM.md) ve adim ① patlar. Yalniz Onur'un makinesinde (Claude Code
eliyle) kosar.
"""
import hashlib
import json
import os
import shutil
import subprocess
import sys
from datetime import datetime, timezone

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

KOK = r"C:\dev\Momentum"
ISTEMCI = os.path.join(KOK, "src", "client")
FLUTTER = r"C:\src\flutter\bin\flutter.bat"
BUILD_CIKTISI = os.path.join(ISTEMCI, "build", "web")
HEDEF = os.path.join(KOK, "src", "backend", "Momentum.Api", "wwwroot")

# D-W3b-3: build komutu IKI BAYRAGI DA tasir (K159-b + SENKRON_SUNUCU_URL ZORUNLU).
BAYRAKLAR = [
    "--release",
    "--no-web-resources-cdn",
    "--dart-define=SENKRON_SUNUCU_URL=http://localhost:5298",
]

ORTAM = dict(os.environ)
ORTAM["PROGRAMFILES(X86)"] = r"C:\Program Files (x86)"


def kaynak_sha_hesapla(depo_kok):
    """D-W3b-4 -- kaynakSha URETIM KURALININ TANIMI.

    🔴 BU FONKSIYON `araclar/yayin-kapisi.py`'DEKI `kaynak_sha_hesapla` ILE
    BAYT-BAYT AYNI OLMAK ZORUNDADIR (spec: "iki el iki deger uretirse kapi
    ikisini de gecer"). Degisirse IKISI BIRDEN degistirilir.

    Kural: `src/client/lib/**` + `src/client/web/**` + `src/client/pubspec.yaml`
    + `src/client/pubspec.lock`; depo kokune gore POSIX yoluna gore SIRALANIR;
    her dosya icin `sha256(yol + "\\n" + icerik)` hesaplanir ve ARDISIK olarak
    bir toplam sha256'ya beslenir (`toplam.update(tekil_hash_bytes)`).
    `build/`, `.dart_tool/`, gizli dosyalar HARIC.
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


def flutter_surumu_olc():
    """K86: `flutter` bu makinede `.bat`; PATHEXT cozulmez, TAM YOL cagrilir.
    Surum OLCULUR, varsayilmaz (spec §4c)."""
    p = subprocess.run(
        [FLUTTER, "--version"],
        cwd=ISTEMCI,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        env=ORTAM,
        timeout=60,
    )
    ilk_satir = (p.stdout or "").splitlines()[0].strip() if p.stdout else ""
    parcalar = ilk_satir.split()
    surum = parcalar[1] if len(parcalar) > 1 else "[OLCULEMEDI]"
    return surum, ilk_satir


def build_kos():
    cmd = [FLUTTER, "build", "web"] + BAYRAKLAR
    print("KOSUYOR: %s" % " ".join(cmd))
    sys.stdout.flush()
    p = subprocess.run(cmd, cwd=ISTEMCI, env=ORTAM, timeout=900)
    return p.returncode


def hedefi_bosalt(hedef):
    """D-W3b-5 ① -- hedef VARSA icerigi BOSALTILIR (yoksa olusturulur)."""
    if os.path.isdir(hedef):
        for ad in os.listdir(hedef):
            tam = os.path.join(hedef, ad)
            if os.path.isdir(tam) and not os.path.islink(tam):
                shutil.rmtree(tam)
            else:
                os.remove(tam)
    else:
        os.makedirs(hedef, exist_ok=True)


def ciktiyi_kopyala(kaynak, hedef):
    """D-W3b-5 ② -- build ciktisi kopyalanir. `_BUILD.json` bu adimda YAZILMAZ
    (o adim ③'tur, EN SON)."""
    for ad in os.listdir(kaynak):
        s = os.path.join(kaynak, ad)
        h = os.path.join(hedef, ad)
        if os.path.isdir(s):
            shutil.copytree(s, h)
        else:
            shutil.copy2(s, h)


def build_json_yaz(hedef, kaynak_sha, flutter_surum):
    """D-W3b-5 ③ -- `_BUILD.json` EN SON yazilir (D-W3b-4 semasi BIREBIR)."""
    veri = {
        "kaynakSha": kaynak_sha,
        "zaman": datetime.now(timezone.utc).isoformat(),
        "flutterSurum": flutter_surum,
        "bayraklar": list(BAYRAKLAR),
    }
    yol = os.path.join(hedef, "_BUILD.json")
    with open(yol, "w", encoding="utf-8") as f:
        json.dump(veri, f, ensure_ascii=False, indent=2)
    return yol, veri


def main():
    print("=" * 74)
    print("GOREV-W3b T3 -- web-yayina-al.py")
    print("=" * 74)

    kaynak_sha, dosya_sayisi = kaynak_sha_hesapla(KOK)
    print("kaynakSha = %s (%d dosya taranarak hesaplandi)" % (kaynak_sha, dosya_sayisi))

    flutter_surum, ilk_satir = flutter_surumu_olc()
    print("flutter --version ilk satir: %s" % ilk_satir)
    print("flutterSurum = %s" % flutter_surum)

    rc = build_kos()
    if rc != 0:
        print("HATA: 'flutter build web' EXIT=%d -- kopyalama YAPILMADI." % rc)
        return rc

    if not os.path.isdir(BUILD_CIKTISI):
        print("HATA: build ciktisi bulunamadi: %s" % BUILD_CIKTISI)
        return 1

    print("① hedef bosaltiliyor: %s" % HEDEF)
    hedefi_bosalt(HEDEF)

    print("② build ciktisi kopyalaniyor: %s -> %s" % (BUILD_CIKTISI, HEDEF))
    ciktiyi_kopyala(BUILD_CIKTISI, HEDEF)

    print("③ _BUILD.json yaziliyor (EN SON)")
    yol, veri = build_json_yaz(HEDEF, kaynak_sha, flutter_surum)
    print("YAZILDI: %s" % yol)
    print(json.dumps(veri, ensure_ascii=False, indent=2))

    print("TAMAM.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

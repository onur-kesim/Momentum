#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
pub-lisans-kapisi.py  --  Momentum G3: pubspec.lock LISANS KAPISI

NEDEN VAR (K42-d slice-3b, T8):
  Drift zinciri iki '+eol' transitif paket getirir (Z9) ve Flutter web icin
  iki ikili (sqlite3.wasm, drift_worker.js) elle indirilir (T5, Z11). Hicbiri
  "muhtemelen MIT'tir" diye VARSAYILAMAZ -- lisans FIILEN OLCULUR.

TASARIM DERSLERI (F2 stili SAF PYTHON, Z14 -- bu projede olculmus):
  1) Lisans bilgisi ne 'dart pub deps'te ne '/api/packages/<ad>'de vardir;
     TEK makine-okunur yol '/api/packages/<ad>/metrics' ->
     scorecard.panaReport.licenses[].spdxIdentifier'dir (Z14). Bu uc
     DOKUMANTASYONSUZ ve SURUM GARANTISIZDIR -- kalkani fixture altin
     kumesidir; bu cumle kapi ciktisina AYNEN basilir.
  2) 'bilinmeyen != temiz': licenses alani BOS/EKSIK ise SESSIZCE temiz
     SAYILMAZ, BULGU verir.
  3) SDK kaynakli paketler (flutter, flutter_driver, flutter_test, ...)
     pub.dev'de YOKTUR (404). 404 sessizce temiz sayilmaz; ayri bir
     "SDK -- kapsam disi, gerekceli" listesinde GORUNUR, susmaz.
  4) Vendored ikili varliklar (T5/Z11) ayni kapsamda: drift_worker.js ->
     simolus3/drift (MIT). sqlite3.wasm -> sarmalayici MIT
     (simolus3/sqlite3.dart) + gomulu SQLite CEKIRDEGI PUBLIC DOMAIN --
     bu ikisi AYRI satirlar olarak yazilir, tek satira sikistirilmaz.
  5) Arac ONCE kendini kanitlar: --altin-kume, agina CIKMAZ (fixture),
     temizde SUSAR, mutasyonda ISIRIR.

METIN-KANITLI ESLESME (araclar/lisans-eslesme.json -- Cowork BAGIMSIZ olcumu,
oturum 30, sqlcipher_flutter_libs bulgusu): pana'nin SPDX etiketi bazen
BULANIK SABLON ESLESTIRMESI yuzunden gercek metinle uyusmaz (orn. duz bir
Apache-2.0 metni "Pixar" sablonuna oturtulabilir). Bunu SESSIZCE beyaz
listeye eklemek KOR KAPI uretir; SESSIZCE kirmizi birakmak OLMAYAN bir ihlali
ilan eder. Cozum: her eslesme kaydi <paket, surum, pana_spdx> -> <gercek_aile>
VE zorunlu kanit alanlari (kanit_dosya, kanit_satir, kanit_olcum, gerekce)
tasir; eslesme YALNIZ o paket+surum icin gecerlidir (genel beyaz liste
DEGILDIR). Kapi her gecerli eslesmeyi "ESLENDI ..." diye BASAR (yutmaz);
alanlardan biri bile eksikse ESLESME GECERSIZ sayilir ve KIRMIZI yanar
(spec-kapi-kapsama.py'nin S4 felsefesiyle ayni: beyan etmek gerekce
vermek demektir).

IZINLI LISANSLAR: MIT, BSD-2-Clause, BSD-3-Clause, Apache-2.0.

CIKIS KODLARI:
  0 = temiz (izinsiz lisans 0, bilinmeyen 0, gecersiz eslesme 0)
  1 = izinsiz/bilinmeyen lisans ya da gecersiz eslesme var
  2 = kullanim/girdi hatasi | 3 = veri/aglama hatasi (bozuk JSON, erisilemeyen uc)

KULLANIM:
  python araclar\\pub-lisans-kapisi.py --altin-kume
  python araclar\\pub-lisans-kapisi.py <pubspec.lock> [--eslesme araclar/lisans-eslesme.json]
"""

import argparse
import datetime
import json
import os
import re
import sys
import urllib.request

SURUM = "0.1.0"

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

UA = {"User-Agent": "momentum-olcum/1.0"}

IZINLI = {"MIT", "BSD-2-Clause", "BSD-3-Clause", "Apache-2.0"}

BEYAN_EDILMIS_SINIR = (
    "/metrics ucu dokumansizdir ve surum garantisizdir; pana'nin lisans "
    "tespiti kusursuz degildir. Bu bir disiplin kapisidir, hukuki guvence degildir."
)

# T5/Z11'in pinledigi vendored ikililer -- kaynagi arsiv/araclar/web-varlik.sha256'dir.
VENDORED = [
    ("drift_worker.js", "simolus3/drift (tag drift-2.34.0)", "MIT"),
    ("sqlite3.wasm (sarmalayici)", "simolus3/sqlite3.dart (tag sqlite3-3.5.0)", "MIT"),
    ("sqlite3.wasm (gomulu SQLite cekirdegi)", "sqlite.org (SQLite C kutuphanesi)", "Public Domain"),
]

ESLESME_ZORUNLU_ALAN = ("kanit_dosya", "kanit_satir", "kanit_olcum", "gerekce")
ESLESME_VARSAYILAN_YOLU = os.path.join(os.path.dirname(os.path.abspath(__file__)), "lisans-eslesme.json")


def eslesme_oku(yol):
    """araclar/lisans-eslesme.json'u {(paket, surum, pana_spdx): kayit} okur.

    Dosya yoksa BOS harita doner (eslesme mekanizmasi opsiyoneldir, kapiyi
    kormez -- yoksa eslesmeyen her SPDX normal LIS-YASAKLI yolundan gecer).
    """
    if not yol or not os.path.exists(yol):
        return {}
    with open(yol, "rb") as f:
        kayitlar = json.loads(f.read().decode("utf-8"))
    harita = {}
    for k in kayitlar:
        anahtar = (k.get("paket"), k.get("surum"), k.get("pana_spdx"))
        harita[anahtar] = k
    return harita


# ============================== GIRDI OKUMA ===================================

def pubspec_lock_oku(yol):
    """pubspec.lock'taki 'packages:' blogunu {ad: {kaynak, surum}} olarak okur."""
    with open(yol, "rb") as f:
        metin = f.read().decode("utf-8")
    satirlar = metin.split("\n")
    basla = None
    for i, s in enumerate(satirlar):
        if re.match(r"^packages:\s*$", s):
            basla = i + 1
            break
    if basla is None:
        return {}
    paketler = {}
    ad, kaynak, surum = None, None, None
    i = basla
    while i < len(satirlar):
        s = satirlar[i]
        if re.match(r"^\S", s):
            break
        m = re.match(r"^  (\S+):\s*$", s)
        if m:
            if ad is not None:
                paketler[ad] = {"kaynak": kaynak, "surum": surum}
            ad, kaynak, surum = m.group(1), None, None
            i += 1
            continue
        m2 = re.match(r'^    source:\s*(\S+)\s*$', s)
        if m2:
            kaynak = m2.group(1)
        m3 = re.match(r'^    version:\s*"?([^"]+?)"?\s*$', s)
        if m3:
            surum = m3.group(1)
        i += 1
    if ad is not None:
        paketler[ad] = {"kaynak": kaynak, "surum": surum}
    return paketler


def metrics_getir_gercek(ad):
    url = "https://pub.dev/api/packages/" + ad + "/metrics"
    r = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(r, timeout=30) as f:
        return json.loads(f.read().decode("utf-8"))


# ============================== DEGERLENDIRME =================================

def degerlendir_paket(ad, surum, veri, eslesme_map):
    """Tek paket icin olay listesi dondurur: [(kod, mesaj)].
    kod: 'LIS-YASAKLI' | 'LIS-BILINMEYEN' | 'LIS-ESLESME-GECERSIZ' | 'ESLESTI'
    """
    pana = (((veri or {}).get("scorecard") or {}).get("panaReport") or {})
    lisanslar = pana.get("licenses")
    if not lisanslar:
        return [("LIS-BILINMEYEN", "licenses alani BOS/EKSIK -- bilinmeyen != temiz")]
    olaylar = []
    for lic in lisanslar:
        spdx = (lic or {}).get("spdxIdentifier")
        if spdx in IZINLI:
            continue
        kayit = eslesme_map.get((ad, surum, spdx))
        if kayit is None:
            olaylar.append(("LIS-YASAKLI", "izinsiz lisans: %s (izinli kume: %s)" % (
                spdx or "?", ", ".join(sorted(IZINLI)))))
            continue
        eksik = [alan for alan in ESLESME_ZORUNLU_ALAN if not kayit.get(alan)]
        if eksik:
            olaylar.append(("LIS-ESLESME-GECERSIZ",
                             "%s -> %s eslesmesi GECERSIZ: eksik alan(lar) %s -- "
                             "beyan etmek gerekce vermek demektir"
                             % (spdx, kayit.get("gercek_aile", "?"), ", ".join(eksik))))
            continue
        gercek_aile = kayit.get("gercek_aile")
        if gercek_aile not in IZINLI:
            olaylar.append(("LIS-YASAKLI",
                             "%s -> %s eslesmesi var ama gercek_aile IZINLI KUMEDE DEGIL"
                             % (spdx, gercek_aile)))
            continue
        olaylar.append(("ESLESTI",
                         "ESLENDI %s %s -> %s [kanit: %s:%s] -- %s"
                         % (ad, spdx, gercek_aile, kayit["kanit_dosya"], kayit["kanit_satir"],
                            kayit["kanit_olcum"])))
    return olaylar


def tara(paketler, getir, eslesme_map=None):
    """Donen: (bulgular, sdk_listesi, eslenen, ortam_hatalari, ham_kanit).
    bulgular / sdk_listesi / eslenen / ortam_hatalari = [(kod, ad, mesaj)]
    """
    eslesme_map = eslesme_map or {}
    bulgular, sdk_listesi, eslenen, ortam_hatalari = [], [], [], []
    ham_kanit = {}
    for ad in sorted(paketler):
        bilgi = paketler[ad]
        if bilgi.get("kaynak") == "sdk":
            sdk_listesi.append(("SDK", ad, "pub.dev'de yayinlanmamis SDK paketi -- kapsam disi, gerekceli"))
            continue
        try:
            veri = getir(ad)
        except Exception as e:
            ortam_hatalari.append(("ORTAM", ad, str(e)))
            continue
        ham_kanit[ad] = veri
        for kod, mesaj in degerlendir_paket(ad, bilgi.get("surum"), veri, eslesme_map):
            (eslenen if kod == "ESLESTI" else bulgular).append((kod, ad, mesaj))
    return bulgular, sdk_listesi, eslenen, ortam_hatalari, ham_kanit


# ============================== ALTIN KUME ===================================
# AGA CIKMAZ (fixture) -- 6 vaka (K40: esik degistiren vaka ekler; 4 -> 6
# METIN-KANITLI ESLESME mekanizmasi eklendiginde, oturum 30).

def _metrics(lisanslar):
    return {"scorecard": {"panaReport": {"licenses": lisanslar}}}


def altin_kume():
    vakalar = []

    def vaka(ad, beklenen_kodlar, paketler, getir, eslesme=None, beklenen_eslenen=False):
        bulgular, sdk_listesi, eslenen, ortam, _ = tara(paketler, getir, eslesme)
        tumu = bulgular + sdk_listesi + ortam
        olculen = sorted(set(k for k, *_ in tumu))
        kod_gecti = olculen == sorted(set(beklenen_kodlar))
        eslenen_gecti = bool(eslenen) == beklenen_eslenen
        gecti = kod_gecti and eslenen_gecti
        vakalar.append((gecti, ad, sorted(set(beklenen_kodlar)), olculen, tumu,
                         beklenen_eslenen, eslenen))

    # 1) hepsi MIT -- SUSMALI
    vaka("1) hepsi MIT -- SUSMALI", [],
         {"drift": {"kaynak": "hosted", "surum": "2.34.2"}},
         lambda p: _metrics([{"spdxIdentifier": "MIT"}]))

    # 2) GPL-3.0 -- LIS-YASAKLI ISIRMALI
    vaka("2) GPL-3.0 lisansli paket -- LIS-YASAKLI isirmali", ["LIS-YASAKLI"],
         {"kirli_lisans": {"kaynak": "hosted", "surum": "1.0.0"}},
         lambda p: _metrics([{"spdxIdentifier": "GPL-3.0"}]))

    # 3) licenses BOS -- LIS-BILINMEYEN ISIRMALI ("bilinmeyen != temiz")
    vaka("3) licenses bos -- LIS-BILINMEYEN isirmali", ["LIS-BILINMEYEN"],
         {"bilinmeyen": {"kaynak": "hosted", "surum": "1.0.0"}},
         lambda p: _metrics([]))

    # 4) SDK paketi -- ayri listelenir, SUSMAZ (ama kusur da SAYILMAZ)
    vaka("4) SDK kaynakli paket (404 sinifi) -- ayri listelenir, susmaz", ["SDK"],
         {"flutter": {"kaynak": "sdk", "surum": "0.0.0"}},
         lambda p: (_ for _ in ()).throw(AssertionError("SDK paketi sorgulanmamali")))

    # 5) KANITLI ESLESME (sqlcipher_flutter_libs orneginin genellemesi) -- SUSMALI,
    #    ama ESLENDI olayi BASILMALI (yutulmaz).
    _kanitli = {("paket_x", "1.0.0", "Pixar"): {
        "gercek_aile": "Apache-2.0", "kanit_dosya": "fixture.txt", "kanit_satir": 1,
        "kanit_olcum": "fixture olcum metni", "gerekce": "fixture gerekce metni"}}
    vaka("5) KANITLI eslesme (Pixar->Apache-2.0) -- SUSMALI, ESLENDI basilir", [],
         {"paket_x": {"kaynak": "hosted", "surum": "1.0.0"}},
         lambda p: _metrics([{"spdxIdentifier": "Pixar"}]),
         eslesme=_kanitli, beklenen_eslenen=True)

    # 6) GEREKCESIZ/KANITSIZ eslesme -- LIS-ESLESME-GECERSIZ ISIRMALI (beyan = gerekce).
    _gecersiz = {("paket_y", "1.0.0", "Pixar"): {
        "gercek_aile": "Apache-2.0", "kanit_dosya": "", "kanit_satir": 1,
        "kanit_olcum": "x", "gerekce": "y"}}  # kanit_dosya BOS -- eksik alan
    vaka("6) GECERSIZ eslesme (eksik kanit_dosya) -- LIS-ESLESME-GECERSIZ isirmali",
         ["LIS-ESLESME-GECERSIZ"],
         {"paket_y": {"kaynak": "hosted", "surum": "1.0.0"}},
         lambda p: _metrics([{"spdxIdentifier": "Pixar"}]),
         eslesme=_gecersiz, beklenen_eslenen=False)

    cizgi = "=" * 78
    print(cizgi)
    print("ALTIN KUME -- PUB LISANS KAPISININ KENDI KANITI (aga cikmaz, kor kapi yok)")
    print(cizgi)
    hepsi = True
    for gecti, ad, beklenen, olculen, tumu, beklenen_eslenen, eslenen in vakalar:
        print("\n[%s] %s" % ("GECTI" if gecti else "KALDI", ad))
        print("    beklenen: %s | olculen: %s | eslenen beklenen: %s | eslenen olculen: %d"
              % (beklenen or "[]", olculen or "[]", beklenen_eslenen, len(eslenen)))
        if not gecti:
            hepsi = False
            for kod, pad, mesaj in tumu:
                print("        %s %s %s" % (kod, pad, mesaj))
            for kod, pad, mesaj in eslenen:
                print("        [%s] %s %s" % (kod, pad, mesaj))
    print("\n" + cizgi)
    if hepsi:
        print("HUKUM: ARAC KULLANILABILIR -- temizde susuyor, kirlide isiriyor.")
        print(cizgi)
        return 0
    print("HUKUM: ARAC KULLANILAMAZ -- altin kume KALDI. Gercek tarama HUKUM VERMEZ.")
    print(cizgi)
    return 1


# ============================== RAPOR / CLI ==================================

def rapor(bulgular, sdk_listesi, eslenen, ortam, ham_kanit, paketler, lock_yolu, eslesme_yolu):
    cizgi = "=" * 78
    print(cizgi)
    print("PUB LISANS KAPISI (G3) v%s" % SURUM)
    print("pubspec.lock : %s" % lock_yolu)
    print("eslesme dosyasi : %s" % (eslesme_yolu if eslesme_yolu and os.path.exists(eslesme_yolu) else "YOK"))
    print("sorgu zamani : %s UTC" % datetime.datetime.now(datetime.timezone.utc).isoformat())
    print(cizgi)

    print("\nOZET: %d paket (SDK %d, hosted %d)"
          % (len(paketler), len(sdk_listesi), len(paketler) - len(sdk_listesi)))

    if sdk_listesi:
        print("\nSDK -- KAPSAM DISI, GEREKCELI (%d):" % len(sdk_listesi))
        for kod, ad, mesaj in sdk_listesi:
            print("  [%s] %s -- %s" % (kod, ad, mesaj))

    print("\nVENDORED IKILILER (T5/Z11, kapsamda):")
    for ad, kaynak, lisans in VENDORED:
        print("  %s -- kaynak: %s -- lisans: %s" % (ad, kaynak, lisans))

    if eslenen:
        print("\nESLENDI (metin-kanitli eslesme, yutulmadi) (%d):" % len(eslenen))
        for kod, ad, mesaj in eslenen:
            print("  %s" % mesaj)

    if ortam:
        print("\nORTAM HATASI (%d) -- bu paketler OLCULEMEDI:" % len(ortam))
        for kod, ad, mesaj in ortam:
            print("  [%s] %s -- %s" % (kod, ad, mesaj))

    if bulgular:
        print("\nBULGU (%d):" % len(bulgular))
        for kod, ad, mesaj in bulgular:
            print("  [%s] %s -- %s" % (kod, ad, mesaj))

    print("\n" + cizgi)
    print("BEYAN EDILMIS SINIR: " + BEYAN_EDILMIS_SINIR)
    print(cizgi)

    if ortam:
        print("HUKUM: OLCULEMEDI -- %d paket icin veri alinamadi. (exit 3)" % len(ortam))
        print(cizgi)
        return 3
    if bulgular:
        print("HUKUM: KUSURLU -- %d izinsiz/bilinmeyen lisans ya da gecersiz eslesme bulgusu. (exit 1)"
              % len(bulgular))
        print(cizgi)
        return 1
    print("HUKUM: TEMIZ -- izinsiz lisans 0, bilinmeyen 0, gecersiz eslesme 0. (exit 0)")
    print(cizgi)
    return 0


def main(argv):
    ap = argparse.ArgumentParser(description="Momentum pub.dev lisans kapisi (G3)")
    ap.add_argument("pubspec_lock", nargs="?", help="pubspec.lock yolu")
    ap.add_argument("--altin-kume", action="store_true",
                    help="araci kendi altin kumesinde kanitla (cikis 0 olmali)")
    ap.add_argument("--eslesme", default=ESLESME_VARSAYILAN_YOLU,
                    help="metin-kanitli lisans eslesme dosyasi (varsayilan: araclar/lisans-eslesme.json)")
    ap.add_argument("--kanit", default=None, help="ham JSON + ozet KANIT dosyasi yolu")
    a = ap.parse_args(argv)

    if a.altin_kume:
        return altin_kume()

    if not a.pubspec_lock:
        ap.print_usage()
        print("HATA: pubspec.lock yolu gerekli (ya da --altin-kume).")
        return 2
    if not os.path.isfile(a.pubspec_lock):
        print("HATA: dosya yok: %s" % a.pubspec_lock)
        return 2

    eslesme_map = eslesme_oku(a.eslesme)
    paketler = pubspec_lock_oku(a.pubspec_lock)
    bulgular, sdk_listesi, eslenen, ortam, ham_kanit = tara(paketler, metrics_getir_gercek, eslesme_map)

    kod = rapor(bulgular, sdk_listesi, eslenen, ortam, ham_kanit, paketler, a.pubspec_lock, a.eslesme)

    if a.kanit:
        os.makedirs(os.path.dirname(a.kanit), exist_ok=True)
        with open(a.kanit, "w", encoding="utf-8", newline="\n") as f:
            f.write("sorgu zamani (UTC): %s\n" % datetime.datetime.now(datetime.timezone.utc).isoformat())
            f.write("eslesme dosyasi: %s\n" % a.eslesme)
            f.write("sdk_listesi: %s\n" % sdk_listesi)
            f.write("eslenen: %s\n" % eslenen)
            f.write("bulgu: %s\n" % bulgular)
            f.write("ortam_hatasi: %s\n" % ortam)
            f.write("vendored: %s\n" % VENDORED)
            f.write("\n--- HAM JSON (paket basina) ---\n")
            for ad in sorted(ham_kanit):
                f.write("\n=== %s ===\n" % ad)
                f.write(json.dumps(ham_kanit[ad], ensure_ascii=True, indent=2))
                f.write("\n")

    return kod


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

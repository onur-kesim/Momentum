# -*- coding: utf-8 -*-
"""pub-surum-olc.py -- pub.dev'den paket SURUMLERINI ve ADVISORY'lerini OLCER.

NEDEN VAR: oturum 29'da iki bagimsiz arastirmaci AYNI paketler icin FARKLI
"en guncel surum" bildirdi (drift_flutter 0.3.1 vs 0.3.0 gibi). Hakem akil
yurutme degil OLCUMDUR. Bu betik pub.dev resmi API'sine sorar.

Ayrica G2 (CVE kapisi) icin kullanilacak /advisories uc noktasini da yoklar;
yani bu betik ayni zamanda o uc noktanin CANLI oldugunun kanitidir.

Kullanim: python araclar\\pub-surum-olc.py <paket> [paket...]
Cikis: 0 = hepsi cozuldu · 2 = en az biri cozulemedi
"""
import json
import sys
import urllib.request

UA = {"User-Agent": "momentum-olcum/1.0"}


def getir(url):
    r = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(r, timeout=30) as f:
        return json.loads(f.read().decode("utf-8"))


def main(paketler):
    if not paketler:
        print("KULLANIM: python araclar\\pub-surum-olc.py <paket> [paket...]")
        return 2
    hata = False
    print("%-24s %-12s %-28s %s" % ("PAKET", "EN GUNCEL", "YAYIN", "ADVISORY"))
    print("-" * 84)
    for p in paketler:
        try:
            d = getir("https://pub.dev/api/packages/" + p)
            son = d.get("latest") or {}
            sur = son.get("version", "?")
            yay = (son.get("published") or "?")[:19]
        except Exception as e:
            print("%-24s %s" % (p, "COZULEMEDI: " + str(e)))
            hata = True
            continue
        try:
            a = getir("https://pub.dev/api/packages/" + p + "/advisories")
            adv = a.get("advisories") or []
            adv_s = str(len(adv)) + " kayit"
            if adv:
                adv_s += " (" + ", ".join(str(x.get("id")) for x in adv[:3]) + ")"
        except Exception as e:
            adv_s = "ADVISORY HATASI: " + str(e)
        print("%-24s %-12s %-28s %s" % (p, sur, yay, adv_s))
    print("-" * 84)
    print("HUKUM: " + ("EN AZ BIRI COZULEMEDI" if hata else "OLCULDU"))
    return 2 if hata else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

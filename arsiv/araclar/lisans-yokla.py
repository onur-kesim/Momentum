# -*- coding: utf-8 -*-
"""lisans-yokla.py -- pub.dev'de LISANS bilgisinin HANGI uc noktada
oldugunu OLCER (G3 kapisinin veri kaynagi kararini beyanla degil olcumle ver).

Oturum 29 denetimi iddia etti: "dart pub deps --json lisans dondurmez ve
/api/packages/<ad> de dondurmez; lisans yalnizca kararsiz /metrics
ucundadir". Bu betik o iddiayi SINAR.
"""
import json
import sys
import urllib.request

UA = {"User-Agent": "momentum-olcum/1.0"}


def getir(url):
    r = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(r, timeout=30) as f:
        return json.loads(f.read().decode("utf-8"))


def main():
    p = "drift"
    print("=== /api/packages/" + p + " ===")
    d = getir("https://pub.dev/api/packages/" + p)
    print("ust duzey alanlar:", sorted(d.keys()))
    print("latest alanlari  :", sorted((d.get("latest") or {}).keys()))
    ham = json.dumps(d.get("latest") or {})
    print("icinde 'licen' geciyor mu:", "licen" in ham.lower())

    print()
    print("=== /api/packages/" + p + "/metrics ===")
    try:
        m = getir("https://pub.dev/api/packages/" + p + "/metrics")
        print("ust duzey alanlar:", sorted(m.keys()))
        sc = m.get("scorecard") or {}
        print("scorecard alanlari:", sorted(sc.keys()))
        pana = sc.get("panaReport") or {}
        print("panaReport alanlari:", sorted(pana.keys()))
        print("LISANSLAR:", json.dumps(pana.get("licenses"), ensure_ascii=True))
    except Exception as e:
        print("HATA:", e)
    return 0


if __name__ == "__main__":
    sys.exit(main())

# -*- coding: utf-8 -*-
"""IS-EMRI-o83-D -- seen dokumu kosucu.

Tam paket (tests/Momentum.Persistence.Tests, 69 test, TEK surec) icinde
Cursor_correctness_is_unaffected_by_concurrent_dispatch_single_owner dusene kadar
(en fazla 3 kosum) `dotnet test` calistirir. Bu betik URUN KODUNA DA TEST IDDIASINA DA
DOKUNMAZ; DispatcherTests.cs'teki gecici enstrumantasyon ayri bir Edit ile eklendi/geri
alinacak (IS-EMRI-o83-D.md s2.2/s2.3). Betik yalniz: (a) her kosumu O83D_RUN ortam
degiskeniyle etiketler, (b) TRX'ten HEDEF testin outcome'unu okur, (c) dustugu ANDA durur.
"""
import io
import os
import subprocess
import sys
import xml.etree.ElementTree as ET

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

KOK = r"C:\dev\Momentum"
KANIT = os.path.join(KOK, "KANIT", "o83D")
TRX_DIZIN = os.path.join(KANIT, "trx")
CSPROJ = os.path.join(KOK, "tests", "Momentum.Persistence.Tests", "Momentum.Persistence.Tests.csproj")
TEST_ADI = "Cursor_correctness_is_unaffected_by_concurrent_dispatch_single_owner"
TRX_NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"

os.makedirs(TRX_DIZIN, exist_ok=True)


def trx_sonuc(trx_yolu, test_adi):
    tree = ET.parse(trx_yolu)
    root = tree.getroot()
    for ut in root.iter(TRX_NS + "UnitTestResult"):
        test_name = ut.get("testName") or ""
        if test_adi in test_name:
            return ut.get("outcome"), test_name
    return None, None


def main():
    for kosum in range(1, 4):
        trx_adi = "kosum-%d.trx" % kosum
        trx_yolu = os.path.join(TRX_DIZIN, trx_adi)
        if os.path.isfile(trx_yolu):
            os.remove(trx_yolu)

        env = dict(os.environ)
        env["O83D_RUN"] = str(kosum)

        cmd = [
            "dotnet", "test", CSPROJ,
            "--logger", "trx;LogFileName=%s" % trx_adi,
            "--results-directory", TRX_DIZIN,
        ]
        print("=== KOSUM %d/3 ===" % kosum)
        print(" ".join(cmd))
        proc = subprocess.run(
            cmd, cwd=KOK, env=env, capture_output=True, text=True,
            encoding="utf-8", errors="replace",
        )
        stdout_yolu = os.path.join(KANIT, "_stdout-kosum-%d.txt" % kosum)
        stderr_yolu = os.path.join(KANIT, "_stderr-kosum-%d.txt" % kosum)
        with io.open(stdout_yolu, "w", encoding="utf-8") as f:
            f.write(proc.stdout)
        with io.open(stderr_yolu, "w", encoding="utf-8") as f:
            f.write(proc.stderr)

        outcome, test_name = (None, None)
        if os.path.isfile(trx_yolu):
            try:
                outcome, test_name = trx_sonuc(trx_yolu, TEST_ADI)
            except Exception as e:
                print("TRX PARSE HATASI:", e)
        else:
            print("UYARI: TRX dosyasi yok:", trx_yolu)

        print("dotnet test exit=%s  hedef test=%s  outcome=%s" % (proc.returncode, test_name, outcome))

        if outcome == "Failed":
            print("DUSTU -- kosum %d/3 -- DUR" % kosum)
            return 0
        elif outcome == "Passed":
            print("gecti -- kosum %d/3 -- devam" % kosum)
        else:
            print("UYARI: hedef test TRX'te bulunamadi/tanimsiz outcome (%r) -- devam" % (outcome,))

    print("3 kosumda da DUSMEDI -- 'tam pakette bu turda URETILEMEDI'")
    return 0


if __name__ == "__main__":
    sys.exit(main())

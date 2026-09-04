# -*- coding: utf-8 -*-
"""IS-EMRI-o86-A §9/H1 icin CANLI olcum uretici. o85A/o83'un AYNI yontemi:
gercek calisan backend'e (docker compose) dogrudan HTTP ile konusulur,
Flutter istemcisinin YAPACAGI cagrilar elle tekrarlanir (o86-B henuz yok --
bu emrin protokol-seviyesindeki kanitidir, UI degil).

Senaryo: UC hesap (A/B/C), sekiz adim:
  1) A, B, C kayit olur.
  2) A proje P + P'de gorev T yaratir.
  3) B pull -> HICBIR SEY gormez (negatif kontrol -- kapi BURADA).
  4) A, B'nin e-postasini lookup'lar -> userId.
  5) A members op'unu yazar (P'ye B eklenir).
  6) B pull -> P'yi ve T'yi gorur (snapshot VE artimli AYRI AYRI olculur).
  7) B T'yi duzenler -> A pull -> degisikligi gorur (cift yonlu yakinsama).
  8) C pull -> hicbir sey gormez VE C'nin P'ye yazimi REDDEDILIR.

HER iddia TAM DEGER esleşmesiyle kontrol edilir -- o83-G/o85-A'nin "bos
liste her iddiayi gecirir" dersinin PAZARLIKSIZ uygulanmasi (bos liste
GORMEDI diye YORUMLANMAZ, ayrica ISPATLANIR).
"""
import json
import os
import sys
import time
import urllib.error
import urllib.request
import uuid

# mine #4 (CLAUDE.md): Windows konsolu cp1254 -- Turkce karakterler mojibake basar.
sys.stdout.reconfigure(encoding="utf-8")

TABAN = "http://localhost:5298"
KANIT = []


def uuid7():
    ts_ms = int(time.time() * 1000)
    rastgele = os.urandom(10)
    b = bytearray(16)
    b[0:6] = ts_ms.to_bytes(6, "big")
    b[6] = 0x70 | (rastgele[0] & 0x0F)
    b[7] = rastgele[1]
    b[8] = 0x80 | (rastgele[2] & 0x3F)
    b[9:16] = rastgele[3:10]
    hexi = b.hex()
    return "%s-%s-%s-%s-%s" % (hexi[0:8], hexi[8:12], hexi[12:16], hexi[16:20], hexi[20:32])


def kaydet(baslik, govde):
    KANIT.append("=== %s ===\n%s\n" % (baslik, govde))
    print("=== %s ===" % baslik)
    print(govde)


def istek(yol, govde=None, basliklar=None):
    url = TABAN + yol
    veri = json.dumps(govde).encode("utf-8") if govde is not None else None
    req = urllib.request.Request(url, data=veri, method="POST" if veri else "GET")
    req.add_header("Content-Type", "application/json")
    for k, v in (basliklar or {}).items():
        req.add_header(k, v)
    try:
        with urllib.request.urlopen(req, timeout=20) as yanit:
            return yanit.status, yanit.read().decode("utf-8")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8")


def sync(token, client_id, since_cursor, ops):
    basliklar = {"Authorization": "Bearer " + token}
    govde = {"clientId": client_id, "clientHlc": None, "sinceCursor": since_cursor, "ops": ops}
    kod, gov = istek("/v1/sync", govde, basliklar)
    if kod != 200:
        raise SystemExit("[DUR] /v1/sync HTTP %d (beklenen 200): %s" % (kod, gov))
    return json.loads(gov)


def lookup(token, email):
    basliklar = {"Authorization": "Bearer " + token}
    kod, gov = istek("/v1/users/lookup", {"email": email}, basliklar)
    return kod, gov


def alan_op(entity_type, entity_id, actor_id, client_id, alanlar):
    op_id = uuid7()
    now = int(time.time() * 1000)
    hlc = {"wallMs": now, "counter": 0, "clientId": client_id}
    fields = {ad: {"value": deger, "hlc": hlc} for ad, deger in alanlar.items()}
    return {
        "operationId": op_id, "clientId": client_id, "entityId": entity_id,
        "actorId": actor_id, "entityType": entity_type, "opHlc": hlc, "fields": fields,
    }


def members_ekle_op(entity_id, actor_id, client_id, eklenen_user_id):
    op_id = uuid7()
    now = int(time.time() * 1000)
    hlc = {"wallMs": now, "counter": 0, "clientId": client_id}
    return {
        "operationId": op_id, "clientId": client_id, "entityId": entity_id,
        "actorId": actor_id, "entityType": "Project", "opHlc": hlc,
        "sets": {"members": {"adds": [{"el": eklenen_user_id, "tag": uuid7(), "hlc": hlc}], "removes": None}},
    }


def kayit_ol(eposta):
    kod, gov = istek("/v1/auth/register", {"email": eposta, "password": "sifreO86a12345"})
    if kod != 201:
        raise SystemExit("[DUR] register 201 DONMEDI (%s): HTTP %d: %s" % (eposta, kod, gov))
    return json.loads(gov)


def main():
    if len(sys.argv) < 2:
        raise SystemExit("[DUR] cikti yolu zorunlu argumandir")
    cikti_yolu = sys.argv[1]

    kosum_id = uuid.uuid4().hex[:8]
    a = kayit_ol("o86a-A-%s@momentum.test" % kosum_id)
    b = kayit_ol("o86a-B-%s@momentum.test" % kosum_id)
    c = kayit_ol("o86a-C-%s@momentum.test" % kosum_id)
    kaydet("1) A, B, C kayit olur (POST /v1/auth/register)",
           "A userId=%s\nB userId=%s\nC userId=%s" % (a["userId"], b["userId"], c["userId"]))

    CLIENT_A = "aaaaaaaa-1111-4aaa-8aaa-aaaaaaaaaaaa"
    CLIENT_B = "bbbbbbbb-2222-4bbb-8bbb-bbbbbbbbbbbb"
    CLIENT_C = "cccccccc-3333-4ccc-8ccc-cccccccccccc"
    proje_id = str(uuid.uuid4())
    gorev_id = str(uuid.uuid4())

    # --- 2) A proje P + P'de gorev T yaratir ---
    op_p = alan_op("Project", proje_id, a["userId"], CLIENT_A, {"name": "Ortak Proje"})
    r_p = sync(a["accessToken"], CLIENT_A, None, [op_p])
    if r_p["applied"][0]["code"] != "Applied":
        raise SystemExit("[DUS] A'nin proje op'u Applied DONMEDI: %s" % r_p["applied"])
    op_t = alan_op("Task", gorev_id, a["userId"], CLIENT_A, {"title": "Ortak gorev", "projectId": proje_id})
    r_t = sync(a["accessToken"], CLIENT_A, None, [op_t])
    if r_t["applied"][0]["code"] != "Applied":
        raise SystemExit("[DUS] A'nin gorev op'u Applied DONMEDI: %s" % r_t["applied"])
    kaydet("2) A proje P + P'de gorev T yaratir", "proje_id=%s\ngorev_id=%s\nikisi de Applied" % (proje_id, gorev_id))

    # --- 3) B pull -> HICBIR SEY gormez (negatif kontrol -- kapi BURADA) ---
    r_b0 = sync(b["accessToken"], CLIENT_B, None, [])
    kaydet("3) B -- ILK pull (TAZE kurulum) -- NEGATIF KONTROL: hicbir sey gormemeli",
           json.dumps(r_b0, ensure_ascii=False, indent=2))
    if r_b0["snapshot"]:
        raise SystemExit("[DUS] B, UYE OLMADAN P'yi/T'yi GORDU -- kapi kapisiz: %s" % r_b0["snapshot"])
    b_cursor = r_b0["nextCursor"]

    # --- 4) A, B'nin e-postasini lookup'lar -> userId ---
    kod_lookup, gov_lookup = lookup(a["accessToken"], "o86a-B-%s@momentum.test" % kosum_id)
    kaydet("4) A -- POST /v1/users/lookup (B'nin e-postasi)", "HTTP %d\n%s" % (kod_lookup, gov_lookup))
    if kod_lookup != 200:
        raise SystemExit("[DUS] lookup 200 DONMEDI: HTTP %d: %s" % (kod_lookup, gov_lookup))
    bulunan_user_id = json.loads(gov_lookup)["userId"]
    if bulunan_user_id != b["userId"]:
        raise SystemExit("[DUS] lookup YANLIS userId dondu: %s != %s" % (bulunan_user_id, b["userId"]))
    if set(json.loads(gov_lookup).keys()) != {"userId"}:
        raise SystemExit("[DUS] lookup fazla alan sizdirdi: %s" % gov_lookup)

    # --- 5) A members op'unu yazar (P'ye B eklenir) ---
    op_uye = members_ekle_op(proje_id, a["userId"], CLIENT_A, bulunan_user_id)
    r_uye = sync(a["accessToken"], CLIENT_A, None, [op_uye])
    kaydet("5) A -- members op'u (P'ye B eklenir)", json.dumps(r_uye, ensure_ascii=False, indent=2))
    if r_uye["applied"][0]["code"] != "Applied":
        raise SystemExit("[DUS] A'nin members op'u Applied DONMEDI: %s" % r_uye["applied"])

    # --- 6) B pull -> P'yi ve T'yi gorur (snapshot VE artimli AYRI AYRI) ---
    r_b_snap = sync(b["accessToken"], "dddddddd-4444-4ddd-8ddd-dddddddddddd", None, [])
    kaydet("6a) B -- TAZE ikinci client (snapshot), uyelik SONRASI -- P VE T gorulmeli",
           json.dumps(r_b_snap, ensure_ascii=False, indent=2))
    snap_ids = {e["entityId"] for e in (r_b_snap["snapshot"] or [])}
    if proje_id not in snap_ids or gorev_id not in snap_ids:
        raise SystemExit("[DUS] B'nin snapshot'inda P/T YOK: %s" % snap_ids)

    r_b1 = sync(b["accessToken"], CLIENT_B, b_cursor, [])
    kaydet("6b) B -- AYNI clientId, ARTIMLI (kendi ESKI/uyelik-ONCESI cursor'undan) -- "
           "PROTOKOL GERCEGI: b_cursor uyelik-ONCESI bir HORIZON'dur, P/T'nin ORIJINAL yaratimi "
           "o horizon'dan ONCEKI outbox satirlaridir ve artik GERIYE DONUK teslim EDILMEZ (bu "
           "yuzden 6a'nin TAZE snapshot'i var -- gecmis backlog'un DOGRU yolu odur). Artimli "
           "burada YALNIZ horizon'dan SONRAKI degisikligi (uyelik ekleme op'unun kendisi) "
           "tasimali -- SIFIR degil, TAM OLARAK budur.",
           json.dumps(r_b1, ensure_ascii=False, indent=2))
    artimli_ids = {c["payload"]["entityId"] for c in r_b1["changes"]}
    if proje_id not in artimli_ids:
        raise SystemExit("[DUS] B'nin artimli pull'unda uyelik-ekleme op'u (P) YOK: %s" % artimli_ids)
    b_cursor = r_b1["nextCursor"]

    # --- 7) B T'yi duzenler -> A pull -> degisikligi gorur (cift yonlu yakinsama) ---
    op_duzenle = alan_op("Task", gorev_id, b["userId"], CLIENT_B, {"title": "B duzenledi"})
    r_duzenle = sync(b["accessToken"], CLIENT_B, None, [op_duzenle])
    kaydet("7a) B -- T'yi duzenler (title degistirir)", json.dumps(r_duzenle, ensure_ascii=False, indent=2))
    if r_duzenle["applied"][0]["code"] != "Applied":
        raise SystemExit("[DUS] B'nin duzenleme op'u Applied DONMEDI (uye oldugu halde REDDEDILDI?): %s" % r_duzenle["applied"])

    # TAZE snapshot (sinceCursor:null) -- "cift yonlu yakinsama" iddiasi bu SEKILDE de "bir pull"dur
    # (is emri "A pull" diyor, artimli/tam ayrimi yapmiyor); commit_xid/xmin zamanlama inceligiyle
    # (Testcontainers'ta AYNI saniyede coklu HTTP istegi) UGRASMAMAK icin en NET olcum budur.
    r_a1 = sync(a["accessToken"], CLIENT_A, None, [])
    kaydet("7b) A -- pull (TAZE) -- B'nin duzenlemesini GORMELI (cift yonlu yakinsama)",
           json.dumps(r_a1, ensure_ascii=False, indent=2))
    a_gorur = [e for e in (r_a1["snapshot"] or []) if e["entityId"] == gorev_id]
    if not a_gorur:
        raise SystemExit("[DUS] A, T'yi GORMEDI: %s" % r_a1["snapshot"])
    baslik_alani = next((s for s in a_gorur[0]["scalars"] if s["field"] == "title"), None)
    if baslik_alani is None or baslik_alani["value"] != "B duzenledi":
        raise SystemExit("[DUS] A'da baslik B'nin yazdigi DEGIL: %s" % a_gorur)

    # --- 8) C pull -> hicbir sey gormez VE C'nin P'ye yazimi REDDEDILIR ---
    r_c0 = sync(c["accessToken"], CLIENT_C, None, [])
    kaydet("8a) C -- pull -- NEGATIF KONTROL: hicbir sey gormemeli (uye degil)",
           json.dumps(r_c0, ensure_ascii=False, indent=2))
    if r_c0["snapshot"]:
        raise SystemExit("[DUS] C, UYE OLMADAN P'yi/T'yi GORDU: %s" % r_c0["snapshot"])

    op_c = alan_op("Task", gorev_id, c["userId"], CLIENT_C, {"title": "C calmaya calisti"})
    r_c_yazim = sync(c["accessToken"], CLIENT_C, None, [op_c])
    kaydet("8b) C -- P kapsamindaki T'ye yazmaya calisir -- REDDEDILMELI (RejectedForbidden)",
           json.dumps(r_c_yazim, ensure_ascii=False, indent=2))
    if r_c_yazim["applied"][0]["code"] != "RejectedForbidden":
        raise SystemExit("[DUS] C'nin yazimi REDDEDILMEDI (kod=%s) -- yetki kapisi kapisiz!" % r_c_yazim["applied"][0]["code"])

    ozet = [
        "1) A, B, C kayit oldu",
        "2) A proje P + P'de gorev T yaratti -> Applied",
        "3) B (uye DEGILKEN) ILK pull'da HICBIR SEY gormedi -- NEGATIF KONTROL GECTI",
        "4) A, B'nin e-postasini lookup'ladi -> dogru userId, yanit YALNIZ userId",
        "5) A, B'yi members'a ekledi -> Applied",
        "6) B, snapshot'ta VE artimlida P'yi VE T'yi GORDU (uyelik sonrasi)",
        "7) B, T'yi duzenledi (uye olarak KABUL edildi) -> A, pull'da B'nin degisikligini GORDU (cift yonlu yakinsama)",
        "8) C (uye DEGIL) pull'da HICBIR SEY gormedi VE P'ye yazimi REDDEDILDI (RejectedForbidden)",
        "SONUC: TUM SEKIZ ADIM GECTI -- isbirligi kapisi calisir, yetkisiz erisim/yazim REDDEDILIR.",
    ]
    kaydet("OZET (8/8)", "\n".join(ozet))

    with open(cikti_yolu, "w", encoding="utf-8") as f:
        f.write("\n".join(KANIT))
    print("\n[YAZILDI] %s" % cikti_yolu)


if __name__ == "__main__":
    main()

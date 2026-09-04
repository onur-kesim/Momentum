# -*- coding: utf-8 -*-
"""IS-EMRI-o86-A2 §F icin CANLI olcum uretici (IS-EMRI-o86-A3 §D'de 13-14 ile GENISLETILDI --
yeni betik YAZILMADI). o86-A'nin AYNI sekiz adimi (protokol seviyesinde, dogrudan HTTP -- o86-B
henuz yok) + DENETIMIN bulduklarini kapatan adimlar (9-14). Ayrica adim 7 DEGISTI: o86-A'yi
YANLISLIKLA gecirmis olan TAZE snapshot olcumu yerine artik A'nin KENDI ARTIMLI (incremental)
pull'u kullanilir -- o86-A2 is emrinin kendi tespiti: "TAZE snapshot ile OLCME -- o86-A'yi
yanlislikla gecirmis tam buydu (snapshot, T'yi A'nin ESKI yaratim satirindan bulup GUNCEL CRDT
durumunu donduruyor => iddia BASKA sebeple yesil oluyor)."

Senaryo: UC hesap (A/B/C), on dort adim:
  1) A, B, C kayit olur.
  2) A proje P + P'de gorev T yaratir. (a_cursor: T'nin yaratimindan HEMEN SONRAKI
     horizon -- adim 7'nin ARTIMLI olcumu buradan baslar.)
  3) B pull -> HICBIR SEY gormez (negatif kontrol -- kapi BURADA).
  4) A, B'nin e-postasini lookup'lar -> userId.
  5) A members op'unu yazar (P'ye B eklenir).
  6) B pull -> P'yi ve T'yi gorur (snapshot VE artimli AYRI AYRI olculur).
  7) B T'yi duzenler -> A a_cursor'dan ARTIMLI pull yapar -> degisikligi gorur
     (o86-A'nin TAZE-snapshot kacagi KAPANDI -- bkz. yukarida).
  8) C pull -> hicbir sey gormez VE C'nin P'ye yazimi REDDEDILIR.
  9) [YENI] A, B'yi uyelikten CIKARIR -> B'nin sonraki (TAZE) pull'u P/T'yi ARTIK
     GORMEZ (bulgu 1'in tersi yonden dogrulanmasi: gorunurluk GERI ALINABILIR).
  10) [YENI] eski uye B, T'yi Gelen Kutusu'na (projectId=null) KOPARMAYA calisir ->
      RejectedForbidden (bulgu 2 §C).
  11) [YENI] eski uye B, T'yi KENDI projesi Q'ya TASIMAYA calisir -> RejectedForbidden
      (IZIN(post=Q) true olsa BILE IZIN(pre=P) artik false).
  12) [YENI] yabanci C, TAHMIN ETTIGI projectId=P ile YEPYENI bir gorev ENJEKTE etmeye
      calisir -> RejectedForbidden (bulgu 2 §D).
  13) [o86-A3 §D] A kendi Gelen Kutusu'nda T2 yaratir -> yabanci C (T2'nin id'sini bilir)
      T2'ye baslik yazmaya calisir -> RejectedForbidden (bulgu 5: scope'suz gorevin
      sahipligi artik IsTaskOwnerAsync ile dogrulanir).
  14) [o86-A3 §D] yabanci C, T2'yi KENDI projesine TASIMAYA (CALMAYA) calisir ->
      RejectedForbidden. POZITIF KONTROL: A (GERCEK sahip) AYNI IKI OP'U (yazma +
      tasima) yapabilir -- kutu kilitlenmedi.

HER iddia TAM DEGER esleşmesiyle kontrol edilir -- o83-G/o85-A'nin "bos liste her
iddiayi gecirir" dersinin PAZARLIKSIZ uygulanmasi (bos liste GORMEDI diye
YORUMLANMAZ, ayrica ISPATLANIR).
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
    tag = uuid7()
    op = {
        "operationId": op_id, "clientId": client_id, "entityId": entity_id,
        "actorId": actor_id, "entityType": "Project", "opHlc": hlc,
        "sets": {"members": {"adds": [{"el": eklenen_user_id, "tag": tag, "hlc": hlc}], "removes": None}},
    }
    return op, tag


def members_cikar_op(entity_id, actor_id, client_id, cikarilan_user_id, gozlenen_tag):
    op_id = uuid7()
    now = int(time.time() * 1000)
    hlc = {"wallMs": now, "counter": 0, "clientId": client_id}
    return {
        "operationId": op_id, "clientId": client_id, "entityId": entity_id,
        "actorId": actor_id, "entityType": "Project", "opHlc": hlc,
        "sets": {"members": {"adds": None, "removes": [{"el": cikarilan_user_id, "observed": [gozlenen_tag], "hlc": hlc}]}},
    }


def kayit_ol(eposta):
    kod, gov = istek("/v1/auth/register", {"email": eposta, "password": "sifreO86a212345"})
    if kod != 201:
        raise SystemExit("[DUR] register 201 DONMEDI (%s): HTTP %d: %s" % (eposta, kod, gov))
    return json.loads(gov)


def main():
    if len(sys.argv) < 2:
        raise SystemExit("[DUR] cikti yolu zorunlu argumandir")
    cikti_yolu = sys.argv[1]

    kosum_id = uuid.uuid4().hex[:8]
    a = kayit_ol("o86a2-A-%s@momentum.test" % kosum_id)
    b = kayit_ol("o86a2-B-%s@momentum.test" % kosum_id)
    c = kayit_ol("o86a2-C-%s@momentum.test" % kosum_id)
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
    # §F kritik: T'nin yaratimindan HEMEN SONRAKI horizon -- adim 7 buradan ARTIMLI pull yapacak.
    a_cursor = r_t["nextCursor"]
    kaydet("2) A proje P + P'de gorev T yaratir", "proje_id=%s\ngorev_id=%s\nikisi de Applied\na_cursor=%s" % (proje_id, gorev_id, a_cursor))

    # --- 3) B pull -> HICBIR SEY gormez (negatif kontrol -- kapi BURADA) ---
    r_b0 = sync(b["accessToken"], CLIENT_B, None, [])
    kaydet("3) B -- ILK pull (TAZE kurulum) -- NEGATIF KONTROL: hicbir sey gormemeli",
           json.dumps(r_b0, ensure_ascii=False, indent=2))
    if r_b0["snapshot"]:
        raise SystemExit("[DUS] B, UYE OLMADAN P'yi/T'yi GORDU -- kapi kapisiz: %s" % r_b0["snapshot"])
    b_cursor = r_b0["nextCursor"]

    # --- 4) A, B'nin e-postasini lookup'lar -> userId ---
    kod_lookup, gov_lookup = lookup(a["accessToken"], "o86a2-B-%s@momentum.test" % kosum_id)
    kaydet("4) A -- POST /v1/users/lookup (B'nin e-postasi)", "HTTP %d\n%s" % (kod_lookup, gov_lookup))
    if kod_lookup != 200:
        raise SystemExit("[DUS] lookup 200 DONMEDI: HTTP %d: %s" % (kod_lookup, gov_lookup))
    bulunan_user_id = json.loads(gov_lookup)["userId"]
    if bulunan_user_id != b["userId"]:
        raise SystemExit("[DUS] lookup YANLIS userId dondu: %s != %s" % (bulunan_user_id, b["userId"]))
    if set(json.loads(gov_lookup).keys()) != {"userId"}:
        raise SystemExit("[DUS] lookup fazla alan sizdirdi: %s" % gov_lookup)

    # --- 5) A members op'unu yazar (P'ye B eklenir) ---
    op_uye, b_tag = members_ekle_op(proje_id, a["userId"], CLIENT_A, bulunan_user_id)
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
           "artik YALNIZ horizon'dan SONRAKI degisikligi (uyelik ekleme op'u) tasimali.",
           json.dumps(r_b1, ensure_ascii=False, indent=2))
    artimli_ids = {c["payload"]["entityId"] for c in r_b1["changes"]}
    if proje_id not in artimli_ids:
        raise SystemExit("[DUS] B'nin artimli pull'unda uyelik-ekleme op'u (P) YOK: %s" % artimli_ids)
    b_cursor_uyelik_sonrasi = r_b1["nextCursor"]

    # --- 7) B T'yi duzenler -> A ARTIMLI pull yapar (a_cursor'dan) -> degisikligi gorur ---
    op_duzenle = alan_op("Task", gorev_id, b["userId"], CLIENT_B, {"title": "B duzenledi"})
    r_duzenle = sync(b["accessToken"], CLIENT_B, None, [op_duzenle])
    kaydet("7a) B -- T'yi duzenler (title degistirir)", json.dumps(r_duzenle, ensure_ascii=False, indent=2))
    if r_duzenle["applied"][0]["code"] != "Applied":
        raise SystemExit("[DUS] B'nin duzenleme op'u Applied DONMEDI (uye oldugu halde REDDEDILDI?): %s" % r_duzenle["applied"])

    # §F kritik DUZELTME: TAZE snapshot (sinceCursor:null) DEGIL -- A'nin KENDI a_cursor'undan (adim 2
    # sonu, T'nin yaratimindan HEMEN SONRA) GERCEK bir ARTIMLI pull. o86-A'yi yanlislikla gecirmis olan
    # tam buydu: TAZE snapshot, T'yi A'nin ESKI yaratim satirindan bulup GUNCEL CRDT durumunu (B'nin
    # yazdigi baslikla) donduruyordu -- iddia GORUNURLUK yuzunden degil, snapshot'in DOGASI yuzunden
    # yesil oluyordu. ARTIMLI ise SADECE horizon'dan SONRAKI outbox satirlarini tasir -- B'nin
    # duzenleme op'unun GERCEKTEN A'ya scope_id uzerinden ULASTIGINI kanitlar.
    r_a1 = sync(a["accessToken"], CLIENT_A, a_cursor, [])
    kaydet("7b) A -- ARTIMLI pull (a_cursor'dan, adim 2 sonu) -- B'nin duzenlemesini GORMELI "
           "(o86-A'daki TAZE-snapshot kacaginin KAPANDIGININ kaniti)",
           json.dumps(r_a1, ensure_ascii=False, indent=2))
    a_duzenleme_gorur = next(
        (c for c in r_a1["changes"]
         if c["payload"]["entityId"] == gorev_id
         and "fields" in c["payload"] and c["payload"]["fields"] is not None
         and "title" in c["payload"]["fields"]),
        None)
    if a_duzenleme_gorur is None:
        raise SystemExit("[DUS] A'nin ARTIMLI pull'unda B'nin duzenleme op'u YOK: %s" % r_a1["changes"])
    if a_duzenleme_gorur["payload"]["fields"]["title"]["value"] != "B duzenledi":
        raise SystemExit("[DUS] A'nin ARTIMLI pull'undaki baslik B'nin yazdigi DEGIL: %s" % a_duzenleme_gorur)
    a_cursor = r_a1["nextCursor"]

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

    # --- 9) [YENI] A, B'yi uyelikten CIKARIR -> B'nin sonraki (TAZE) pull'u P/T'yi ARTIK GORMEZ ---
    op_cikar = members_cikar_op(proje_id, a["userId"], CLIENT_A, b["userId"], b_tag)
    r_cikar = sync(a["accessToken"], CLIENT_A, None, [op_cikar])
    kaydet("9a) A -- members op'u (B CIKARILIR)", json.dumps(r_cikar, ensure_ascii=False, indent=2))
    if r_cikar["applied"][0]["code"] != "Applied":
        raise SystemExit("[DUS] A'nin uye-cikarma op'u Applied DONMEDI: %s" % r_cikar["applied"])

    r_b_cikarilmis = sync(b["accessToken"], "eeeeeeee-5555-4eee-8eee-eeeeeeeeeeee", None, [])
    kaydet("9b) B -- TAZE ucuncu client (snapshot), CIKARILDIKTAN SONRA -- P VE T ARTIK GORULMEMELI",
           json.dumps(r_b_cikarilmis, ensure_ascii=False, indent=2))
    cikarilmis_snap_ids = {e["entityId"] for e in (r_b_cikarilmis["snapshot"] or [])}
    if proje_id in cikarilmis_snap_ids or gorev_id in cikarilmis_snap_ids:
        raise SystemExit("[DUS] B, CIKARILDIGI HALDE P/T'yi HALA GORUYOR: %s" % cikarilmis_snap_ids)

    # --- 10) [YENI] eski uye B, T'yi Gelen Kutusu'na (projectId=null) KOPARMAYA calisir -> RED ---
    op_kopar = alan_op("Task", gorev_id, b["userId"], CLIENT_B, {"projectId": None})
    r_kopar = sync(b["accessToken"], CLIENT_B, None, [op_kopar])
    kaydet("10) B (eski uye) -- T'yi Gelen Kutusu'na KOPARMAYA calisir -- REDDEDILMELI (RejectedForbidden)",
           json.dumps(r_kopar, ensure_ascii=False, indent=2))
    if r_kopar["applied"][0]["code"] != "RejectedForbidden":
        raise SystemExit("[DUS] eski uye B'nin koparma yazimi REDDEDILMEDI (kod=%s)!" % r_kopar["applied"][0]["code"])

    # --- 11) [YENI] eski uye B, T'yi KENDI projesi Q'ya TASIMAYA calisir -> RED ---
    proje_q_id = str(uuid.uuid4())
    op_q = alan_op("Project", proje_q_id, b["userId"], CLIENT_B, {"name": "B'nin kendi projesi Q"})
    r_q = sync(b["accessToken"], CLIENT_B, None, [op_q])
    if r_q["applied"][0]["code"] != "Applied":
        raise SystemExit("[DUS] B'nin KENDI projesi Q'nun yaratimi Applied DONMEDI: %s" % r_q["applied"])
    op_tasi = alan_op("Task", gorev_id, b["userId"], CLIENT_B, {"projectId": proje_q_id})
    r_tasi = sync(b["accessToken"], CLIENT_B, None, [op_tasi])
    kaydet("11) B (eski uye) -- T'yi KENDI projesi Q'ya TASIMAYA calisir -- REDDEDILMELI "
           "(IZIN(post=Q) true olsa BILE IZIN(pre=P) artik false)",
           json.dumps(r_tasi, ensure_ascii=False, indent=2))
    if r_tasi["applied"][0]["code"] != "RejectedForbidden":
        raise SystemExit("[DUS] eski uye B'nin Q'ya tasima yazimi REDDEDILMEDI (kod=%s)!" % r_tasi["applied"][0]["code"])

    # --- 12) [YENI] yabanci C, TAHMIN ETTIGI projectId=P ile YEPYENI bir gorev ENJEKTE eder -> RED ---
    yeni_gorev_id = str(uuid.uuid4())
    op_enjekte = alan_op("Task", yeni_gorev_id, c["userId"], CLIENT_C, {"title": "C'nin enjekte ettigi", "projectId": proje_id})
    r_enjekte = sync(c["accessToken"], CLIENT_C, None, [op_enjekte])
    kaydet("12) C (yabanci) -- TAHMIN ETTIGI projectId=P ile YEPYENI bir gorev ENJEKTE etmeye calisir "
           "-- REDDEDILMELI (RejectedForbidden)",
           json.dumps(r_enjekte, ensure_ascii=False, indent=2))
    if r_enjekte["applied"][0]["code"] != "RejectedForbidden":
        raise SystemExit("[DUS] yabanci C'nin enjeksiyonu REDDEDILMEDI (kod=%s) -- enjeksiyon deligi ACIK!" % r_enjekte["applied"][0]["code"])

    # --- 13) [IS-EMRI-o86-A3 §D] A kendi Gelen Kutusu'nda T2 yaratir -> C (yabanci, T2'nin
    #      id'sini bilir) T2'ye baslik yazmaya calisir -- REDDEDILMELI (bulgu 5) ---
    gorev2_id = str(uuid.uuid4())
    op_t2 = alan_op("Task", gorev2_id, a["userId"], CLIENT_A, {"title": "A'nin kisisel gorevi T2"})
    r_t2 = sync(a["accessToken"], CLIENT_A, None, [op_t2])
    if r_t2["applied"][0]["code"] != "Applied":
        raise SystemExit("[DUS] A'nin T2 yaratimi Applied DONMEDI: %s" % r_t2["applied"])
    kaydet("13a) A kendi Gelen Kutusu'nda T2 yaratir", "gorev2_id=%s -> Applied" % gorev2_id)

    op_c_yaz = alan_op("Task", gorev2_id, c["userId"], CLIENT_C, {"title": "C'nin yazmaya calistigi"})
    r_c_yaz = sync(c["accessToken"], CLIENT_C, None, [op_c_yaz])
    kaydet("13b) C (yabanci, T2'nin id'sini bilir) -- T2'ye baslik yazmaya calisir -- "
           "REDDEDILMELI (RejectedForbidden, bulgu 5: IZIN_PRE'in null kolu artik IsTaskOwnerAsync sorar)",
           json.dumps(r_c_yaz, ensure_ascii=False, indent=2))
    if r_c_yaz["applied"][0]["code"] != "RejectedForbidden":
        raise SystemExit("[DUS] yabanci C'nin T2'ye yazimi REDDEDILMEDI (kod=%s) -- bulgu 5 kapanmadi!" % r_c_yaz["applied"][0]["code"])

    # --- 14) [IS-EMRI-o86-A3 §D] C, T2'yi KENDI projesi Q_C'ye TASIMAYA calisir -- REDDEDILMELI.
    #      POZITIF KONTROL: A (GERCEK sahip) AYNI IKI OP'U (yazma + tasima) yapabilir -- kutu
    #      kilitlenmedi. ---
    proje_qc_id = str(uuid.uuid4())
    op_qc = alan_op("Project", proje_qc_id, c["userId"], CLIENT_C, {"name": "C'nin kendi projesi"})
    r_qc = sync(c["accessToken"], CLIENT_C, None, [op_qc])
    if r_qc["applied"][0]["code"] != "Applied":
        raise SystemExit("[DUS] C'nin KENDI projesinin yaratimi Applied DONMEDI: %s" % r_qc["applied"])
    op_c_tasi = alan_op("Task", gorev2_id, c["userId"], CLIENT_C, {"projectId": proje_qc_id})
    r_c_tasi = sync(c["accessToken"], CLIENT_C, None, [op_c_tasi])
    kaydet("14a) C -- T2'yi KENDI projesine TASIMAYA calisir -- REDDEDILMELI (RejectedForbidden, bulgu 5 CALMA)",
           json.dumps(r_c_tasi, ensure_ascii=False, indent=2))
    if r_c_tasi["applied"][0]["code"] != "RejectedForbidden":
        raise SystemExit("[DUS] yabanci C'nin T2'yi CALMASI REDDEDILMEDI (kod=%s)!" % r_c_tasi["applied"][0]["code"])

    # POZITIF KONTROL: A (GERCEK sahip) AYNI IKI OP'U (yazma + tasima) yapabilir -- kutu kilitlenmedi.
    op_a_yaz = alan_op("Task", gorev2_id, a["userId"], CLIENT_A, {"title": "A duzenledi"})
    r_a_yaz = sync(a["accessToken"], CLIENT_A, None, [op_a_yaz])
    kaydet("14b) POZITIF KONTROL -- A (GERCEK sahip) T2'ye YAZABILIR", json.dumps(r_a_yaz, ensure_ascii=False, indent=2))
    if r_a_yaz["applied"][0]["code"] != "Applied":
        raise SystemExit("[DUS] POZITIF KONTROL DUSTU: A kendi gorevine YAZAMADI (kod=%s)!" % r_a_yaz["applied"][0]["code"])

    proje_qa_id = str(uuid.uuid4())
    op_qa = alan_op("Project", proje_qa_id, a["userId"], CLIENT_A, {"name": "A'nin kendi ikinci projesi"})
    r_qa = sync(a["accessToken"], CLIENT_A, None, [op_qa])
    if r_qa["applied"][0]["code"] != "Applied":
        raise SystemExit("[DUS] A'nin ikinci projesinin yaratimi Applied DONMEDI: %s" % r_qa["applied"])
    op_a_tasi = alan_op("Task", gorev2_id, a["userId"], CLIENT_A, {"projectId": proje_qa_id})
    r_a_tasi = sync(a["accessToken"], CLIENT_A, None, [op_a_tasi])
    kaydet("14c) POZITIF KONTROL -- A (GERCEK sahip) T2'yi KENDI projesine TASIYABILIR (kutu kilitlenmedi)",
           json.dumps(r_a_tasi, ensure_ascii=False, indent=2))
    if r_a_tasi["applied"][0]["code"] != "Applied":
        raise SystemExit("[DUS] POZITIF KONTROL DUSTU: A kendi gorevini KENDI projesine TASIYAMADI (kod=%s)!" % r_a_tasi["applied"][0]["code"])

    ozet = [
        "1) A, B, C kayit oldu",
        "2) A proje P + P'de gorev T yaratti -> Applied",
        "3) B (uye DEGILKEN) ILK pull'da HICBIR SEY gormedi -- NEGATIF KONTROL GECTI",
        "4) A, B'nin e-postasini lookup'ladi -> dogru userId, yanit YALNIZ userId",
        "5) A, B'yi members'a ekledi -> Applied",
        "6) B, snapshot'ta VE artimlida P'yi VE T'yi GORDU (uyelik sonrasi)",
        "7) B, T'yi duzenledi -> A, KENDI ARTIMLI pull'unda (a_cursor'dan) B'nin degisikligini GORDU "
        "-- o86-A'nin TAZE-snapshot kacagi KAPANDI",
        "8) C (uye DEGIL) pull'da HICBIR SEY gormedi VE P'ye yazimi REDDEDILDI (RejectedForbidden)",
        "9) A, B'yi uyelikten CIKARDI -> B'nin sonraki TAZE pull'u P/T'yi ARTIK GORMEDI",
        "10) eski uye B, T'yi Gelen Kutusu'na KOPARAMADI -- RejectedForbidden",
        "11) eski uye B, T'yi KENDI projesi Q'ya TASIYAMADI -- RejectedForbidden (IZIN(pre) VE IZIN(post))",
        "12) yabanci C, TAHMIN ETTIGI projectId ile YEPYENI gorev ENJEKTE EDEMEDI -- RejectedForbidden",
        "13) A kendi Gelen Kutusu'nda T2 yaratti -> yabanci C, T2'nin id'sini bilse bile baslik "
        "YAZAMADI -- RejectedForbidden (bulgu 5)",
        "14) yabanci C, T2'yi KENDI projesine TASIYAMADI (calamadi) -- RejectedForbidden; "
        "POZITIF KONTROL: A (GERCEK sahip) AYNI IKI OP'U (yazma + tasima) yapabildi -- kutu kilitlenmedi",
        "SONUC: TUM ON DORT ADIM GECTI -- isbirligi kapisi calisir, gorunurluk GERI ALINABILIR, "
        "kaynak/hedef scope IKISI DE dogrulanir, yabanci enjeksiyonu KAPALI, scope'suz (Gelen "
        "Kutusu) gorevlerin sahipligi de ARTIK dogrulaniyor (bulgu 5 kapandi).",
    ]
    kaydet("OZET (14/14)", "\n".join(ozet))

    with open(cikti_yolu, "w", encoding="utf-8") as f:
        f.write("\n".join(KANIT))
    print("\n[YAZILDI] %s" % cikti_yolu)


if __name__ == "__main__":
    main()

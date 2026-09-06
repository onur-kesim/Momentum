# IS-EMRI-o83-D — CEVAP

1. **Permütasyon mu?** EVET. benzersiz=25 · tekrar=0 · eksik=0.

2. **İlk 5 (seen vs beklenen):**

   | i | seen[i] | beklenen[i] |
   |---|---|---|
   | 0 | v12 | v0 |
   | 1 | v13 | v1 |
   | 2 | v14 | v2 |
   | 3 | v15 | v3 |
   | 4 | v16 | v4 |

3. **Ufuk geride miydi?** `pg_snapshot_xmin(pg_current_snapshot())` = 1019 · `max(commit_xid) FROM outbox_messages` = 1012. 1019 > 1012 → HAYIR.

4. **DB ham sırası doğru muydu?** HAYIR. `SELECT ... ORDER BY commit_xid, server_seq` ham çıktısı da `seen` ile birebir aynı sırayı verdi: db[0..12] = v12..v24 (commit_xid 1000..1012) · db[13..24] = v0..v11 (commit_xid 988..999). Yazılan sütunlar (commit_xid, server_seq) v-index ile birebir monoton: v0=xid 988/seq 1 … v24=xid 1012/seq 25. Kusur OKUMADA (bu `ORDER BY commit_xid, server_seq` sorgusunun döndürdüğü sırada) — YAZMADA değil.

5. **Kaç koşumda üretildi:** 1/3.

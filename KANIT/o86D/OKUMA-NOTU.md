# KANIT/o86D — okuma notu (5 Eyl 2026, oturum 89)

Bu klasordeki trx'ler **temizlenmemistir**; kirmizilar da durur. Hangisinin ne oldugu:

| dosya | kaynak | sonuc | anlami |
|---|---|---|---|
| `00-TEMIZ-ONCE.trx` | o86-D temiz | 🟢 | denetim oncesi taban |
| `M2-kullanici-horizonu-silindi.trx` | MUTANT | 🔴 | kapi GERCEK (mutant oldu) |
| `M1-horizon-yazilmaz.trx` | MUTANT | 🔴 | kapi GERCEK (mutant oldu) |
| `99-TEMIZ-SONRA.trx` | sozde temiz | 🔴 | **GECERSIZ — asagiya bak** |
| `01-KARARLILIK-1..3.trx` | o86-D2 temiz (builder) | 🟢 6/6 ×3 | kararlilik sarti |
| `M4-kapsayici-esik-geri-alindi.trx` | MUTANT (`<=` → `<`) | 🔴 | G6 **ve** G1 birlikte oldu |
| `02-DENETIM-TEMIZ-SONRA.trx` | sozde temiz | 🔴 | **GECERSIZ — asagiya bak** |
| `03-DENETIM-ZORLA-DERLEME-1..3.trx` | o86-D2 temiz (denetci) | 🟢 6/6 ×3 | bagimsiz kararlilik |

## 🔴 Iki kirmizi neden GECERSIZ

`99-TEMIZ-SONRA` ve `02-DENETIM-TEMIZ-SONRA`, mutant turundan sonra kaynak geri alinip kosuldu.
Geri alim `Copy-Item` ile yapildi ve **PowerShell'de `Copy-Item` dosyanin eski `LastWriteTime`'ini
KORUR**. Geri alinan kaynak (19:39) derlenmis DLL'den (23:00) ESKI gorundu ⇒ **MSBuild yeniden
derlemedi** ⇒ o iki kosum hala **mutantli ikiliyi** calistirdi. sha256 dogrulamasi KAYNAGI
dogruluyordu, KOSAN IKILIYI degil.

`LastWriteTime = Get-Date` ile zorla derlendiginde ayni kaynak **6/6 yesil** verdi
(`03-DENETIM-ZORLA-DERLEME-1..3`).

**Kural:** mutant turundan sonra geri alim yalniz sha256 ile dogrulanmaz; zaman damgasi
tazelenir ve ilk temiz kosumda derleme satirinin ciktida GORULDUGU teyit edilir.
Olculen sey kaynak degil, **kosan ikilidir**.

## Not

`outbox-sorgu.txt` bu klasorde ama **o86-D ile ilgisizdir** — baska bir testin yan ciktisidir
(o83-F5 owner_id/actor_id olcumu). Bilerek commit EDILMEDI.

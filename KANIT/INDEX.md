# KANIT — dizin ve ham kanıta erişim

**Bu klasörde ne var:** her dilimin **özeti** — kabul hükümleri, denetim raporları, mutant
özetleri, okuma notları. Düşmüş denetimler (`…-DENETIMDE-DUSTU…`, `…-KILITLENEMEDI…`)
**temizlenmedi ve bilerek duruyor**: bir spec'in üç kez düşmesi bu deponun gizlediği değil
**belgelediği** bir olgudur.

**Burada ne YOK:** ham koşum logları, ekran görüntüleri, arayüz dökümleri, ölçüm betikleri,
sqlite anlık görüntüleri. Bunlar **silinmedi** — ağaçtan çıkarıldı, git tarihçesinde duruyor.

## Neden

8 Eyl 2026'daki dış denetim ölçtü: izlenen 2.172 dosyanın **1.622'si (%75)** KANIT'tı ve
bunların çoğu ham logdu. Kanıtın kendisi değerlidir, ama yeri yanlıştı: klonlayan herkes
26 MB ham log indiriyordu ve dilim özetleri o yığının içinde kayboluyordu.

| | önce | sonra |
|---|---|---|
| KANIT'ta izlenen dosya | 1.622 | **144** |
| Bunun ürün+belge içindeki payı | %75 | **%10** |

**Ham kanıtın son bulunduğu commit: `964603d`.** O commit ve öncesi hiç değişmedi.

## Ham kanıta nasıl ulaşılır

Bir dilimin ham dosyalarını listele:

```
git ls-tree -r --name-only 964603d -- KANIT/o86F
```

Tek bir dosyayı oku (çalışma ağacına yazmadan):

```
git show 964603d:KANIT/o86F/ui-T2-B2-03-DAVETTEN-ONCE.xml
```

Bir dilimin tamamını geri getir:

```
git checkout 964603d -- KANIT/o86F
```

🔴 **Özetlerin içindeki dosya adları hâlâ geçerlidir.** Bir `.md` özeti
`03-mutant-kosumlari.txt` diyorsa o dosya vardır — ağaçta değil, `964603d`'de. Yukarıdaki üç
komuttan biriyle alınır. Kırık başvuru değildir; **arşivlenmiş** başvurudur.

## Ağaçta bilerek bırakılanlar

Teslim belgelerinin (`README.md`, `SURUM-NOTU-v1.1.0.md`, `docs/BRIFING.md`) **adıyla andığı**
`.md` olmayan on dosya, okuyucu tek tıkla görebilsin diye ağaçta bırakıldı:

- `A11/03-MUTANT-OZET.txt` · `A13/04-MUTANT-statik/OZET.txt`
- `o71/03-MUTANT-OZET.txt` · `o71/15-verify-CVE-pin-sonrasi.txt` · `o71/16-pages-demo/01-mutant-kosumlari.txt`
- `o72/03-MUTANT-M-o72-3.txt` · `o72/03-MUTANT-OZET.txt`
- `o80/03-MUTANT-OZET.txt` · `o81/01-aspnet-pin-olcumu.txt`
- `o86F/ui-T2-B2-04-DAVETTEN-SONRA-10sn.xml`

## Yeni dilimler için kural

Ham log ağaca **girmez**. Dilim biterken KANIT'a yalnız tek özet yazılır: hedef · kabul
ölçütü · kapı beyanı (commit sha + CI koşum no) · sonuç. Ham çıktı gerekiyorsa CI
artefaktına yüklenir ve özette koşum numarasıyla anılır.

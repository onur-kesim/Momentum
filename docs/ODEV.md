# ÖDEV — Momentum (sözlü brief, yazıya geçirilmiş)

> **Bu belge şirketin verdiği bir doküman DEĞİLDİR.** Ödev **sözlü** verilmiştir ve yazılı kapsam
> **gelmeyecektir** (bilinçli: karşı taraf hayal gücünü de görmek istiyor). Bu dosya, Onur'un
> 20 Tem 2026'da Cowork'e yaptığı **aktarımın** yazıya geçirilmiş hâlidir.
> **Kapsam otoritesi budur** — hafızadaki türev cümleler değil. Çelişkide bu dosya geçerlidir.
> Onur hatırladıkça güncellenir; her güncelleme tarihli olarak eklenir.

---

## 1. Sözlü brief — Onur'un aktarımı (20 Tem 2026)

- Önemli bir yazılım şirketinden gelen **işe-alım / portfolyo ödevi**.
- İstenen: **çok platformlu görev yönetimi (to-do) uygulaması** — Flutter istemci + N-katmanlı
  .NET 9 / ASP.NET Core backend + PostgreSQL.
  > **⏱ ERRATA [16 Ağu 2026 — o79]:** brief'te söylenen **.NET 9**'dur ve bu satır o sözlü aktarımın
  > kaydıdır; **değiştirilmemiştir**. Ancak fiilen koşan sürüm **net10.0**'dır (`src/backend/
  > Directory.Build.props`, K111 ölçülmüş geçiş, oturum 48) ve SDK `global.json`'da **10.0.302**
  > ile pinlidir. Teslim belgelerinde (README, imaj) **net10.0** yazılır; ödevin teknoloji şartı
  > "modern .NET" olarak karşılanmıştır, düşürülmemiştir.
- **Yazılı kapsam verilmedi ve verilmeyecek.** Gerekçe (Onur'un aktarımı): karşı taraf
  **adayın hayal gücünü / kapsam kurma yeteneğini** de değerlendirmek istiyor.
  ⇒ **Kapsamı biz tanımlıyoruz ve bu tanımın kendisi değerlendirmeye tabidir.**
- Onur oturum 1'de Cowork'e **bir örnek uygulama gösterdi**; o uygulamanın **çalışması/etkileşimi**
  çok beğeni toplamıştı ve "o tarzda bir şey" istedi. *(Uygulamanın adı kayıtta yok — §5'e bakınız.)*
- **Çevrimdışı çalışma AÇIKÇA istendi:** *"TickTick gibi çevrimdışı çalışsın, sonra senkron olsun."*
- Özet cümle (Onur'un kendi ifadesi): *"piyasadaki en beğenilen muadilleriyle aynı özellikte, ama
  bize has aşamalardan geçirilip belli bir disiplin içinde oluşturulmuş, **mimari ve kod kalitesi
  yüksek** bir şey."*

## 2. Değerlendirme biçimi [Onur teyit etti, 20 Tem 2026]

**Kesinlikle çalışan bir uygulama olacak. Önce uygulamaya bakılacak, sonra mimari ve kod.**

Bu, kapsam kararlarının **birinci ölçütüdür**: görünür ve çalışan ürün olmadan mimari derinliği
tek başına değerlendirilmeyecektir. *(Doğrudan sonucu için bkz. §6.)*

## 3. Süre

**2-3 hafta** — Onur'un kendi tahmini (Tuzak Avcısı projesine harcadığı süreyle kıyaslayarak).
**Şirketin verdiği bir tarih DEĞİLDİR**; iç hedeftir ama bağlayıcı kabul edilir.

Başlangıç: 18 Tem 2026. ⇒ İlk hedef teslim aralığı: 1–8 Ağu 2026.

### 3.1 GÜNCELLEME [Onur, 20 Tem 2026 — K7-b]: hedef ~**15 Ağu 2026**

Cowork'ün iş kırılımı 1–8 Ağu penceresine (**12-19 gün**) karşı **17-23 gün** iş gösterdi
(kapı yükü dâhil; geçmiş dilimlerde ~%30). Onur **kapsamı sabit tutup takvimi esnetmeyi** seçti.
*Reddedilen:* kesme sırasını şimdi kilitleyip alttan düşürmek · kapsam ve takvimi birlikte sabitleyip
kapı yükünü düşürmek.

**Bu bir TAHMİNDİR, ölçüm değildir** — ve esnetilen tarih şirketin verdiği bir tarih olmadığı için
esnetme maliyeti dışsal değildir. **Ancak §8(2) hâlâ açıktır: ödevin ne zaman beklendiği bilinmiyor.**

Plan [TAHMİN]: 21-23 Tem ~~`slice-3a-auth`~~ · 24 Tem-5 Ağu `slice-3b` (Flutter) ·
6-8 Ağu gerçek zamanlı işbirliği istemcisi · 9-10 Ağu NLP hızlı ekleme ·
11-12 Ağu CI/CD + paketleme · 13-15 Ağu tampon + teslim paketi. **Tampon yalnız 3 gün.**

### 3.2 ERRATA + DURUM [26 Tem 2026, oturum 25 — K34; kapı-6 §7(5) bulgusunun karşılığı]

**(a) DİLİM ADI DÜZELTİLDİ:** yukarıdaki plandaki `slice-3a-auth` **BAYATTIR** → doğrusu **`slice-3c-auth`**
(K14-h, 25 Tem 2026). Gerekçe: `slice-3a` adı **zaten kullanılıyordu** (ADR 0002 K2-I3'ün sunucu
materyalizasyon dilimi; `KANIT/slice-3a/` dolu) ve `slice-3b` = Flutter istemci.
*Bilinen ve kabul edilen kusur: kimlik dilimi 3b'den ÖNCE koşuyor ⇒ harf sırası kronolojiyi yalanlıyor.*
**§4.1'deki `slice-3a` ifadesi DOĞRUDUR ve dokunulmamıştır** — orası materyalizasyon dilimini kastediyor.

**(b) PLAN TAKVİMİ FİİLEN AŞILDI — ölçüm, kesme önerisi DEĞİL (K12-b):** `slice-3c-auth` penceresi
**21-23 Tem**'di; bugün **26 Tem** ve dilim **hâlâ kilitli değil**. Harcanan: ADR 0003 için
**6 yazım turu + 6 kapı turu**. Kanonik sürüm **v6**, hüküm **KİLİTLENEMEZ** (9 bloker · 31 majör · 14 minör).
Kalan: v7 yazımı + aracın 2. onarımı (ayrı el) + 7. kapı turu + ADR 0004 + spec + build + doğrulama.
**15 Ağu hedefi ağır baskı altındadır.** K12-b yürürlükte: **ölçüm raporlanır, kapsam kesilmez.**

## 4. Kapsam kilidi [Onur, 20 Tem 2026]

**ÇEKİRDEK PARİTE + 2 VİTRİN.**

**(a) Çekirdek parite — muadillerinin standart özellik seti:**
görev · liste · proje · etiket · öncelik · tarih/son tarih · tekrar (RRULE) · hatırlatıcı ·
arama · karanlık mod.

**(b) İki taç mücevher [İKİSİ BİRDEN]:**
1. **Çevrimdışı-öncelikli senkron + çakışma çözümü** *(brief'te açıkça istendi)*
2. **Gerçek zamanlı işbirliği**

**(c) Farklılaştırıcılar: yalnız 1-2 tanesi.** Havuz: NLP hızlı ekleme · Kanban+Takvim görünümü ·
time-blocking + Google Takvim · AI asistan. *(Önceki "dördü de" kararı bu kilitle daraltılmıştır.)*

### 4.1 SEÇİM [Onur, 20 Tem 2026 — K7-a]: **YALNIZ NLP HIZLI EKLEME**

Havuzdan **bir** farklılaştırıcı yapılacaktır: *doğal dille hızlı görev ekleme*
(ör. `"yarın 17:00 rapor gönder #iş !p1"` → tarih + etiket + öncelik ayrıştırması).

**Seçim gerekçesi:** §5'e göre Todoist'in **en çok övülen etkileşimi** budur ⇒ §2'nin
"önce uygulamaya bakılacak" ölçütüne en doğrudan cevap. Ayrıca ayrıştırıcı **saf ve
deterministiktir** ⇒ property + mutant kapısı kurulabilir (bu projenin kapı doktrini geçerli kalır),
ve **backend değişikliği sıfırdır** — tarih/öncelik/etiket/RRULE alanları slice-3a'da zaten
materyalize ediliyor.

**Elenenler ve gerekçeleri [adlandırılmış]:**

| aday | eleme gerekçesi |
|---|---|
| **AI asistan** | LLM çağrısı deterministik değil ⇒ **mutantla ısırtılamaz**; ya kapısız kalır (doktrin deliği) ya da kapı tiyatrosu doğurur. Ayrıca API anahtarı gerektirir ve **çevrimdışı vitriniyle çelişir**. |
| **Time-blocking + Google Takvim** | OAuth + dış API + token saklama; **ikinci bir senkron/çakışma problemi** açar. Değerlendiricinin makinesinde hesap/ağ olmadan çalışmaz ⇒ §2'nin "**kesinlikle çalışan uygulama**" ölçütüne doğrudan tehdit. |
| **Kanban + Takvim görünümü** | Elenmesi kalite değil **takvim** gerekçesiyledir: görsel etkisi en yüksek aday ve `boardPos`/fractional pos backend'de hazır, ama drag-drop + takvim widget'ı ~2-3 gün yer, widget testleri kırılgan, a11y kapısı zorlaşır. **Tampon açılırsa ilk geri çağrılacak adaydır.** |

**Kapsam dışı [adlandırılmış]:** iOS fiili cihaz testi (Mac yok → yalnız CI'da derlenir) ·
AI asistan · Google Takvim entegrasyonu · Kanban/Takvim görünümü.

## 5. Referans uygulamalar [Cowork araştırdı, 20 Tem 2026]

Onur örnek uygulamanın adını hatırlamadı; *"en çok indirme/beğeni alan hangisiyse o"* dedi.
Araştırma sonucu:

| rol | uygulama | gerekçe |
|---|---|---|
| **Birincil referans** | **Todoist** | En yaygın kullanım (**50M+ kullanıcı**), karşılaştırma kaynaklarında "en iyi genel seçim". En çok övülen etkileşimi **doğal dille hızlı görev ekleme** — "çalışması beğeni topladı" tarifine en çok uyan davranış. |
| **İkincil referans** | **TickTick** | Onur'un **çevrimdışı davranış** için adıyla verdiği referans. Puanlarda lider (iOS ~4.8 / Play ~4.5); takvim + Pomodoro ile "hepsi bir arada" konumu. |

Liderlerin App Store puanları **4.5–4.8** bandında.

**DÜRÜSTLÜK BEYANI:** bu rakamlar **ikincil karşılaştırma kaynaklarından** derlenmiştir, resmî mağaza
verisi değildir. Ayrıca **Onur'un oturum 1'de gösterdiği uygulamanın bu ikisinden biri olduğu
DOĞRULANMAMIŞTIR** — seçim, Onur'un "en çok indirme/beğeni alan" talimatına göre yapılmıştır.

## 6. Bu belgenin plan üzerindeki doğrudan sonucu

§2 (**önce uygulama**) + §3 (**2-3 hafta**) + bugünkü durum (**backend %100 kanıtlı, istemci %0**)
birlikte okunduğunda:

- **Kritik yol = slice-3b (Flutter istemci).** Backend'in kanıt zinciri güçlü ve yeterli olgunlukta.
- ~~**Backend'de kalan işler zaman kutulanmalıdır.** Süresi ölçülemeyen bir onarım turu, çalışan
  uygulamanın önüne geçemez.~~
  > **⏱ ERRATA [Onur, 26 Tem 2026 — K34-a]: BU CÜMLE YÜRÜRLÜKTEN KALKTI. Yürürlükteki kural K13-a'dır:
  > *"bloker sıfırlanana kadar tur; tur sayısı raporlanır, SINIRLANMAZ."***
  > *Gerekçe [ölçüldü]:* bu cümle **20 Tem** tarihlidir; **aynı dosyanın** §8(2) **K12 güncellemesi 25 Tem**
  > tarihlidir ve Onur orada süreye göre kapsam/derinlik kesilmesini **açıkça reddetmiştir**
  > (*"süreye göre bir şeylerden kısıp hızlı gideceğim diye hata yapmanı istemiyorum"* ⇒ **kalite > hız**).
  > Bu dosyanın kendi başlığı *"her güncelleme tarihli olarak eklenir"* der ⇒ **sonraki tarihli hüküm geçerlidir.**
  > ⇒ Kapı-6'nın *"ODEV §6 ⟷ K13-a aynı anda yürürlükte olamaz"* bulgusu **doğruydu**, ama çelişki
  > ODEV ⟷ hafıza değil, **bu dosyanın kendi içinde bayat kalmış bir satırdı.**
  > *Bedel [adlandırılmış, gizlenmiyor]:* **v6→v7 döngüsünün matematiksel bir ÜST SINIRI YOKTUR.**
  > Sonlanma yalnız *"bloker sıfırlanana kadar"* koşuluna bağlıdır. Bu, §2'nin *"kesinlikle çalışan
  > uygulama"* ölçütüyle gerçek bir gerilim taşır ve **§3.2(b)'nin takvim ölçümü her turda raporlanır.**
  > *Reddedilenler [adlandırılmış]:* ODEV'in üstün tutulup ADR'ye zaman kutusu konması (kilit ölçütü
  > ilk kez gevşer, emsal doğar) · kilit ölçütünün daraltılması (yalnız kör kapı + inşa edilemezlik
  > turu uzatır — *"bloker"* tanımı tur ortasında değişir, kapı-1…6'nın tüm blokerleri geriye dönük
  > yeniden sınıflandırılmak zorunda kalır ve iki standart doğar).
- **YERİNE GEÇEN ÖLÇÜT [K34-a]:** adlandırılmış ve KANIT'ta açıkça beyan edilmiş bir kapı zayıflığı,
  **olmayan bir istemciden daha az zarar verir**. Bu projenin doktrini zaten *"beyan edilmiş sınır
  kabul edilebilir, gizlenmiş sınır edilemez"*dir. Süre baskısı **ölçülür ve raporlanır** (§3.2(b)),
  ama **tek başına kesme gerekçesi olmaz** (K12-b).

### 6.1 GÜNCELLEME [Cowork kararı, 20 Tem 2026 — K7-c]: kritik yolun önüne **ince kimlik dilimi**

Kritik yol slice-3b olarak kalır, ama önüne **~~`slice-3a-auth`~~ → `slice-3c-auth`**
(o günkü TAHMİN: **1,5-2 gün**) konur. *(Onur kararı Cowork'e bıraktı; gerekçe kayda geçirilmiştir.)*

> **⏱ ERRATA + GERÇEKLEŞEN [26 Tem 2026, oturum 25 — K34]:**
> **(a) AD:** `slice-3a-auth` **BAYATTIR** → `slice-3c-auth` (K14-h). Bkz. §3.2(a).
> **(b) BÜTÇE — "1,5-2 gün" TAHMİNİ TUTMADI ve bu ölçümle kayda geçiyor:** dilim
> **18 Tem'de değil 25 Tem'de** başladı ve **bugün 26 Tem itibarıyla hâlâ kilitli değildir.**
> Fiilen harcanan: **ADR 0003 için 6 yazım turu + 6 kapı turu** (v1 24,7 K → v6 **241.689 bayt · 1.123 satır**),
> artı **ADR 0004 hiç yazılmadı** ve **`GOREV-slice-3c-auth` spec'i hiç yazılmadı.**
> ⇒ Bugünkü dürüst tahmin **1,5-2 gün DEĞİL**; kalan iş: v7 + aracın 2. onarımı (ayrı el) + 7. kapı turu
> + ADR 0004 (yazım + ≥1 kapı) + spec + build + Cowork doğrulaması. **[Bu bir TAHMİN değil, GERÇEKLEŞEN
> ölçümdür; kalan kısım tahmindir ve sayı verilmemektedir — K13-a gereği tur sayısı öngörülemez.]**
> **(c) SEBEP [adlandırılmış, gizlenmiyor]:** tahmin, kimlik dilimini **bir kodlama işi** sayıyordu;
> gerçekte K8-a ile **tam aktivasyon** kapsamı seçildi (dört ertelenmiş gereksinim + hub kimliği +
> `outbox.owner_id`) ve belge **altı bağımsız kapı turu** yedi. **Bu, kalite tercihinin ölçülmüş bedelidir**
> (K12-b: kalite > hız) — bir tahmin hatası olarak değil, **kilitlenmiş bir kararın maliyeti** olarak okunmalıdır.

**Gerekçe:** kimlik burada bir özellik değil **şema kararıdır** — çevrimdışı-öncelikli istemcide
"bu yerel satır kimin", "token nerede duruyor", "401 gelince kuyruktaki yazımlar ne oluyor",
"çıkışta yerel DB'ye ne oluyor" soruları Drift şemasını ve depo katmanını belirler; 3b'den sonraya
bırakmak migration + yeniden yazım demektir. Ayrıca **gerçek zamanlı işbirliği vitrini iki gerçek
kullanıcı ister** ve F5 kilidi (`owner_id` kimliği doğrulanmış actor'dan) ancak böyle fiilen
devreye girer — bugün `WireOp.ActorId` **istemci-beyanlıdır**.

**"Tam auth" bilinçli olarak seçilmedi.** Parola sıfırlama / e-posta doğrulama demoya sıfır katkı
yapar ama SMTP + sır yönetimi + dış bağımlılık getirir — §4.1'de AI asistan ve Google Takvim'i
eleyen risk sınıfının aynısı. **Yenileme token'ı yine de dâhildir:** çevrimdışı istemcide erişim
token'ı süresi dolduğunda kuyruktaki yazımların kaybolmaması mimari zorunluluktur, konfor değil.

**Auth kapsam dışı [adlandırılmış]:** parola sıfırlama · e-posta doğrulama · OAuth/sosyal giriş ·
2FA · RBAC (paylaşım yetkisi işbirliği diliminde asgari düzeyde ele alınır).

## 7. Kapsamın kökeni — ne nereden geldi [şeffaflık]

| madde | kaynak |
|---|---|
| Çok platformlu Flutter + .NET 9 + PostgreSQL | **Onur** (sözlü brief) |
| Odak = mimari & kod kalitesi | **Onur** (sözlü brief) |
| **Çevrimdışı çalışma + sonradan senkron** | **Onur** (*"TickTick gibi"*) |
| Muadilleriyle özellik paritesi | **Onur** (sözlü brief) |
| Gerçek zamanlı işbirliğinin ikinci vitrin olması | **Cowork önerisi** (oturum 1 pazar araştırması), Onur onayladı |
| Dört farklılaştırıcı | **Cowork önerisi** (oturum 1), Onur onayladı → §4(c) ile 1-2'ye daraltıldı |
| **Mekanizma derinliği** (HLC + katmanlı CRDT + bağımsız oracle + property + 16 mutant) | **Cowork kararı** (ADR 0001/0002). Brief "çevrimdışı çalışsın" diyordu; **bu derinlik brief'te istenmedi**, mimari kalite ölçütüne cevap olarak seçildi. |

*Son satır kayda geçmiştir: özellik seçimi brief'ten, mühendislik derinliği Cowork'ten gelmiştir.*

## 8. Açık kalemler

1. Örnek uygulamanın adı — Onur hatırlarsa §5 güncellenir.
2. **Teslim biçimi** (repo linki mi, paketlenmiş build mi, canlı sunum mu) — sorulmadı/söylenmedi.
   **AÇIK VE ÖNEMLİ:** §3.1 takvimi esnetti; ödevin ne zaman ve hangi biçimde beklendiği hâlâ
   bilinmiyor. Öğrenilirse §3 ve teslim paketi doğrudan etkilenir.
   **GÜNCELLEME [Onur, 25 Tem 2026 — K12]:** Onur 3 haftalık **iç** çerçeveyi teyit etti
   (*"3 haftamız var ona göre ama hızlı da gidebilir erken de bitirebiliriz"*) ve **süreye göre
   kapsam/derinlik kesilmesini açıkça reddetti** (*"süreye göre bir şeylerden kısıp hızlı gideceğim
   diye hata yapmanı istemiyorum"*). ⇒ Öncelik sırası **kalite > hız**. **Bu YENİ DIŞ BİLGİ DEĞİLDİR:**
   şirketten gelen bir tarih hâlâ yok ve **teslim biçimi hâlâ bilinmiyor** ⇒ bu madde AÇIK kalır.
3. ~~Farklılaştırıcılardan hangi 1-2'sinin yapılacağı~~ → **KAPANDI (20 Tem 2026):** yalnız
   **NLP hızlı ekleme**, bkz. §4.1.
4. CI/CD (GitHub Actions) — ADR 0001'in "yayına götürme sürecini biliyoruz" vaadi, henüz kurulmadı.
   Plana girdi: 11-12 Ağu (§3.1).
   **GÜNCELLEME [Onur, 25 Tem 2026 — K22-a]: PAKETLEMENİN BİR PARÇASI `slice-3c-auth`'a ÖNE ÇEKİLDİ.**
   Ölçüldü (oturum 21): repoda **`Dockerfile` YOKTUR** ve `docker-compose.yml` yalnız `postgres:17-alpine`
   tanımlıyor ⇒ ADR 0003'ün `KON` (konteyner) test seviyesi ve geliştirme bootstrap'ı **var olmayan bir
   artefakta** çıpalıydı. **Karar:** API `Dockerfile` + giriş betiği + compose `api` servisi **kimlik
   diliminde** yazılır. *Gerekçe:* değerlendirici uygulamayı **tek komutla** (`docker compose up`) ayağa
   kaldırır ⇒ **§2'nin "kesinlikle çalışan uygulama; önce uygulamaya bakılacak" ölçütüne doğrudan hizmet.**
   **CI/CD'nin kendisi (GitHub Actions) 11-12 Ağu'da kalır** — bu madde AÇIK; `KON` testleri o güne kadar
   **yerelde** koşulur ve KANIT'a yazılır (ADR 0003 §3.2(7)'de adlandırılmış sapma olarak yazılıdır).
   **KAPANDI [ölçüldü, 5 Eyl 2026]:** üç iş akışı canlı — `ci` · `paket` · `pages`. Son yeşil koşum
   `ci #81` ve `paket #15`, ikisi de `c3dfe14` (sha'lar run kayıtlarının KENDİ sayfalarından okundu).
5. **Auth'un ürün-içi görünürlüğü** — §6.1 ince kimlik dilimini kilitledi, ama çok-kullanıcılı
   *paylaşım* (liste/proje davet akışı) gerçek zamanlı işbirliği diliminde tasarlanacak; bugün
   kapsamı belirsiz.

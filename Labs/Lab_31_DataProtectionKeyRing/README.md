# Lab_31 — Data Protection anahtar halkası

İlgili yazı: [#31 — Data Protection](https://blog.furkantural.com/Home/Post/31)

## İddia

Her senaryoda aynı **5 yük** korunuyor ve ardından bir süreç yeniden başlıyor: aynı
yapılandırmayla yeni bir sağlayıcı kuruluyor. Yük hiç değişmiyor, korunma anı hiç
değişmiyor. Değişen tek şey anahtar halkasının **nerede durduğu ve kime ait olduğu**.

Ölçülen şey çözülebilen yük sayısı. Oturum çerezi de sahtecilik jetonu da bu yoldan
geçtiği için sayı doğrudan şunu söylüyor: geri dönüşümden sonra kaç kullanıcı içeride.

## Kanıt

```text
Senaryo                                                    Çözülen  Beklenen      Süre     Bellek  Sonuç
─────────────────────────────────────────────────────────  ───────  ─────────  ───────  ─────────  ─────
1) Bellekteki halka, süreç yeniden başladı                       0  = 0          24 ms      59 KB  GEÇTİ
2) Kalıcı halka, süreç yeniden başladı                           5  = 5          35 ms     224 KB  GEÇTİ
3) Kalıcı halka, uygulama adı değişti                            0  = 0          13 ms     170 KB  GEÇTİ
4) Kalıcı halka, amaç dizgesi değişti                            0  = 0          11 ms     159 KB  GEÇTİ
5) Yeni anahtar üretildi, eskisi duruyor                         5  = 5          21 ms     249 KB  GEÇTİ
6) Anahtar iptal edildi, süreç yeniden başladı                   0  = 0          31 ms     265 KB  GEÇTİ
7) Anahtar klasörü yayında silindi                               0  = 0          24 ms     227 KB  GEÇTİ
8) Eski işçi, yeni işçinin anahtarıyla korunan yükte             5  = 5          21 ms     309 KB  GEÇTİ
9) Anahtar iptal edildi, eski işçi çalışmaya devam ediyor        5  = 5          13 ms     106 KB  GEÇTİ
10) Halka DPAPI ile şifreli, okuyanda ayar yok                   5  = 5          13 ms     213 KB  GEÇTİ
```

Okuma notları:

- **1 ve 2, tablonun sebebi.** İki senaryo arasındaki tek fark halkanın diske yazılıp
  yazılmaması. Kod aynı, yük aynı, çerez süresi dolmamış, veri tabanı yerinde — ama
  birinde beş yükün beşi gidiyor, diğerinde beşi de duruyor. Dağıtım sonrası toplu
  çıkışın adresi burası; bir çerez ayarı değil, **anahtarın adresi**.
- **3, uygulama adının neden sabit bir dizge olması gerektiği.** Dosyalar aynı klasörde,
  anahtar yerinde, yine de hiçbir yük açılmıyor: `SetApplicationName` amaç zincirinin
  kökü. Adı içerik kökü yolundan türetmek, siteyi başka bir klasöre taşıdığın gün
  halkayı öldürmek demektir.
- **4, aynı dersin ikinci yarısı.** Aynı halka, aynı uygulama adı, tek fark
  `CreateProtector`'a verilen dizge. Koruma alanını tanımlayan şey anahtar değil
  **amaç zinciri**; halkayı paylaşmak tek başına bir şey satın almıyor.
- **5, süresi dolmak ile kaybolmak aynı şey değil.** Halkaya yeni ve aktif bir anahtar
  eklendi, yeni yükler onunla korunacak. Eski beş yük hâlâ çözülüyor, çünkü eski anahtar
  halkada duruyor. "90 gün doldu, herkes çıkış yapar" diye bir kural yok.
- **6, iptalin gerçek bedeli.** Anahtar dosyası silinmedi, yalnızca iptal işaretlendi;
  yeni süreçte beş yükün hiçbiri açılmıyor. Silmek de iptal etmek de geri dönüşsüzdür.
- **7, yapılandırması kusursuz görünen başarısızlık.** Klasör doğru verilmişti ama yayın
  onu sildi. Yeni süreç klasörü yeniden kurup sıfırdan anahtar üretiyor ve sonuç 1.
  senaryoyla birebir aynı. Anahtar klasörü yayın paketinin **dışında** kalmalı.
- **8, yaygın bir korkunun çürüdüğü yer.** Halka önbelleğe alınıyor, bu doğru. Ama
  **bilinmeyen bir anahtar kimliği onu tazeletiyor**: eski işçi, kendisi başladıktan
  sonra üretilmiş bir anahtarla korunan beş yükü de açıyor. Örtüşen geri dönüşümde
  oturum kaybının sebebi önbellek değil; koşul yalnızca iki işçinin aynı depoyu okuması.
- **9, 8'in sınırı ve tablonun en keskin çifti.** 6 ile aynı iptal, tek fark okuyanın
  halkasını iptalden **önce** yüklemiş olması. Beşi de hâlâ açılıyor: bilinmeyen anahtar
  halkayı tazeletir, **bilinen ama iptal edilmiş** anahtar tazeletmez. 6 ve 9 yan yana
  şunu söylüyor: iptal çalışan süreçlerde anında geçerli değil. Bir oturumu gerçekten
  o anda kapatmak isteyen uygulama bunu kendi durumunda tutmak zorunda.
- **10, at-rest şifrelemenin bedeli nerede değil.** Halka diske şifreli yazıldı, okuyan
  taraf için hiçbir şey yapılandırılmadı ve beşi de çözüldü: çözücü XML'in içinden
  seçiliyor. Bedel aynı makinede görünmüyor — makine kapsamlı şifrelenmiş bir halka
  başka bir fiziksel makineye taşınırsa okunamaz, ve o bedel bu düzenekte ölçülemez.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekmez. Laboratuvar her senaryo için geçici klasörde kendi anahtar deposunu
kuruyor ve çıkışta siliyor; 10. senaryo Windows dışında DPAPI'yi atlar, o durumda 2.
senaryonun düzeneğine indiği için beklenti yine 5'tir.

## Neden bu sayılar sabit

Beklentiler ölçümden değil Data Protection'ın sözleşmesinden geliyor ve koda gömülü:

- kalıcı yer yoksa anahtarlar süreçle gider → 0
- klasöre yazılan halka yeni süreçte okunur → 5
- uygulama adı ve amaç dizgesi amaç zincirinin parçası → 0, 0
- yeni anahtar eskisini silmez → 5
- iptal yeni süreçte okumayı bitirir → 0
- silinen klasör halkayı sıfırlar → 0
- bilinmeyen anahtar kimliği halkayı tazeletir → 5
- bilinen-iptal anahtar tazeletmez → 5
- at-rest çözücüsü XML'den seçilir → 5

Her senaryo kendi geçici klasörünü alır, bu yüzden senaryolar birbirinin halkasını hiç
görmez ve sıraya bağlı değildir. Çözülen sayılırken istisnanın yokluğu yeterli
sayılmıyor: korunan metnin **birebir** geri gelmesi aranıyor, böylece yanlış anahtarla
"başarılı" bir çözme sayıya karışmıyor.

**8. senaryonun beklentisi yanlış bir gerekçeyle yazılmıştı ve düzeltilen şey beklenti
değil gerekçeydi.** İlk kurguda 0 bekleniyordu: halka 24 saate kadar önbellekte
tutulduğu için eski işçinin yeni anahtarı göremeyeceği varsayılmıştı. Ölçüm 5 verdi.

Buna şu itiraz yapılabilir: "yeni işçi aslında eski anahtarı kullandı, o yüzden 5 çıktı —
senaryo bir şey ölçmüyor." İtiraz izole bir sonda ile elendi. Data Protection yükünün
başlığı 4 baytlık sihirli sayı ve ardından **16 baytlık anahtar kimliğidir**; iki işçinin
ürettiği yüklerin kimlikleri okunduğunda farklı çıkıyor:

```text
eski halkanın kullandığı anahtar : 12836ec8-7a52-4391-896d-f6e21d1fb840
üretilen yeni anahtar            : df82acc6-54dc-4c96-9616-e658c72fd6ea
yeni halkanın kullandığı anahtar : df82acc6-54dc-4c96-9616-e658c72fd6ea
aynı mı                          : False
eski halka yeni yükü çözdü       : EVET
```

Yani yeni anahtar gerçekten kullanıldı ve eski işçi onu yine çözdü; mekanizma bilinmeyen
anahtar kimliğinde yapılan depo tazelemesidir. Aynı sonda 9. senaryonun mekanizmasını da
pinledi: iptal edilen anahtar önbellekte **bilindiği** için tazeleme tetiklenmiyor ve
çalışan işçi eski çerezi kabul etmeye devam ediyor. Bu iki ölçüm birlikte, tek başına
hiçbirinin söylemediği şeyi söylüyor — tazeleme bilinmeyen anahtara bağlı, iptale değil.

Sonda bu laboratuvarın düzeneğinden bağımsızdı ve tabloda yer almıyor, çünkü tablo
anahtar kimliği değil çözülen yük ölçüyor; buraya gerekçenin dayanağı olarak not edildi.

Süre ve bellek sütunları denetlenmez ve yazıda kullanılmaz. 1. senaryo ilk koşan senaryo
olduğu için Data Protection altyapısının kurulumunu üstleniyor — o sayı senaryonun
maliyeti değil, ölçüm sırasının. Denetlenen tek sayı çözülen yük adedidir. Laboratuvar
üst üste dört kez koşturuldu ve dördünde de aynı tabloyu verdi.

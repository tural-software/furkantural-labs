# Lab_32 — ETag ve If-Match

İlgili yazı: [#32 — ETag ve If-Match](https://blog.furkantural.com/Home/Post/32)

## İddia

Her senaryoda iki istemci aynı kaydı **aynı sürümden** okuyup değiştirmeye çalışıyor.
Kayıt aynı, okuma anı aynı, gönderilen gövde aynı. Değişen tek şey sunucunun ön koşulu
nasıl ele aldığı ve istemcinin elinde hangi doğrulayıcının olduğu.

Ölçülen şey kabul edilen yazma sayısı. İki yazmanın ikisi de kabul edildiyse biri sessizce
kayboldu demektir — bu tabloda **2 iyi haber değil**, tek başına bir şey de söylemiyor.

## Kanıt

```text
Senaryo                                                  Kabul edilen  Beklenen      Süre     Bellek  Sonuç
───────────────────────────────────────────────────────  ────────────  ─────────  ───────  ─────────  ─────
1) Sunucu If-Match'e bakmıyor                                       2  = 2         130 ms     508 KB  GEÇTİ
2) İkisi de aynı doğrulayıcıyı gönderiyor                           1  = 1           8 ms     154 KB  GEÇTİ
3) 412 alan istemci yeniden okuyup gönderiyor                       2  = 2           2 ms      58 KB  GEÇTİ
4) Sunucu If-Match zorunlu tutuyor, istemci göndermiyor             0  = 0           5 ms     127 KB  GEÇTİ
5) Zayıf doğrulayıcı (W/) ile If-Match                              0  = 0           1 ms      38 KB  GEÇTİ
6) If-Match: * iki istemcide                                        2  = 2           1 ms      15 KB  GEÇTİ
7) PUT yanıtı yeni ETag döndürmüyor                                 1  = 1           4 ms     150 KB  GEÇTİ
8) PUT yanıtı yeni ETag döndürüyor                                  2  = 2           1 ms      16 KB  GEÇTİ
```

Okuma notları:

- **1 ve 2, tablonun sebebi.** Tek fark sunucunun `If-Match`'i okuyup okumaması. Birinde
  iki yazma da kabul ediliyor ve kayıtta yalnız ikincisi duruyor: birinci istemci `200`
  almış, işinin kaybolduğunu öğrenmesinin hiçbir yolu yok. Diğerinde ikinci istemci `412`
  alıyor ve birincinin yazması yerinde kalıyor. Kayıp ortadan kalkmıyor, **görünür hataya
  dönüşüyor** — asıl kazanç budur.
- **3, aynı sayının zıt anlamı.** 412 alan istemci yeniden okuyup gönderdiğinde yine iki
  kabul var, ama bu kez kayıp yok: ikinci yazma birincinin ürettiği sürümün üstüne bindi.
  1. senaryoyla **birebir aynı sayı**, taban tabana zıt sonuç. Bu yüzden "kaç yazma geçti"
  tek başına ölçüt değil; ölçüt neyin üstüne yazıldığı.
- **4, 412 ile 428 neden ayrı.** İki istek de `428` aldı. `412` "gönderdiğin eşleşmedi"
  derken `428` "ön koşul göndermedin" diyor; ayrım istemciye ne yapacağını söyler. İkisini
  `412`'de birleştiren bir API, eksik başlığı bayat başlık gibi gösterir ve istemciyi
  sonsuza kadar yeniden okumaya yollar.
- **5, doğrulayıcının gücü bir detay değil.** Sürüm doğru, etiket doğru, tek fark başındaki
  `W/` — ve ikisi de `412`. `If-Match` güçlü karşılaştırma ister, zayıf doğrulayıcı hiçbir
  koşulda eşleşmez. Karşılaştırmayı laboratuvar kendi eliyle yapmıyor:
  `EntityTagHeaderValue.Compare(..., useStrongComparison: true)` çerçevenin kendi
  uygulamasıdır ve kararı o veriyor.
- **6, yıldız bir eşzamanlılık denetimi değil.** Ön koşul gönderildi, sunucu uyguladı ve
  sonuç 1. senaryoyla birebir aynı: iki kabul, bir kayıp. `If-Match: *` "bu kaynak var mı"
  diye sorar, "benim okuduğum sürüm mü" diye sormaz. Kayıp update'e karşı hiçbir şey yapmaz;
  işi, var olmayan bir kaynağa yazmayı engellemektir.
- **7 ve 8, bayatlatan şey başka bir istemci değil.** Tek istemci, üst üste iki yazma, arada
  kimse yok — ve yanıt yeni `ETag`'i taşımadığında ikincisi `412` alıyor. Sunucu ön koşulu
  doğru uyguluyor; eksik olan, yazma yanıtının yeni sürümü bildirmemesi. Tek satırlık bir
  ekleme istemciyi her turda bir `GET`'ten kurtarıyor.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekmez. Laboratuvar dört ayrı minimal uygulamayı süreç içinde ayağa kaldırır
(ön koşulu yok sayan, uygulayan, uygulayıp yanıtta `ETag` döndürmeyen ve zorunlu tutan) ve
gerçek Kestrel üzerinden gerçek HTTP konuşur.

## Neden bu sayılar sabit

Beklentiler ölçümden değil **HTTP sözleşmesinden** (RFC 9110) geliyor ve koda gömülü:

- ön koşul okunmazsa son yazan kazanır, ikisi de kabul edilir → 2
- aynı doğrulayıcıyı gönderen ikinci istemci `412` alır → 1
- yeniden okuyup gönderen istemcinin yazması da uygulanır → 2
- `If-Match` zorunluysa göndermeyen istemci `428` alır → 0
- `If-Match` güçlü karşılaştırma ister, zayıf doğrulayıcı eşleşmez → 0
- `If-Match: *` varlık denetimidir → 2
- yanıt yeni `ETag` döndürmezse istemcinin etiketi bayattır → 1
- döndürürse ikinci yazma da geçer → 2

Her senaryo **kendi kayıt anahtarını** kullanır, bu yüzden senaryolar birbirinin sürümünü
görmez ve sıraya bağlı değildir. Kurulumun gerçekten kurulduğu her senaryoda ayrıca
denetlenir ve kurulmadıysa senaryo fırlatır: iki istemcinin aynı sürümü okuduğu, 1 ve 6'da
son değerin ikinci istemciye ait olduğu, 2'de birincinin yazmasının korunduğu, 3'te iki
yazmanın gerçekten uygulandığı, 7'de yanıtın `ETag` taşımadığı, 8'de taşıdığı ve yeni
olduğu. Bu denetimler olmasa bir senaryo doğru sayıyı yanlış sebeple verebilirdi.

**5. senaryonun kendi içinde bir kontrolü var.** Zayıf doğrulayıcının reddi iki şeyden
gelebilirdi: zayıflık ya da bayatlık. Ayırmak için, ölçüm bittikten sonra aynı sürümün
**güçlü** hâliyle bir kontrol yazması gönderiliyor; o kabul edilmezse senaryo fırlatıyor.
Kontrol yazması ölçüme girmez, yalnızca redde sebep olan şeyin zayıflık olduğunu kanıtlar.

İstekler **sıra ile** gönderiliyor ve bu bilinçli: depo süreç içinde ve kilitsiz, ölçülen
şey iş parçacığı güvenliği değil HTTP sözleşmesi. Gerçek kayıp update de böyle doğar —
yazmalar sunucuda zaten sıralıdır, kaybı yaratan şey yazmaların yarışması değil **okumanın
bayatlaması**. Eşzamanlı gönderilseydi ölçülen şey sözleşme değil yarışın sonucu olurdu.

Süre ve bellek sütunları denetlenmez ve yazıda kullanılmaz. 1. senaryo ilk koşan senaryo
olduğu için barındırma ve istemci altyapısının kurulumunu üstleniyor (130 ms, 508 KB) — o
sayı senaryonun maliyeti değil, ölçüm sırasının. Denetlenen tek sayı kabul edilen yazma
adedidir. Laboratuvar üst üste dört kez koşturuldu ve dördünde de aynı tabloyu verdi.

# Lab_29 — Retry ve Idempotency

İlgili yazı: [#29 — Retry ve Idempotency](https://blog.furkantural.com/Home/Post/29)

## İddia

Kullanıcı bir kez "Sipariş ver"e basıyor; istemci **tek bir POST** gönderiyor. Sunucu her
seferinde siparişi kaydediyor, sonra bozuluyor: ya 503 dönüyor ya da cevabı istemcinin
sabrından geç yazıyor. Arıza hep kayıttan **sonra** geliyor — veritabanı commit etti,
sonra bir bildirim servisi düştü ya da bir ağ geçidi zaman aşımına uğradı.

Ölçülen şey sunucudaki sipariş adedi. Doğru cevap her senaryoda 1. Değişen tek şey
istemcinin `HttpClient` pipeline'ı: yeniden deneme var mı, POST'u ayırıyor mu, idempotency
anahtarı nerede basılıyor.

## Kanıt

```text
Senaryo                                             Sipariş  Beklenen      Süre     Bellek  Sonuç
──────────────────────────────────────────────────  ───────  ─────────  ───────  ─────────  ─────
1) Yeniden deneme yok, sunucu 503                         1  = 1          89 ms     426 KB  GEÇTİ
2) Standart handler, ilk iki deneme 503                   3  = 3         141 ms     675 KB  GEÇTİ
3) Standart handler, ilk iki deneme geç cevap             3  = 3        2096 ms     620 KB  GEÇTİ
4) Standart handler, her deneme 503                       4  = 4          88 ms     198 KB  GEÇTİ
5) Güvensiz metotlarda yeniden deneme kapalı              1  = 1           3 ms     188 KB  GEÇTİ
6) Anahtar pipeline'ın dışında, ilk iki deneme 503        1  = 1          72 ms     199 KB  GEÇTİ
7) Anahtar pipeline'ın içinde, ilk iki deneme 503         3  = 3          63 ms     196 KB  GEÇTİ
```

İstemcinin son gördüğü durum kodu, sırasıyla: 503, 201, 201, 503, 503, 201, 201.

Okuma notları:

- **1, başlangıç noktası.** Tek deneme, tek kayıt. İstemci 503 görüyor ama sipariş
  sunucuda duruyor. Hata kodu işin yapılmadığını söylemez; yalnız cevabın iyi
  gitmediğini söyler.
- **2, tuzağın kendisi.** `AddStandardResilienceHandler()` 5xx'i geçici hata sayıyor ve
  HTTP metoduna bakmıyor. Üç deneme, üçü de kaydetti. İstemci 201 görüyor: kullanıcıya
  "siparişiniz alındı" yazıyor, kartından üç kez çekiliyor.
- **3, hata kodu gerekmiyor.** Sunucu hiç hata dönmedi, yalnız yavaştı. Deneme zaman aşımı
  istemci tarafında doldu, istek yeniden gitti; sunucu bu arada ilk işi de bitirdi.
  Zaman aşımı "iş yapılmadı" değil "cevabı beklemedim" demektir.
- **4, en kötüsü.** Varsayılan üç yeniden deneme tükeniyor ve istemci sonunda 503 görüyor.
  Kullanıcı "sipariş alınamadı" okuyor, bir daha deniyor; sunucuda şimdiden dört sipariş var.
- **5, güvenli ama dayanıklı değil.** `Retry.DisableForUnsafeHttpMethods()` POST, PUT,
  PATCH ve DELETE'i yeniden denemeden çıkarıyor. Kopya yok, ama istemci yine 503 görüyor:
  geçici bir hata artık kullanıcıya kadar ulaşıyor. Yeniden denemeyi kapatmak sorunu
  çözmez, yalnız maliyetini geri kullanıcıya verir.
- **6, çözüm.** İstek bir `Idempotency-Key` taşıyor; sunucu anahtarı gördüyse işi ikinci
  kez yapmıyor. Anahtarı basan handler yeniden deneme handler'ından **önce** kaydedildi,
  yani dışarıda duruyor ve mantıksal istek başına bir kez çalışıyor. Üç deneme de aynı
  anahtarı taşıyor: tek sipariş ve istemci 201 görüyor.
- **7, anahtar yanlış yerde.** Aynı handler, aynı kod; tek fark `AddHttpMessageHandler`'ın
  `AddStandardResilienceHandler`'dan **sonra** çağrılması. Sonra kaydedilen handler
  içeride kalır ve her denemede yeniden çalışır: her deneme yeni bir anahtar, sunucu için
  yeni bir sipariş. İstemci yine 201 görüyor. Anahtar "var" olmak korumak değildir.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekmez; laboratuvar kendi sunucusunu ayağa kaldırır.

## Neden bu sayılar sabit

Beklentiler ölçümden değil **standart yeniden deneme kuralından** geliyor ve koda gömülü:
5xx ve deneme zaman aşımı geçici hata sayılır, en fazla **3** kez yeniden denenir, HTTP
metoduna bakılmaz. Sunucu her denemede önce kaydediyor, sonra bozuluyor; yani sipariş
sayısı deneme sayısına (ya da sunucunun gördüğü farklı anahtar sayısına) eşit:

- pipeline yok → tek deneme → 1
- ilk iki deneme bozuk, standart → iki bozuk + bir sağlam → 3
- aynısı zaman aşımıyla → 3
- her deneme bozuk, standart → 1 + 3 yeniden deneme → 4
- POST yeniden denenmiyor → 1
- anahtar dışarıda → üç deneme, tek anahtar → 1
- anahtar içeride → üç deneme, üç anahtar → 3

Yedisi de mekanizma doğru bağlandıktan sonra ilk koşuda tuttu. İlk koşu 2, 4 ve 7'de
kırmızı yandı ve bu laboratuvarın kendi hatasıydı: uç nokta `HttpContext` alan bir lambda
olarak verildiğinde `MapPost` onu `RequestDelegate` sanıyor ve dönen `IResult`'u çöpe
atıyordu. Sunucu 503 yerine hep boş bir 200 dönünce yeniden deneme hiç tetiklenmedi. Uç
nokta açık bir `Func<HttpContext, Task<IResult>>` olarak bağlandı; beklentilere dokunulmadı.

**Yeniden deneme sayısına dokunulmadı**; ölçülen şey varsayılanın davranışı. Varsayılan
değişirse 4. senaryo kırmızı yanar. Yalnız bekleme süresi kısaltıldı (sabit 20 ms, jitter
yok): varsayılan 2 saniyelik üstel gecikme sayıyı değiştirmez, yalnız laboratuvarı
uzatır. Deneme zaman aşımı 1 saniye, sunucunun geç cevabı 3 saniye; aradaki pay makinenin
hızından bağımsız olacak kadar geniş.

Her senaryo kendi `ServiceCollection`'ını kuruyor. İsimli istemcinin pipeline'ı ve devre
kesicinin sayaçları senaryolar arasında taşınmıyor; devre kesici zaten en az 100 istek
görmeden açılmadığı için bu ölçekte hiç devreye girmiyor.

Yeniden deneme aynı `HttpRequestMessage`'ı tekrar gönderiyor. Bu yüzden anahtar handler'ı
başlığı eklemeden önce siliyor; silmeseydi 7. senaryoda başlık üç değer taşır, sunucu
ilkini okur ve tuzak kendiliğinden kapanmış gibi görünürdü.

Süre ve bellek sütunları denetlenmez ve yazıda kullanılmaz. 3. senaryonun 2 saniyesi iki
deneme zaman aşımının toplamıdır; senaryonun maliyeti değil, kurgunun süresidir.
Denetlenen tek sayı sunucudaki sipariş adedidir.

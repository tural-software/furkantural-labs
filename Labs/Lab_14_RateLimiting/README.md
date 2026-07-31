# Lab_14 — Rate Limiting

İlgili yazı: [#14 — Rate Limiting — .NET'in Yerleşik Limiter'ı Varken Neden Hâlâ Elle Sayaç Yazıyorsunuz?](https://blog.furkantural.com/Home/Post/14)

## İddia

Her senaryoda 20 istek **aynı anda** gönderiliyor ve izin sayısı 5. Ölçülen şey, kaçının
200 aldığı.

## Kanıt

```text
Senaryo                                    Geçen  Beklenen      Süre     Bellek  Sonuç
────────────────────────────────────────  ──────  ─────────  ───────  ─────────  ─────
1) Elle sayaç, eşzamanlı                      20  6–20        149 ms     1,4 MB  GEÇTİ
2) Yerleşik, varsayılan ayar                   5  = 5          10 ms     659 KB  GEÇTİ
3) Yerleşik, pencere yenilendikten sonra       5  = 5        1030 ms     1,8 MB  GEÇTİ
4) İki istemci, ayrı partition                10  = 10          2 ms     136 KB  GEÇTİ
5) İki istemci, tek partition                  5  = 5           2 ms     200 KB  GEÇTİ
```

Okuma notları:

- **1'de limit hiç uygulanmadı: 20 isteğin 20'si de geçti.** Beklenti tek sayı değil aralık,
  çünkü yarış koşulunun sonucu tanımı gereği deterministik değildir — ama art arda
  koşumların hepsinde sonuç 20 çıktı. `Get` ile `Set` arasında kilit olmadığı için eşzamanlı
  isteklerin hepsi aynı eski sayıyı okuyor ve hepsi kendini limitin altında sanıyor. İstekler
  sıra ile gönderilseydi bu kusur hiç görünmezdi; elle yazılmış sayaçların testten geçip
  sahada çökmesinin sebebi de bu.
- **Elle yazılan sayaçta karşılaştırma bilerek yumuşatıldı.** Yazıdaki kod `count > Limit`
  yazıyor ve limitin bir fazlasına izin veriyor; laboratuvarda `>=` kullanıldı ki iki
  uygulama aynı izin sayısına sahip olsun ve ölçülen fark yalnızca eşzamanlılıktan gelsin.
- **2'de sayı doğru ama yanıt yanlış.** Reddedilen isteklerin hepsi **503** döndü ve
  `Retry-After` başlığı hiç basılmadı; ikisi de varsayılan davranış. İstemci "servis çöktü"
  sanar ve yeniden deneme mantığı yanlış çalışır. Laboratuvar bunu tabloya karıştırmadan
  senaryonun içinde doğruluyor: kod 503 dışında bir şey dönerse senaryo patlar.
- **3'te ölçülen ikinci yığındır.** Kota tüketildikten sonra pencere kapanana kadar
  beklendi; hak geri geldi. Aynı senaryoda red kodunun 429'a çekildiği ve `Retry-After`'ın
  basıldığı da doğrulanıyor — ikisi de `OnRejected` içinde elle yazılıyor.
- **4 ile 5 arasındaki tek fark partition anahtarı.** 4'te iki istemci ayrı kovalardan
  beşer hak aldı; 5'te ikisi tek kovayı paylaştı. 5, proxy arkasında `RemoteIpAddress`
  kullanmanın karşılığıdır: tüm trafik tek partition'a düşer ve gerçek kullanıcılar
  birbirinin kotasını yer.

Yazının adı geçen ama bu tabloda **ölçülmeyen** bir kusuru daha var: elle yazılan sayaçta
her `Set` çağrısı süreyi baştan başlatır, yani düzenli aralıklarla istek gönderen bir
istemcinin penceresi hiç kapanmaz. Ölçümü duvar saatine bağlı olduğu için kanıt kapısını
kırılgan hâle getirirdi; tabloya alınmadı.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekmez. Laboratuvar ~1,5 saniye sürer: bir senaryo pencerenin kapanmasını
gerçekten bekler.

## Neden bu sayılar sabit

Pencere 500 ms, izin 5. Senaryolar birbirinin kotasını yemesin diye her biri kendi
partition anahtarını kullanır; pencere beklemesi yalnız bunu ölçen senaryodadır. Yerleşik
limiter'ın sayıları eşzamanlılıktan etkilenmez — zaten kanıtlanan şey de bu.

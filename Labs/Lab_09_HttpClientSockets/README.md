# Lab_09 — HttpClient ve Socket Exhaustion

İlgili yazı: [#9 — HttpClient Yanlış Kullanımı — Socket Exhaustion ve IHttpClientFactory](https://blog.furkantural.com/Home/Post/9)

## İddia

Dört yaklaşımın dördü de aynı 20 isteği gönderiyor ve dördü de çalışıyor. Fark,
sunucunun kaç farklı **istemci portu** görmesinde — port tükenmesi buradan doğar.

Socket tükenmesini gerçekten tüketerek göstermek gerekmiyor; mekanizmayı ölçmek yeter.
Sunucu her isteğin geldiği istemci portunu not ediyor: aynı port, aynı TCP bağlantısı.

## Kanıt

```text
Senaryo                             Port  Beklenen      Süre     Bellek  Sonuç
────────────────────────────────  ──────  ─────────  ───────  ─────────  ─────
1) Her çağrıda new HttpClient         20  = 20        100 ms     980 KB  GEÇTİ
2) Paylaşılan tek HttpClient           1  = 1           4 ms      73 KB  GEÇTİ
3) IHttpClientFactory                  1  = 1           7 ms     124 KB  GEÇTİ
4) new HttpClient, ortak handler       1  = 1           3 ms      77 KB  GEÇTİ
```

Okuma notları:

- **1'de 20 istek 20 bağlantı açtı.** `using` bloğu istemciyi dispose ediyor ama
  altındaki socket hemen kapanmıyor; TCP bağlantısı `TIME_WAIT` durumunda dakikalarca
  bekliyor. Saniyede yüzlerce istek atan bir serviste bu sayı port havuzunu tüketir.
- **4, asıl suçlunun kim olduğunu söyler.** Burada da her çağrıda `new HttpClient()` var —
  ama handler paylaşıldığı için sonuç 1. Yani sorun `HttpClient`'ı new'lemek değil,
  **her seferinde yeni bir handler yaratmak**. Bağlantı havuzu handler'ın içindedir.
- **2 ile 3 aynı sayıyı verir, aynı şey değildir.** Tek statik istemci bağlantıyı sonsuza
  kadar tutar ve DNS değişikliğini fark etmez. `IHttpClientFactory` handler'ları
  havuzlar ve belirli aralıklarla tazeler: 2'nin kazancı, 2'nin sorunu olmadan.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekmez; laboratuvar hem sunucusunu hem istemcilerini kendisi kurar.

## Neden bu sayılar sabit

İstekler sıra ile gönderilir ve keep-alive açıktır: tek bir bağlantı 20 isteğe yeter.
Ölçüm sunucu tarafında yapılır (`HttpContext.Connection.RemotePort`), yani istemcinin ne
iddia ettiğine değil işletim sisteminin gerçekten açtığı bağlantıya bakılır.

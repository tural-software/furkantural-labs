# Lab_21 — Output Caching

İlgili yazı: [#21 — Output Caching — Cache Ekledim Ama Sunucu Yükü Neden Düşmedi?](https://blog.furkantural.com/Home/Post/21)

## İddia

Her senaryoda 10 istek gönderiliyor ve hepsi 200 alıyor. Ölçülen şey, **ucun kaç kez
gerçekten çalıştığı** — "sunucu yükü düştü mü" sorusunun tek dürüst karşılığı.

## Kanıt

```text
Senaryo                                 Uca ulaşan  Beklenen      Süre     Bellek  Sonuç
──────────────────────────────────────  ──────────  ─────────  ───────  ─────────  ─────
1) Sadece Cache-Control başlığı                 10  = 10        148 ms     451 KB  GEÇTİ
2) Response caching, Authorization var          10  = 10         14 ms     174 KB  GEÇTİ
3) Response caching, Authorization yok           1  = 1          12 ms      66 KB  GEÇTİ
4) Output caching                                1  = 1          34 ms     354 KB  GEÇTİ
5) Output caching, EvictByTag sonrası            2  = 2           5 ms     258 KB  GEÇTİ
6) SetVaryByQuery eksik                          1  = 1           3 ms      99 KB  GEÇTİ
```

Sayaç ucun kendi içinde artar. Yanıtın nereden geldiğini istemci tarafından anlamak
güvenilir değildir — gövde aynıdır, durum kodu aynıdır.

Okuma notları:

- **1, `[ResponseCache]` attribute'unun sunucudaki karşılığıdır.** Başlık yazıldı — senaryo
  bunu ayrıca doğruluyor — ama sunucuda kopya oluşmadı ve uç 10 kez çalıştı. Attribute bir
  talimattır: "tarayıcı ya da CDN, bunu 60 saniye sakla." Önünde o talimatı uygulayan bir
  katman yoksa sunucu yükü hiç değişmez.
- **2 ile 3 arasındaki tek fark bir başlık.** Middleware ikisinde de kayıtlı, ikisinde de
  uyarı yok. `Authorization` taşıyan istekler için tek yanıt bile saklanmadı. JWT ile
  korunan bir API'de her istek 2. satırdaki gibidir: `UseResponseCaching` orada durur,
  hiçbir işe yaramaz ve bunu size kimse söylemez.
- **4, farkın nereden geldiğini söyler.** Output caching HTTP cache kurallarına tabi
  değildir; kararı policy verir. İsabet olduğunda istek uca hiç ulaşmaz.
- **5, geçersizleştirmenin TTL beklemek olmadığını gösterir.** 20 istek gönderildi, uç 2
  kez çalıştı: tag düşürüldüğü an yanıt yeniden üretildi. Beş dakikalık süreyi beklemek
  geçersizleştirme değil, kadercilik.
- **6, performans hatası değil veri hatası.** `SetVaryByQuery("page")` yazıldı, `pageSize`
  unutuldu. İkinci istek `pageSize=50` sordu ve `pageSize=10` yanıtını aldı — laboratuvar
  iki gövdenin birebir aynı olduğunu doğruluyor. Anahtarı gevşetmek, bir kullanıcının
  yanıtını diğerine servis etmenin ilk adımıdır.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekmez; laboratuvar üç ayrı uygulamayı rastgele portlarda ayağa kaldırır.

## Neden bu sayılar sabit

İstekler sıra ile gönderilir. Eşzamanlı gönderilselerdi hepsi ilk isabetten önce gelir ve
ölçülen şey cache'in çalışıp çalışmadığı değil yarışın sonucu olurdu.

Senaryolar birbirinin anahtarını kirletmesin diye ayrı yollar ve ayrı `page` değerleri
kullanır. Sayaç her senaryodan önce sıfırlanır; ölçülen sayı, o senaryoda gönderilen
isteklerin kaçının uca ulaştığıdır.

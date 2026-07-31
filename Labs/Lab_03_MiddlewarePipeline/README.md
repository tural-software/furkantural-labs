# Lab_03 — Middleware Pipeline

İlgili yazı: [#3 — Middleware Pipeline — ASP.NET Core'da Sıralama Neden Hayati?](https://blog.furkantural.com/Home/Post/3)

## İddia

Aynı istek beş farklı boru hattından geçiyor. Sıralamayı bozmanın bedeli, yaygın olarak
söylendiği gibi istekleri **sessizce geçirmek değil**.

> ⚠️ Bu laboratuvar yazıyla çelişiyor. Yazı şunu söylüyor: *"Yanlış sırada `[Authorize]`
> attribute'u hiç çalışmaz, ama hata da vermez. Tüm istekler geçer."* Ölçüm bunun tersini
> gösteriyor. Düzeltilmesi gereken yazıdır.

## Kanıt

```text
Senaryo                              Durum  Beklenen      Süre     Bellek  Sonuç
──────────────────────────────────  ──────  ─────────  ───────  ─────────  ─────
1) Doğru sıra, kimliksiz               401  = 401        64 ms     211 KB  GEÇTİ
2) Doğru sıra, kimlikli                200  = 200        15 ms      68 KB  GEÇTİ
3) Ters sıra, kimlikli                 401  = 401         4 ms     116 KB  GEÇTİ
4) UseAuthorization yok, kimliksiz     401  = 401         1 ms      96 KB  GEÇTİ
5) Short-circuit middleware            403  = 403         2 ms      79 KB  GEÇTİ
```

Gerçek bir Kestrel ayağa kalkıyor ve gerçek HTTP konuşuluyor; durum kodları istemcinin
aldığı kodlardır.

Okuma notları:

- **2 ile 3'ü yan yana koyun.** Aynı geçerli kimlikle doğru sırada 200, ters sırada 401.
  Ters sıralama kapıyı açmıyor, **kapatıyor**: `UseAuthorization` çalıştığında
  `HttpContext.User` henüz doldurulmamıştır, kullanıcı anonim görünür ve yetkilendirme
  reddeder. Sonra çalışan `UseAuthentication`'ın bir hükmü kalmaz, çünkü zincir çoktan kesilmiştir.
- **4, `UseAuthorization`'ı hiç yazmamanın da kapıyı açmadığını gösterir.** Yetkilendirme
  servisleri kayıtlıysa `WebApplication` middleware'i kendisi ekler. Yani "unutmak" bu
  sürümde sessiz bir güvenlik açığı değil.
- **Bu, sıralamanın önemsiz olduğu anlamına gelmez.** Önemi başka: hata *sessiz* değil
  **gürültülü**. Ters sırada uygulama çalışmaya devam etmez — herkes 401 alır ve sorun
  ilk denemede görünür. Sessiz bozulma senaryosu bu değil.
- **5, zincirin gerçekten kesildiğini gösterir.** `next()` çağrılmadığında uç sayacı 0'da
  kalıyor: uç hiç çalışmadı. Bu bazen istenen davranıştır (short-circuit), bazen hatadır.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekmez; laboratuvar kendi sunucusunu rastgele bir portta ayağa kaldırır.

## Neden bu sayılar sabit

Kimlik doğrulama şeması `X-Lab-User` başlığına bakan on satırlık bir handler; JWT kurmak
sıralama sorusuna hiçbir şey katmaz. Uygulamalar ölçümün dışında başlatılır, böylece
Kestrel'in açılış maliyeti ilk senaryoya yazılmaz.

using System.Text;
using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Host;
using Lab_29_RetryIdempotency;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

// Kullanıcı bir kez "Sipariş ver"e basıyor; istemci tek bir POST gönderiyor. Sunucu her
// seferinde siparişi kaydediyor, sonra bozuluyor: ya 503 dönüyor ya da cevabı geç yazıyor.
// Ölçülen şey sunucudaki sipariş adedi. Doğru cevap her senaryoda 1.
//
// Beklentiler ölçümden değil standart yeniden deneme kuralından geliyor: 5xx ve deneme
// zaman aşımı geçici hata sayılır, en fazla 3 kez yeniden denenir, HTTP metoduna bakılmaz.
//   · pipeline yok                                  → tek deneme, 1
//   · standart, ilk iki deneme 503                  → üç deneme, üçü de kaydeder, 3
//   · standart, ilk iki deneme geç cevap            → aynı sayı, hata kodu yok, 3
//   · standart, her deneme 503                      → 1 + 3 yeniden deneme, 4
//   · POST için yeniden deneme kapalı               → tek deneme, 1
//   · anahtar pipeline'ın dışında basılıyor         → üç deneme, tek anahtar, 1
//   · aynı anahtar handler'ı pipeline'ın içinde     → üç deneme, üç anahtar, 3

const int IlkIkiDeneme = 2;
const int HerDeneme = int.MaxValue;

var defter = new SiparisDefteri();

await using var app = await LabsWebHost.StartAsync(application =>
    application.MapPost("/siparisler", (Func<HttpContext, Task<IResult>>)defter.KarsilaAsync));

var report = new LabReport(
    title: "Lab_29 — Tek sipariş, yeniden deneyen istemci: sunucuda kaç sipariş var",
    claim: "Kullanıcı bir kez tıklıyor, istemci tek bir POST gönderiyor. Sunucu siparişi " +
           "kaydettikten sonra bozuluyor. Ölçülen şey sunucudaki sipariş adedi; doğru cevap 1.",
    metric: "Sipariş");

// ── 1. Pipeline yok ──────────────────────────────────────────────────────────
await report.MeasureAsync(
    "1) Yeniden deneme yok, sunucu 503",
    Expectation.Exactly(1),
    () => SiparisVerAsync(_ => { }, Ariza.KaydedipHataDon, IlkIkiDeneme),
    note: "Tek deneme, tek kayıt. İstemci 503 görüyor ama sipariş sunucuda duruyor: " +
          "hata kodu işin yapılmadığını söylemiyor.");

// ── 2–4. Standart dayanıklılık handler'ı, POST'a ayrı muamele yok ────────────
await report.MeasureAsync(
    "2) Standart handler, ilk iki deneme 503",
    Expectation.Exactly(IlkIkiDeneme + 1),
    () => SiparisVerAsync(b => b.AddStandardResilienceHandler(Hizli), Ariza.KaydedipHataDon, IlkIkiDeneme),
    note: "503 geçici hata sayılıyor, POST ayrılmıyor. Üç deneme, üçü de kaydetti; " +
          "istemci 201 görüyor ve kullanıcı her şeyin yolunda olduğunu sanıyor.");

await report.MeasureAsync(
    "3) Standart handler, ilk iki deneme geç cevap",
    Expectation.Exactly(IlkIkiDeneme + 1),
    () => SiparisVerAsync(b => b.AddStandardResilienceHandler(Hizli), Ariza.KaydedipGecCevapVer, IlkIkiDeneme),
    note: "Sunucu hata dönmedi, yalnız yavaştı. Deneme zaman aşımı istemci tarafında doldu, " +
          "istek yeniden gitti; sunucu bu arada ilk işi de bitirdi.");

await report.MeasureAsync(
    "4) Standart handler, her deneme 503",
    Expectation.Exactly(1 + 3),
    () => SiparisVerAsync(b => b.AddStandardResilienceHandler(Hizli), Ariza.KaydedipHataDon, HerDeneme),
    note: "Varsayılan üç yeniden deneme tükeniyor, istemci sonunda 503 görüyor. " +
          "Kullanıcı 'sipariş alınamadı' okuyor; sunucuda dört sipariş var.");

// ── 5. POST'u yeniden denemeden çıkarmak ─────────────────────────────────────
await report.MeasureAsync(
    "5) Güvensiz metotlarda yeniden deneme kapalı",
    Expectation.Exactly(1),
    () => SiparisVerAsync(
        b => b.AddStandardResilienceHandler(o =>
        {
            Hizli(o);
            o.Retry.DisableForUnsafeHttpMethods();
        }),
        Ariza.KaydedipHataDon, IlkIkiDeneme),
    note: "POST, PUT, PATCH, DELETE yeniden denenmiyor; GET denenmeye devam ediyor. " +
          "Kopya yok, ama istemci yine 503 görüyor: güvenli, fakat dayanıklı değil.");

// ── 6–7. Idempotency anahtarı: aynı handler, iki farklı yer ─────────────────
await report.MeasureAsync(
    "6) Anahtar pipeline'ın dışında, ilk iki deneme 503",
    Expectation.Exactly(1),
    () => SiparisVerAsync(
        b => b.AddHttpMessageHandler(() => new AnahtarDamgasi())
              .AddStandardResilienceHandler(Hizli),
        Ariza.KaydedipHataDon, IlkIkiDeneme),
    note: "Anahtar bir kez basılıyor, üç deneme de aynı anahtarı taşıyor. Sunucu ilkinde " +
          "kaydediyor, sonrakilerde tanıyor. Tek sipariş ve istemci 201 görüyor.");

await report.MeasureAsync(
    "7) Anahtar pipeline'ın içinde, ilk iki deneme 503",
    Expectation.Exactly(IlkIkiDeneme + 1),
    () => SiparisVerAsync(
        b =>
        {
            b.AddStandardResilienceHandler(Hizli);
            b.AddHttpMessageHandler(() => new AnahtarDamgasi());
        },
        Ariza.KaydedipHataDon, IlkIkiDeneme),
    note: "Aynı handler, kayıt sırası ters. Her deneme kendi anahtarını alıyor; sunucu " +
          "hiçbirini tanımıyor ve istemci yine 201 görüyor. Anahtar var, koruma yok.");

return report.Print();

// Her senaryo kendi servis sağlayıcısını kuruyor: isimli istemcinin pipeline'ı ve
// devre kesicinin sayaçları senaryolar arasında taşınmasın.
async Task<int> SiparisVerAsync(Action<IHttpClientBuilder> kur, Ariza ariza, int arizaliDenemeSayisi)
{
    defter.Sifirla(ariza, arizaliDenemeSayisi);

    var services = new ServiceCollection();
    kur(services.AddHttpClient("siparis", client => client.BaseAddress = app.BaseAddress));

    await using var provider = services.BuildServiceProvider();
    var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("siparis");

    using var istek = new HttpRequestMessage(HttpMethod.Post, "/siparisler")
    {
        Content = new StringContent("""{"urun":"kulaklik","adet":1}""", Encoding.UTF8, "application/json")
    };

    using var _ = await client.SendAsync(istek);

    return defter.SiparisSayisi;
}

// Yeniden deneme sayısına dokunulmuyor; ölçülen şey varsayılanın davranışı. Yalnız
// bekleme süresi kısaltılıyor: varsayılan 2 sn üstel gecikme sayıyı değiştirmez, yalnız
// laboratuvarı uzatır. Deneme zaman aşımı sunucunun geç cevabının üçte biri.
static void Hizli(HttpStandardResilienceOptions o)
{
    o.Retry.Delay = TimeSpan.FromMilliseconds(20);
    o.Retry.BackoffType = DelayBackoffType.Constant;
    o.Retry.UseJitter = false;
    o.AttemptTimeout.Timeout = SiparisDefteri.GecikmeSuresi / 3;
}

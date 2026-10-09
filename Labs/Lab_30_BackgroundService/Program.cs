using FurkanTural_Labs_Application.Diagnostics;
using Lab_30_BackgroundService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Her senaryoda aynı 10 iş var ve ölçülen tek şey kaçının tamamlandığı. Değişen şey işin
// kendisi değil, onu barındıran host'un semantiği: açılışın neyi beklediği, kaçan
// istisnaya ne yapıldığı ve kapanış token'ına saygı duyulup duyulmadığı.
//
// Beklentiler ölçümden değil barındırma sözleşmesinden geliyor:
//   · host açılışta ExecuteAsync'i beklemez; senkron ön ek bile açılışı tutmaz   → 0 / 0
//   · ExecuteAsync'ten kaçan istisna döngüyü olduğu yerde keser                  → 3
//   · varsayılan davranış (StopHost) kaçan istisnada tüm host'u indirir          → 0
//   · aynı istisna Ignore ile yalnız o işçiyi bitirir, host yaşar                → 10
//   · iş başına try/catch istisnayı ExecuteAsync'ten dışarı hiç çıkarmaz         → 9
//   · stoppingToken'a saygı kalan işi durdurur; yok saymak host'u bekletir       → 3 / 10

const int IsSayisi = 10;
const int BozukSira = 4;
const int IlkParti = 3;

// Karar penceresi: host'un kapanma kararının yayılması için bırakılan sınırlı süre.
// Süreç içi kapanış sinyali mikrosaniyeler mertebesinde yayılır; buradaki pay bolca
// yeterlidir ve her ölçüm bir yarışa değil açık bir sinyale bağlanmıştır.
var pencere = TimeSpan.FromMilliseconds(250);
var pay = TimeSpan.FromMilliseconds(250);

var report = new LabReport(
    title: "Lab_30 — BackgroundService: aynı 10 iş, host semantiğine göre kaçı tamamlanıyor",
    claim: $"Her senaryoda {IsSayisi} iş kuyrukta. Değişen tek şey işi barındıran host'un " +
           "davranışı; ölçülen şey tamamlanan iş sayısı.",
    metric: "Tamamlanan iş");

// ── 1–2. Açılış: host, ExecuteAsync'i beklemiyor ────────────────────────────
// Yaygın tavsiye şudur: "ExecuteAsync'e erken bir await koy, yoksa uygulamanın açılışını
// bloklarsın." Ölçüm bunu desteklemiyor: döngü await'ten önce de sonra da olsa StartAsync
// döndüğünde tamamlanan iş sıfır. Host ExecuteAsync'e giriyor ama onu beklemiyor.
await report.MeasureAsync(
    "1) Döngü ilk await'ten önce, StartAsync döndüğünde",
    Expectation.Exactly(0),
    () => BaslangicAsync(senkronOnce: true),
    note: "Döngü tamamen senkron ve ilk await'ten önce. Klasik tavsiye bu sayının 10 olmasını " +
          "bekletirdi — host işi bitirmeden açılışı tamamlamazdı. Ölçülen 0: host açılışı " +
          "ExecuteAsync'e bağlamıyor.");

await report.MeasureAsync(
    "2) Döngü ilk await'ten sonra, StartAsync döndüğünde",
    Expectation.Exactly(0),
    () => BaslangicAsync(senkronOnce: false),
    note: "Önerilen yazım: await önce, iş sonra. Sonuç 1 ile aynı. İki kurgu açılış " +
          "açısından ayırt edilemiyor; yani o tavsiye bu sürümde bir şey satın almıyor.");

// ── 3. Kaçan istisna, patlayan işçinin kendi bilançosu ──────────────────────
await report.MeasureAsync(
    "3) Kaçan istisna, patlayan işçi",
    Expectation.Exactly(BozukSira - 1),
    PatlayanIsciAsync,
    note: $"Bozuk iş {BozukSira}. sırada; öncesindeki {BozukSira - 1} iş tamamlandı, sonrası " +
          "hiç denenmedi. ExecuteAsync'ten çıkan istisna döngüyü olduğu yerde kesiyor ve " +
          "işçi bir daha başlamıyor — yeniden deneme diye bir şey yok.");

// ── 4–5. Aynı istisna, iki farklı host davranışı ────────────────────────────
// Ölçülen şey patlayan işçi değil KARDEŞİ: patlamadan sonra ayrı bir işçi hâlâ iş
// tamamlayabiliyor mu? Cevap, host'un ayakta kalıp kalmadığını birebir gösteriyor.
// Host burada RunAsync ile sürülüyor, çünkü kapanış kararını yürüten şey odur.
await report.MeasureAsync(
    "4) İstisna sonrası kardeş işçi, varsayılan davranış",
    Expectation.Exactly(0),
    () => KardesAsync(hostuDurdur: true),
    note: "Varsayılan BackgroundServiceExceptionBehavior.StopHost: kaçan istisna yalnız o " +
          "işçiyi değil tüm uygulamayı durduruyor. Kardeş işçinin penceresi iptal ediliyor, " +
          "tek iş bile tamamlanamıyor. Tek bir işçinin hatası bütün süreci indiriyor.");

await report.MeasureAsync(
    "5) İstisna sonrası kardeş işçi, Ignore",
    Expectation.Exactly(IsSayisi),
    () => KardesAsync(hostuDurdur: false),
    note: "Tek ayar değişti. İstisna yine kaçtı, patlayan işçi yine 3'te kaldı — ama host " +
          "yaşıyor ve kardeş işçi 10 işin 10'unu bitiriyor. Bedeli: işçi sessizce öldü ve " +
          "uygulama bunu kimseye söylemiyor.");

// ── 6. İstisnayı ExecuteAsync'ten dışarı hiç çıkarmamak ─────────────────────
await report.MeasureAsync(
    "6) İş başına try/catch",
    Expectation.Exactly(IsSayisi - 1),
    KorumaliIsciAsync,
    note: "Aynı bozuk iş, bu kez her iş kendi try/catch'inde. İstisna ExecuteAsync'ten hiç " +
          "çıkmadığı için host davranışı devreye girmiyor: yalnız bozuk iş atlanıyor, kalan " +
          "9 iş tamamlanıyor. Kararın verileceği yer HostOptions değil, döngünün içi.");

// ── 7–8. Kapanış: stoppingToken'a saygı duymak ne değiştiriyor ──────────────
await report.MeasureAsync(
    "7) Kapanışta stoppingToken'a saygılı",
    Expectation.Exactly(IlkParti),
    () => KapanisAsync(tokenaSaygi: true),
    note: $"StopAsync çağrıldığı anda {IlkParti} iş bitmişti. Token okunduğu için kalan " +
          "işlere hiç başlanmıyor: kapanış istendiği gibi hızlı.");

await report.MeasureAsync(
    "8) Kapanışta stoppingToken yok sayılıyor",
    Expectation.Exactly(IsSayisi),
    () => KapanisAsync(tokenaSaygi: false),
    note: "Aynı noktada aynı StopAsync, ama token okunmuyor: işçi 10 işin hepsini bitiriyor " +
          "ve host onu beklemek zorunda kalıyor. Kapanış en yavaş işçinin hızına düşüyor; " +
          "iş ShutdownTimeout'u aşsaydı süreç ortasından kesilirdi.");

return report.Print();

// Host'u ölçüm için kurar: günlükler kapatılır (kaçan istisna aksi hâlde tabloyu kirletir)
// ve istenirse istisna davranışı değiştirilir.
IHost HostKur(Action<IServiceCollection> kayit, bool? stopHost = null)
{
    var builder = Host.CreateApplicationBuilder();
    builder.Logging.ClearProviders();

    if (stopHost is not null)
    {
        builder.Services.Configure<HostOptions>(o => o.BackgroundServiceExceptionBehavior =
            stopHost.Value
                ? BackgroundServiceExceptionBehavior.StopHost
                : BackgroundServiceExceptionBehavior.Ignore);
    }

    kayit(builder.Services);
    return builder.Build();
}

// StartAsync tam döndüğü ANDA sayacı okur. Kapı hiç açılmadığı için ölçüm bir yarışa
// bağlı değil: host açılışı ExecuteAsync'e bağlasaydı senkron kurguda 10 görülmek
// zorundaydı, çünkü o döngü ilk await'ten önce ve kesintisiz koşuyor.
async Task<int> BaslangicAsync(bool senkronOnce)
{
    var defter = new IsDefteri();
    var kapi = new TaskCompletionSource();

    using var host = HostKur(s => s.AddHostedService(
        _ => new BaslangicIscisi(defter, kapi.Task, IsSayisi, senkronOnce)));

    await host.StartAsync();
    var olculen = defter.Tamamlanan;

    kapi.TrySetResult();
    await host.StopAsync();

    return olculen;
}

// Ignore ile koşar: ölçülen şey host'un kaderi değil, patlayan işçinin istisnadan önce
// kaç iş bitirdiği. Host'un düşmesi bu sayıyı değiştirmez ama ölçümü gürültüye boğar.
async Task<int> PatlayanIsciAsync()
{
    var defter = new IsDefteri();
    var patlama = new TaskCompletionSource();

    using var host = HostKur(
        s => s.AddHostedService(_ => new PatlayanIsci(defter, IsSayisi, BozukSira, patlama)),
        stopHost: false);

    await host.StartAsync();
    await patlama.Task;
    await Task.Delay(pay);
    await host.StopAsync();

    return defter.Tamamlanan;
}

// Host RunAsync ile sürülüyor: StopHost'un kapanma kararını yürüten şey StartAsync değil
// RunAsync'tir. Elle StartAsync ile sürülen bir host'ta istisna kapanmayı ister ama kimse
// yürütmez; o kurguda bu senaryo ölçmek istediği şeyi hiç ölçmez.
async Task<int> KardesAsync(bool hostuDurdur)
{
    var patlayanDefter = new IsDefteri();
    var kardesDefter = new IsDefteri();
    var patlama = new TaskCompletionSource();

    using var host = HostKur(
        s =>
        {
            s.AddHostedService(_ => new PatlayanIsci(patlayanDefter, IsSayisi, BozukSira, patlama));
            s.AddHostedService(_ => new KardesIsci(kardesDefter, patlama.Task, pencere, IsSayisi));
        },
        stopHost: hostuDurdur);

    var kosu = host.RunAsync();
    await patlama.Task;

    // Host kendiliğinden kapanırsa onu bekle; kapanmazsa karar penceresi dolana kadar
    // bekle ve kapanışı kendin istet. İki dalda da ölçüm aynı: kardeş işçinin defteri.
    if (await Task.WhenAny(kosu, Task.Delay(pencere + pay)) != kosu)
    {
        host.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
    }

    await kosu;

    return kardesDefter.Tamamlanan;
}

async Task<int> KorumaliIsciAsync()
{
    var defter = new IsDefteri();

    using var host = HostKur(s => s.AddHostedService(
        _ => new KorumaliIsci(defter, IsSayisi, BozukSira)));

    await host.StartAsync();
    await Task.Delay(pay);
    await host.StopAsync();

    return defter.Tamamlanan;
}

// İlk parti bitince host'u durdurur; kalan işlerin tamamlanıp tamamlanmadığı yalnızca
// işçinin token'ı okuyup okumamasına bağlı.
async Task<int> KapanisAsync(bool tokenaSaygi)
{
    var defter = new IsDefteri();
    var ilkPartiBitti = new TaskCompletionSource();

    using var host = HostKur(s => s.AddHostedService(
        _ => new KapanisIscisi(defter, IsSayisi, IlkParti, tokenaSaygi, pencere, ilkPartiBitti)));

    await host.StartAsync();
    await ilkPartiBitti.Task;
    await host.StopAsync();

    return defter.Tamamlanan;
}

using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Host;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

const int Requests = 5;

var work = TimeSpan.FromMilliseconds(400);          // sunucudaki ağır iş
var giveUp = TimeSpan.FromMilliseconds(80);         // istemcinin vazgeçtiği an
var selfTimeout = TimeSpan.FromMilliseconds(120);   // sunucunun kendi üst sınırı

var completed = new Counters();

await using var app = await LabsWebHost.StartAsync(application =>
{
    // Jeton parametreye alınmıyor: kullanıcı bağlantıyı kopardığında bu iş yine de biter.
    application.MapGet("/tokensiz", async () =>
    {
        await Task.Delay(work);
        completed.Increment("tokensiz");
        return Results.Ok();
    });

    // Jeton alınıyor ve aşağı geçiriliyor: RequestAborted iptal edilince Task.Delay fırlatır.
    application.MapGet("/tokenli", async (CancellationToken ct) =>
    {
        await Task.Delay(work, ct);
        completed.Increment("tokenli");
        return Results.Ok();
    });

    // İstemci beklemeye devam etse bile sunucu kendi üst sınırını koyuyor.
    application.MapGet("/kendi-timeout", async (CancellationToken ct) =>
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(selfTimeout);

        await Task.Delay(work, cts.Token);
        completed.Increment("kendi-timeout");
        return Results.Ok();
    });
});

var report = new LabReport(
    title: "Lab_08 — CancellationToken: istemci vazgeçtikten sonra ne oluyor",
    claim: $"Her senaryoda {Requests} istek gönderilip {giveUp.TotalMilliseconds:N0} ms sonra " +
           $"iptal ediliyor. Sunucudaki iş {work.TotalMilliseconds:N0} ms sürüyor; ölçülen, kaçının " +
           "buna rağmen sonuna kadar çalıştığı.",
    metric: "Tamamlanan");

// ── 1. Jeton geçirilmemiş uç ─────────────────────────────────────────────────
await report.MeasureAsync(
    "1) Jeton geçirilmiyor",
    Expectation.Exactly(Requests),
    () => AbortedRunAsync(app, "/tokensiz", completed, "tokensiz"),
    note: "İstemci gitti, iş bitti: CPU ve bağlantı, kimsenin okumayacağı bir cevap için harcandı.");

// ── 2. Jeton aşağı geçirilmiş uç ─────────────────────────────────────────────
await report.MeasureAsync(
    "2) Jeton geçiriliyor",
    Expectation.Exactly(0),
    () => AbortedRunAsync(app, "/tokenli", completed, "tokenli"),
    note: "Tek fark metot imzasındaki parametre; iş istemciyle birlikte duruyor.");

// ── 3. Kendi üst sınırını koyan uç ───────────────────────────────────────────
// İptal her zaman istemciden gelmez. Bağlı jeton, isteğin iptalini de kendi süresini de dinler.
await report.MeasureAsync(
    "3) Bağlı jeton + CancelAfter",
    Expectation.Exactly(0),
    () => RunAsync(app, "/kendi-timeout", completed, "kendi-timeout", abort: false),
    note: $"İstemci beklemeye razıydı; işi {selfTimeout.TotalMilliseconds:N0} ms'de sunucu durdurdu.");

// ── 4. İptal yok: jeton yolun taşı değil ─────────────────────────────────────
await report.MeasureAsync(
    "4) Jeton var, iptal yok",
    Expectation.Exactly(Requests),
    () => RunAsync(app, "/tokenli", completed, "tokenli", abort: false),
    note: "Jeton geçirmek işi kırmıyor; yalnız gereksiz hâle geldiğinde durduruyor.");

return report.Print();

// İstekleri gönderir, istenirse yarıda keser ve sunucudaki işin bitmesini bekleyip sayar.
Task<int> AbortedRunAsync(LabApp app, string path, Counters counters, string key)
    => RunAsync(app, path, counters, key, abort: true);

async Task<int> RunAsync(LabApp app, string path, Counters counters, string key, bool abort)
{
    counters.Reset(key);

    for (var i = 0; i < Requests; i++)
    {
        using var cts = new CancellationTokenSource();
        if (abort) cts.CancelAfter(giveUp);

        try
        {
            using var response = await app.Client.GetAsync(path, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Beklenen: istemci vazgeçti. Sunucunun ne yaptığı ayrı bir soru.
        }
        catch (HttpRequestException)
        {
            // Sunucu isteği iptal edilmiş olarak sonlandırdı; istemci tarafında bağlantı düştü.
        }
    }

    // Sunucudaki iş istemciden bağımsız sürüyor; saymadan önce bitmesine izin ver.
    await Task.Delay(work + TimeSpan.FromMilliseconds(200));

    return counters.Read(key);
}

/// <summary>Uçların "sonuna kadar çalıştım" sayacı.</summary>
internal sealed class Counters
{
    private readonly Dictionary<string, int> _values = [];
    private readonly Lock _gate = new();

    public void Increment(string key)
    {
        lock (_gate) _values[key] = _values.GetValueOrDefault(key) + 1;
    }

    public void Reset(string key)
    {
        lock (_gate) _values[key] = 0;
    }

    public int Read(string key)
    {
        lock (_gate) return _values.GetValueOrDefault(key);
    }
}

using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Host;
using Lab_21_OutputCaching;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

const int RequestCount = 10;
const string Tag = "bloglar";

var headerCounter = new EndpointCounter();
var responseCounter = new EndpointCounter();
var outputCounter = new EndpointCounter();

// ── Yalnız başlık: [ResponseCache] attribute'unun sunucudaki karşılığı ───────
// Attribute bir filtredir; tek işi yanıta Cache-Control yazmaktır. Sunucuda kopya oluşmaz.
await using var headerOnly = await LabsWebHost.StartAsync(
    app => app.MapGet("/bloglar", (HttpContext context) =>
    {
        WriteCacheControl(context);
        headerCounter.Record();
        return Results.Ok("bloglar");
    }));

// ── Response caching: sunucuda gerçekten saklayan middleware ─────────────────
await using var responseCaching = await LabsWebHost.StartAsync(
    app =>
    {
        app.UseResponseCaching();

        app.MapGet("/korumali", (HttpContext context) =>
        {
            WriteCacheControl(context);
            responseCounter.Record();
            return Results.Ok("korumali");
        });

        app.MapGet("/acik", (HttpContext context) =>
        {
            WriteCacheControl(context);
            responseCounter.Record();
            return Results.Ok("acik");
        });
    },
    builder => builder.Services.AddResponseCaching());

// ── Output caching: kararı HTTP kuralları değil, policy verir ────────────────
await using var outputCaching = await LabsWebHost.StartAsync(
    app =>
    {
        app.UseRouting();
        app.UseOutputCache();

        app.MapGet("/bloglar", (int page, int pageSize) =>
        {
            outputCounter.Record();
            return Results.Ok($"page={page}, pageSize={pageSize}");
        })
        .CacheOutput(policy => policy
            .Tag(Tag)
            .Expire(TimeSpan.FromMinutes(5))
            // Bilerek eksik: uç pageSize'ı da okuyor ama anahtara yalnız page giriyor.
            .SetVaryByQuery("page"));
    },
    builder => builder.Services.AddOutputCache());

var report = new LabReport(
    title: "Lab_21 — Cache eklendi, sunucu yükü düştü mü",
    claim: $"Her senaryoda {RequestCount} istek gönderiliyor ve hepsi 200 alıyor. " +
           "Ölçülen şey, ucun kaç kez gerçekten çalıştığı.",
    metric: "Uca ulaşan");

// ── 1. Yalnız Cache-Control başlığı ──────────────────────────────────────────
await report.MeasureAsync(
    "1) Sadece Cache-Control başlığı",
    Expectation.Exactly(RequestCount),
    async () =>
    {
        var hits = await SweepAsync(headerOnly, headerCounter, "/bloglar", RequestCount);

        using var response = await headerOnly.Client.GetAsync("/bloglar");
        if (response.Headers.CacheControl?.Public != true)
            throw new InvalidOperationException("Cache-Control başlığı yazılmamış; ölçüm başka şeyi ölçüyor.");

        return hits;
    },
    note: "Başlık yazıldı ama sunucuda kopya yok: talimat istemciye verildi, uç yine çalıştı.");

// ── 2. Response caching + Authorization ──────────────────────────────────────
await report.MeasureAsync(
    "2) Response caching, Authorization var",
    Expectation.Exactly(RequestCount),
    () => SweepAsync(responseCaching, responseCounter, "/korumali", RequestCount, authorization: "Bearer lab"),
    note: "Middleware kayıtlı, uyarı yok, tek yanıt bile saklanmadı. JWT'li API'de her istek böyledir.");

// ── 3. Aynı middleware, kimliksiz istek ──────────────────────────────────────
await report.MeasureAsync(
    "3) Response caching, Authorization yok",
    Expectation.Exactly(1),
    () => SweepAsync(responseCaching, responseCounter, "/acik", RequestCount),
    note: "Tek fark bir başlık. HTTP cache kurallarına uyan istek gerçekten saklandı.");

// ── 4. Output caching ────────────────────────────────────────────────────────
await report.MeasureAsync(
    "4) Output caching",
    Expectation.Exactly(1),
    () => SweepAsync(outputCaching, outputCounter, "/bloglar?page=1&pageSize=10", RequestCount),
    note: "İsabet olduğunda istek uca hiç ulaşmıyor; kararı HTTP kuralları değil policy veriyor.");

// ── 5. Tag ile geçersizleştirme ──────────────────────────────────────────────
await report.MeasureAsync(
    "5) Output caching, EvictByTag sonrası",
    Expectation.Exactly(2),
    async () =>
    {
        outputCounter.Reset();

        await SendAsync(outputCaching, "/bloglar?page=2&pageSize=10", RequestCount);

        var store = outputCaching.Services.GetRequiredService<IOutputCacheStore>();
        await store.EvictByTagAsync(Tag, CancellationToken.None);

        await SendAsync(outputCaching, "/bloglar?page=2&pageSize=10", RequestCount);

        return outputCounter.Hits;
    },
    note: $"{RequestCount * 2} istek, 2 çalışma: tag düşünce TTL beklenmeden yeniden üretildi.");

// ── 6. Anahtara girmeyen parametre ───────────────────────────────────────────
await report.MeasureAsync(
    "6) SetVaryByQuery eksik",
    Expectation.Exactly(1),
    async () =>
    {
        outputCounter.Reset();

        var first = await ReadAsync(outputCaching, "/bloglar?page=9&pageSize=10");
        var second = await ReadAsync(outputCaching, "/bloglar?page=9&pageSize=50");

        if (first != second)
            throw new InvalidOperationException("İki yanıt farklı; anahtar pageSize'ı ayırmış.");

        return outputCounter.Hits;
    },
    note: "İkinci istek pageSize=50 sordu, pageSize=10 yanıtını aldı. Bu performans değil, veri hatası.");

return report.Print();

// Attribute'un yaptığı işin aynısı: yanıta talimat yaz, başka bir şey yapma.
static void WriteCacheControl(HttpContext context)
    => context.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
    {
        Public = true,
        MaxAge = TimeSpan.FromSeconds(60)
    };

static async Task<int> SweepAsync(
    LabApp app,
    EndpointCounter counter,
    string path,
    int count,
    string? authorization = null)
{
    counter.Reset();
    await SendAsync(app, path, count, authorization);
    return counter.Hits;
}

// İstekler sıra ile gönderilir: eşzamanlı gönderilseydi hepsi ilk isabetten önce
// gelir ve cache'in çalışıp çalışmadığı değil, yarışın sonucu ölçülürdü.
static async Task SendAsync(LabApp app, string path, int count, string? authorization = null)
{
    for (var i = 0; i < count; i++)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (authorization is not null)
            request.Headers.Add(HeaderNames.Authorization, authorization);

        using var response = await app.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}

static async Task<string> ReadAsync(LabApp app, string path)
{
    using var response = await app.Client.GetAsync(path);
    response.EnsureSuccessStatusCode();

    return await response.Content.ReadAsStringAsync();
}

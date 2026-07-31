using System.Threading.RateLimiting;
using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Host;
using Lab_14_RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

const int RequestCount = 20;
const string ByClient = "istemciye-gore";
const string SingleBucket = "tek-kova";

// ── Elle yazılmış sayaç ──────────────────────────────────────────────────────
await using var naive = await LabsWebHost.StartAsync(
    app =>
    {
        app.UseNaiveRateLimiter();
        app.MapGet("/veri", () => Results.Ok("veri"));
    },
    builder => builder.Services.AddMemoryCache());

// ── Yerleşik limiter, hiçbir şey ayarlanmamış ────────────────────────────────
await using var defaults = await LabsWebHost.StartAsync(
    app =>
    {
        app.UseRateLimiter();
        app.MapGet("/veri", () => Results.Ok("veri")).RequireRateLimiting(ByClient);
    },
    builder => builder.Services.AddRateLimiter(options => options.AddPolicy(ByClient, ByClientPartition)));

// ── Yerleşik limiter, red yanıtı düzeltilmiş ─────────────────────────────────
await using var tuned = await LabsWebHost.StartAsync(
    app =>
    {
        app.UseRateLimiter();
        app.MapGet("/veri", () => Results.Ok("veri")).RequireRateLimiting(ByClient);

        // Proxy arkasında RemoteIpAddress herkes için aynıdır: sabit anahtarın karşılığı bu.
        app.MapGet("/proxy-arkasi", () => Results.Ok("veri")).RequireRateLimiting(SingleBucket);
    },
    builder => builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy(ByClient, ByClientPartition);
        options.AddPolicy(SingleBucket, _ => FixedWindow("herkes"));

        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            // Framework bu başlığı kendiliğinden basmaz. Metadata'yı yenilenme periyodu
            // bilinen limiter'lar üretir; ConcurrencyLimiter'da TryGetMetadata false döner.
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

            await context.HttpContext.Response.WriteAsJsonAsync(
                new { message = "Çok fazla istek gönderdiniz." }, token);
        };
    }));

var report = new LabReport(
    title: "Lab_14 — Rate limiting: aynı yığın, iki uygulama",
    claim: $"Her senaryoda {RequestCount} istek aynı anda gönderiliyor ve izin sayısı " +
           $"{NaiveRateLimiter.PermitLimit}. Ölçülen şey, kaçının 200 aldığı.",
    metric: "Geçen");

// ── 1. Elle yazılmış sayaç ───────────────────────────────────────────────────
// Beklenti tek sayı değil aralık: yarış koşulunun sonucu tanımı gereği deterministik
// değildir. Kanıtlanan şey "kaç tane geçti" değil, "limit tutmadı".
await report.MeasureAsync(
    "1) Elle sayaç, eşzamanlı",
    Expectation.Between(NaiveRateLimiter.PermitLimit + 1, RequestCount),
    async () => (await Burst.SendAsync(naive, "/veri", "a", RequestCount)).Passed,
    note: $"İzin {NaiveRateLimiter.PermitLimit}'ti. Get ile Set arasında kilit yok; " +
          "eşzamanlı istekler aynı eski sayıyı okuyor.");

// ── 2. Yerleşik limiter, varsayılan ayarlar ──────────────────────────────────
await report.MeasureAsync(
    "2) Yerleşik, varsayılan ayar",
    Expectation.Exactly(NaiveRateLimiter.PermitLimit),
    async () =>
    {
        var result = await Burst.SendAsync(defaults, "/veri", "a", RequestCount);

        if (!result.AllRejectedWith(StatusCodes.Status503ServiceUnavailable))
            throw new InvalidOperationException(
                $"Varsayılan red kodu beklenenden farklı: {string.Join(", ", result.RejectedCodes.Distinct())}");

        if (result.RetryAfterSeen)
            throw new InvalidOperationException("Retry-After kendiliğinden basıldı; yazı bunun tersini söylüyor.");

        return result.Passed;
    },
    note: "Sayı doğru ama reddedilenlerin hepsi 503 döndü ve Retry-After yok: ikisi de varsayılan.");

// ── 3. Pencere kapandıktan sonra ─────────────────────────────────────────────
await report.MeasureAsync(
    "3) Yerleşik, pencere yenilendikten sonra",
    Expectation.Exactly(NaiveRateLimiter.PermitLimit),
    async () =>
    {
        await Burst.SendAsync(tuned, "/veri", "b", RequestCount);          // kotayı tüket
        await Task.Delay(NaiveRateLimiter.Window * 2);                     // pencerenin kapanmasını bekle

        var result = await Burst.SendAsync(tuned, "/veri", "b", RequestCount);

        if (!result.AllRejectedWith(StatusCodes.Status429TooManyRequests))
            throw new InvalidOperationException(
                $"Red kodu 429'a çekilmemiş: {string.Join(", ", result.RejectedCodes.Distinct())}");

        if (!result.RetryAfterSeen)
            throw new InvalidOperationException("Retry-After basılmadı; OnRejected çalışmamış.");

        return result.Passed;
    },
    note: "Ölçülen ikinci yığın. Kota geri geldi; red kodu 429 ve Retry-After elle basılıyor.");

// ── 4. İki ayrı istemci ──────────────────────────────────────────────────────
await report.MeasureAsync(
    "4) İki istemci, ayrı partition",
    Expectation.Exactly(NaiveRateLimiter.PermitLimit * 2),
    async () =>
    {
        var first = Burst.SendAsync(tuned, "/veri", "c", RequestCount);
        var second = Burst.SendAsync(tuned, "/veri", "d", RequestCount);

        var results = await Task.WhenAll(first, second);
        return results.Sum(r => r.Passed);
    },
    note: $"{RequestCount * 2} istek gönderildi; her istemci kendi kovasından {NaiveRateLimiter.PermitLimit} hak aldı.");

// ── 5. Proxy arkası: herkes tek kovada ───────────────────────────────────────
await report.MeasureAsync(
    "5) İki istemci, tek partition",
    Expectation.Exactly(NaiveRateLimiter.PermitLimit),
    async () =>
    {
        var first = Burst.SendAsync(tuned, "/proxy-arkasi", "e", RequestCount);
        var second = Burst.SendAsync(tuned, "/proxy-arkasi", "f", RequestCount);

        var results = await Task.WhenAll(first, second);
        return results.Sum(r => r.Passed);
    },
    note: "Aynı istekler, tek fark partition anahtarı. İki istemci birlikte tek hakkı paylaştı.");

return report.Print();

static RateLimitPartition<string> ByClientPartition(HttpContext context)
    => FixedWindow(context.Request.Headers[NaiveRateLimiter.ClientHeader].ToString());

static RateLimitPartition<string> FixedWindow(string partitionKey)
    => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey,
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = NaiveRateLimiter.PermitLimit,
            Window = NaiveRateLimiter.Window,
            QueueLimit = 0
        });

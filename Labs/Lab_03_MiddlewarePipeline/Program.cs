using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Host;
using Lab_03_MiddlewarePipeline;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

// Uygulamalar ölçümün dışında ayağa kaldırılır: Kestrel'in açılış maliyeti hangi senaryo
// önce koşarsa ona yazılırdı ve tablo yanıltırdı.
await using var correctOrder = await StartAsync(app =>
{
    app.UseAuthentication();      // 1. Kim olduğunu öğren
    app.UseAuthorization();       // 2. Ne yapabileceğine karar ver
    MapEndpoints(app);
});

await using var reversedOrder = await StartAsync(app =>
{
    app.UseAuthorization();       // Kimlik henüz belirlenmedi
    app.UseAuthentication();
    MapEndpoints(app);
});

await using var noAuthorization = await StartAsync(MapEndpoints);

var shortCircuitReached = 0;
await using var shortCircuit = await StartAsync(app =>
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Headers.ContainsKey("X-Lab-Block"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;                                   // next() çağrılmadı: zincir burada bitti
        }

        await next(context);
    });

    app.MapGet("/gizli", () => { shortCircuitReached++; return Results.Ok("gizli"); });
});

var report = new LabReport(
    title: "Lab_03 — Middleware sıralaması: ne zaman sessiz, ne zaman sesli bozulur",
    claim: "Aynı istek beş farklı boru hattından geçiyor. Sıralamayı bozmanın bedeli " +
           "istekleri sessizce geçirmek değil; ölçülen sonuç bunun tersini söylüyor.",
    metric: "Durum");

// ── 1. Doğru sıra, kimliksiz istek ───────────────────────────────────────────
await report.MeasureAsync(
    "1) Doğru sıra, kimliksiz",
    Expectation.Exactly(StatusCodes.Status401Unauthorized),
    () => StatusAsync(correctOrder, user: null),
    note: "Beklenen davranış: kimlik yoksa yetkilendirme reddeder.");

// ── 2. Doğru sıra, kimlikli istek ────────────────────────────────────────────
await report.MeasureAsync(
    "2) Doğru sıra, kimlikli",
    Expectation.Exactly(StatusCodes.Status200OK),
    () => StatusAsync(correctOrder, user: "furkan"),
    note: "Kimlik doğrulama önce çalıştı, yetkilendirme onu gördü.");

// ── 3. Ters sıra, kimlikli istek ─────────────────────────────────────────────
// Yazının merkez iddiası burada sınanıyor: "yanlış sırada tüm istekler geçer".
await report.MeasureAsync(
    "3) Ters sıra, kimlikli",
    Expectation.Exactly(StatusCodes.Status401Unauthorized),
    () => StatusAsync(reversedOrder, user: "furkan"),
    note: "Aynı geçerli kimlikle 2) 200 döndü, bu 401 döndü: hat açılmıyor, kapanıyor.");

// ── 4. UseAuthorization hiç yazılmamış ───────────────────────────────────────
await report.MeasureAsync(
    "4) UseAuthorization yok, kimliksiz",
    Expectation.Exactly(StatusCodes.Status401Unauthorized),
    () => StatusAsync(noAuthorization, user: null),
    note: "Middleware yazılmadı ama koruma çalıştı: WebApplication servisler kayıtlıysa ekliyor.");

// ── 5. Short-circuit: next() çağrılmadı ──────────────────────────────────────
await report.MeasureAsync(
    "5) Short-circuit middleware",
    Expectation.Exactly(StatusCodes.Status403Forbidden),
    async () =>
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/gizli");
        request.Headers.Add("X-Lab-Block", "1");

        var response = await shortCircuit.Client.SendAsync(request);
        if (shortCircuitReached != 0)
            throw new InvalidOperationException("Uç çalıştı; zincir kırılmamış.");

        return (int)response.StatusCode;
    },
    note: "Uç sayacı 0'da kaldı: zincir kırıldığında sonrası hiç çalışmaz.");

return report.Print();

// Ortak kurulum: aynı servisler, aynı uç, yalnız boru hattı değişiyor.
static Task<LabApp> StartAsync(Action<WebApplication> configure) =>
    LabsWebHost.StartAsync(configure, builder =>
    {
        builder.Services
            .AddAuthentication(HeaderAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(
                HeaderAuthenticationHandler.SchemeName, _ => { });

        builder.Services.AddAuthorization();
    });

static void MapEndpoints(WebApplication app)
    => app.MapGet("/gizli", () => Results.Ok("gizli")).RequireAuthorization();

static async Task<int> StatusAsync(LabApp app, string? user)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, "/gizli");
    if (user is not null)
        request.Headers.Add(HeaderAuthenticationHandler.HeaderName, user);

    var response = await app.Client.SendAsync(request);
    return (int)response.StatusCode;
}

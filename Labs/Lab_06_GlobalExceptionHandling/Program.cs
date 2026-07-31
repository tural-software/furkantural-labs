using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Host;
using Lab_06_GlobalExceptionHandling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

// Aynı uçlar, iki farklı uygulama: biri hatayı kimseye emanet etmiyor, diğeri tek yerde topluyor.
await using var unhandled = await LabsWebHost.StartAsync(MapEndpoints);

await using var handled = await LabsWebHost.StartAsync(
    application =>
    {
        application.UseExceptionHandler();
        MapEndpoints(application);
    },
    builder =>
    {
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();
    });

var report = new LabReport(
    title: "Lab_06 — Global exception handling: istemci ne görüyor",
    claim: "Aynı beş hata iki uygulamada da fırlatılıyor. Try-catch'leri silmenin karşılığı " +
           "daha az kod değil; istemcinin gördüğü durum kodunun hatanın türüyle eşleşmesi.",
    metric: "Durum");

// ── 1. Hiçbir yerde yakalanmayan hata ────────────────────────────────────────
await report.MeasureAsync(
    "1) Handler yok, NotFound",
    Expectation.Exactly(StatusCodes.Status500InternalServerError),
    () => StatusAsync(unhandled, "/yok"),
    note: "İş kuralı ihlali sunucu hatası olarak dönüyor; istemci ayırt edemiyor.");

// ── 2. Aynı hata, tek yerde eşlenmiş ─────────────────────────────────────────
await report.MeasureAsync(
    "2) Handler var, NotFound",
    Expectation.Exactly(StatusCodes.Status404NotFound),
    () => StatusAsync(handled, "/yok"),
    note: "Uçta tek satır try-catch yok; eşleme handler'ın içinde.");

// ── 3. Doğrulama hatası ──────────────────────────────────────────────────────
await report.MeasureAsync(
    "3) Handler var, Validation",
    Expectation.Exactly(StatusCodes.Status400BadRequest),
    () => StatusAsync(handled, "/gecersiz"),
    note: "Yeni hata tipi eklemek tek bir switch kolu; uçlara dokunulmuyor.");

// ── 4. Tanınmayan hata ───────────────────────────────────────────────────────
// Handler'ın işi hataları gizlemek değil; tanımadığını 500'e düşürüp metnini sızdırmamak.
await report.MeasureAsync(
    "4) Handler var, beklenmedik",
    Expectation.Exactly(StatusCodes.Status500InternalServerError),
    async () =>
    {
        var response = await handled.Client.GetAsync("/patla");
        var body = await response.Content.ReadAsStringAsync();

        if (body.Contains("veritabanı parolası", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("İç hata metni istemciye sızdı.");

        return (int)response.StatusCode;
    },
    note: "500 dönüyor ama gövde ProblemDetails; özgün hata metni istemciye geçmiyor.");

// ── 5. İptal edilen istek ────────────────────────────────────────────────────
// Yazı #8'in bağlandığı yer: iptali 500 sayan bir handler, gösterge panosunu kirletir.
await report.MeasureAsync(
    "5) Handler var, iptal",
    Expectation.Exactly(GlobalExceptionHandler.ClientClosedRequest),
    () => StatusAsync(handled, "/iptal"),
    note: "İptal hata değil; 499 ile ayrılmazsa hata oranı istemcinin davranışıyla şişer.");

return report.Print();

static void MapEndpoints(WebApplication app)
{
    // Dönüş tipi açıkça yazılıyor: gövdesi yalnız throw olan bir lambda'nın tipini
    // derleyici çıkaramaz ve RequestDelegate sanır.
    app.MapGet("/yok", IResult () => throw new NotFoundException("Kayıt bulunamadı"));
    app.MapGet("/gecersiz", IResult () => throw new ValidationException("Ad alanı zorunlu"));
    app.MapGet("/patla", IResult () => throw new InvalidOperationException("bağlantı dizesi: veritabanı parolası"));
    app.MapGet("/iptal", IResult () => throw new OperationCanceledException());
}

static async Task<int> StatusAsync(LabApp app, string path)
{
    var response = await app.Client.GetAsync(path);
    return (int)response.StatusCode;
}

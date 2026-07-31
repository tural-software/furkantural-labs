using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Host;
using Lab_20_Cors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

const string PolicyName = "spa";
const int Allowed = 1;
const int Blocked = 0;

// ── CORS hiç yapılandırılmamış ───────────────────────────────────────────────
await using var noCors = await LabsWebHost.StartAsync(app => app.MapGet("/veri", () => Results.Ok("veri")));

// ── Doğru yapılandırma, doğru sıra ───────────────────────────────────────────
await using var correct = await LabsWebHost.StartAsync(
    app =>
    {
        app.UseRouting();
        app.UseCors(PolicyName);        // authentication ve authorization'dan ÖNCE
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/veri", () => Results.Ok("veri"));

        // RequireCors uca CORS metadata'sı iliştirir — [EnableCors("spa")] ile aynı şey.
        // Yönlendirme ön kontrolü ancak bu metadata varsa uca eşler; eşlemezse yetkilendirme
        // hiç çalışmaz ve sıralama sorusu ölçülemeden kaybolur.
        app.MapPut("/veri", () => Results.Ok("veri"))
           .RequireCors(PolicyName)
           .RequireAuthorization();
    },
    builder => ConfigureCors(builder, CrossOriginProbe.AllowedOrigin));

// ── Tek fark: origin'in sonunda eğik çizgi var ───────────────────────────────
await using var trailingSlash = await LabsWebHost.StartAsync(
    app =>
    {
        app.UseCors(PolicyName);
        app.MapGet("/veri", () => Results.Ok("veri"));
    },
    builder => ConfigureCors(builder, CrossOriginProbe.AllowedOrigin + "/"));

// ── Aynı policy, yanlış sıra ─────────────────────────────────────────────────
await using var corsAfterAuth = await LabsWebHost.StartAsync(
    app =>
    {
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseCors(PolicyName);        // preflight buraya gelmeden yetkilendirmeye uğruyor

        app.MapPut("/veri", () => Results.Ok("veri"))
           .RequireCors(PolicyName)
           .RequireAuthorization();
    },
    builder => ConfigureCors(builder, CrossOriginProbe.AllowedOrigin));

var report = new LabReport(
    title: "Lab_20 — CORS: tarayıcının aradığı izin döndü mü",
    claim: $"Altı istek de sunucuya ulaşıyor ve işleniyor. Ölçülen şey, yanıtta " +
           $"{CrossOriginProbe.AllowedOrigin} için Access-Control-Allow-Origin başlığının bulunup bulunmadığı.",
    metric: "Origin izni");

// ── 1. CORS hiç kurulmamış ───────────────────────────────────────────────────
await report.MeasureAsync(
    "1) CORS yapılandırılmamış",
    Expectation.Exactly(Blocked),
    () => AllowedWithStatusAsync(
        CrossOriginProbe.RequestAsync(noCors, HttpMethod.Get, "/veri"),
        expectedStatus: StatusCodes.Status200OK),
    note: "Sunucu 200 döndü, iş yapıldı. Başlık olmadığı için tarayıcı sonucu JavaScript'e vermez.");

// ── 2. Tam eşleşen origin ────────────────────────────────────────────────────
await report.MeasureAsync(
    "2) WithOrigins, tam eşleşme",
    Expectation.Exactly(Allowed),
    async () => (await CrossOriginProbe.RequestAsync(correct, HttpMethod.Get, "/veri")).OriginAllowed,
    note: "Liste konfigürasyondan okunuyor, joker yok, credential'a izin var.");

// ── 3. Sondaki tek karakter ──────────────────────────────────────────────────
await report.MeasureAsync(
    "3) WithOrigins, sonda eğik çizgi",
    Expectation.Exactly(Blocked),
    () => AllowedWithStatusAsync(
        CrossOriginProbe.RequestAsync(trailingSlash, HttpMethod.Get, "/veri"),
        expectedStatus: StatusCodes.Status200OK),
    note: "Karşılaştırma metin olarak yapılır; tek karakterlik fark eşleşmeyi bitirir.");

// ── 4. Joker + credential ────────────────────────────────────────────────────
// Spesifikasyon joker origin ile credential'ı bir arada kabul etmez. Ölçülen soru:
// bu hata ne zaman ortaya çıkıyor — ilk istekte mi, yoksa daha uygulama açılırken mi?
await report.MeasureAsync(
    "4) AllowAnyOrigin + AllowCredentials",
    Expectation.Exactly(Blocked),
    async () =>
    {
        try
        {
            await using var wildcard = await LabsWebHost.StartAsync(
                app =>
                {
                    app.UseCors();
                    app.MapGet("/veri", () => Results.Ok("veri"));
                },
                builder => builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials())));
        }
        catch (InvalidOperationException)
        {
            // İzin dönmedi çünkü ortada dinleyen bir uygulama yok.
            return Blocked;
        }

        throw new InvalidOperationException(
            "Uygulama ayağa kalktı: spesifikasyon dışı birleşim artık başlangıçta yakalanmıyor.");
    },
    note: "Uygulama hiç açılmadı; hata CORS middleware'i kurulurken fırlıyor. Bkz. README.");

// ── 5. Preflight, UseCors yetkilendirmeden sonra ─────────────────────────────
await report.MeasureAsync(
    "5) Preflight, UseCors auth'tan sonra",
    Expectation.Exactly(Blocked),
    () => AllowedWithStatusAsync(
        CrossOriginProbe.PreflightAsync(corsAfterAuth, "/veri", HttpMethod.Put),
        expectedStatus: StatusCodes.Status401Unauthorized),
    note: "Ön kontrol kimlik bilgisi taşımaz; 401 alıyor ve asıl istek hiç gönderilmiyor.");

// ── 6. Aynı preflight, doğru sıra ────────────────────────────────────────────
await report.MeasureAsync(
    "6) Preflight, UseCors auth'tan önce",
    Expectation.Exactly(Allowed),
    async () => (await CrossOriginProbe.PreflightAsync(correct, "/veri", HttpMethod.Put)).OriginAllowed,
    note: "Aynı korumalı uç, aynı ön kontrol. Tek fark iki satırın yeri.");

return report.Print();

static void ConfigureCors(WebApplicationBuilder builder, string origin)
{
    builder.Services.AddLabAuthentication();
    builder.Services.AddAuthorization();

    builder.Services.AddCors(options => options.AddPolicy(PolicyName, policy => policy
        .WithOrigins(origin)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders("X-Pagination")
        .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));
}

// Durum kodunu tabloya karıştırmadan doğrular: sütun tek birim taşır, sapma gürültüyle biter.
static async Task<int> AllowedWithStatusAsync(Task<ProbeResult> probe, int expectedStatus)
{
    var result = await probe;

    if (result.Status != expectedStatus)
        throw new InvalidOperationException($"Beklenen durum {expectedStatus}, gelen {result.Status}.");

    return result.OriginAllowed;
}

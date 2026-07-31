using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Host;
using Lab_05_DiScope;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const int Requests = 2;

// Tek bir uygulama; her uç farklı bir yaşam süresi sorusunu cevaplıyor.
await using var app = await LabsWebHost.StartAsync(
    application =>
    {
        application.MapGet("/scoped", (Probe probe) => probe.InstanceId);
        application.MapGet("/singleton", (SingletonProbe probe) => probe.InstanceId);
        application.MapGet("/captive", (CaptiveHolder holder) => holder.Held.InstanceId);
        application.MapGet("/factory", (ScopeAwareHolder holder) => holder.ResolveInstanceId());
    },
    builder =>
    {
        builder.Services.AddScoped<Probe>();
        builder.Services.AddSingleton<SingletonProbe>();
        builder.Services.AddSingleton<CaptiveHolder>();      // ← Scoped bağımlılık tutan Singleton
        builder.Services.AddSingleton<ScopeAwareHolder>();
    });

var report = new LabReport(
    title: "Lab_05 — DI scope: bağımlılık ne zaman hapsolur",
    claim: $"Aynı uygulamaya {Requests} istek gidiyor ve her seferinde kaç farklı nesne üretildiği " +
           "sayılıyor. Captive dependency'nin belirtisi hata değil, bu sayının 1'de kalmasıdır.",
    metric: "Örnek");

// ── 1. Scoped: her istek kendi örneğini alır ─────────────────────────────────
await report.MeasureAsync(
    "1) Scoped servis",
    Expectation.Exactly(Requests),
    () => DistinctInstancesAsync(app, "/scoped"),
    note: "Beklenen davranış: istek biter, nesne de biter.");

// ── 2. Singleton: uygulama boyunca tek örnek ─────────────────────────────────
await report.MeasureAsync(
    "2) Singleton servis",
    Expectation.Exactly(1),
    () => DistinctInstancesAsync(app, "/singleton"),
    note: "Bu doğru davranış: yapılandırma, önbellek gibi durumsuz/paylaşılan servisler.");

// ── 3. Captive dependency ────────────────────────────────────────────────────
// Kayıt Scoped, davranış Singleton. Kod hiçbir yerde yanlış görünmüyor; ikinci istek
// birincinin nesnesini kullanıyor. DbContext olsaydı bayat veri ya da ObjectDisposedException.
await report.MeasureAsync(
    "3) Singleton içinde Scoped",
    Expectation.Exactly(1),
    () => DistinctInstancesAsync(app, "/captive"),
    note: "Scoped kaydedildi ama 1'de kaldı: ilk isteğin nesnesi uygulama boyunca yaşıyor.");

// ── 4. IServiceScopeFactory ile doğrusu ──────────────────────────────────────
await report.MeasureAsync(
    "4) IServiceScopeFactory",
    Expectation.Exactly(Requests),
    () => DistinctInstancesAsync(app, "/factory"),
    note: "Singleton hâlâ Singleton, ama bağımlılığı tutmuyor; her kullanımda yeni scope.");

// ── 5. Doğrulama açıkken aynı kayıt ──────────────────────────────────────────
// Yukarıdaki 3. senaryo yalnızca doğrulama kapalı olduğu için çalışabildi. Açıkken
// uygulama hiç ayağa kalkmaz: hata üretime değil, geliştiricinin ekranına düşer.
await report.MeasureAsync(
    "5) ValidateOnBuild açık",
    Expectation.Exactly(0),
    () => Task.FromResult(InstancesCreatedWithValidation()),
    note: "Uygulama başlamadı; captive dependency derleme değil ama açılış hatası hâline geldi.");

return report.Print();

// Aynı uca arka arkaya istek atar ve kaç farklı kimlik döndüğünü sayar.
static async Task<int> DistinctInstancesAsync(LabApp app, string path)
{
    var seen = new HashSet<int>();

    for (var i = 0; i < Requests; i++)
        seen.Add(int.Parse(await app.Client.GetStringAsync(path)));

    return seen.Count;
}

// Aynı kayıtlarla, yalnız doğrulama açık bir sağlayıcı kurmayı dener.
static int InstancesCreatedWithValidation()
{
    var builder = WebApplication.CreateBuilder();
    builder.Logging.ClearProviders();
    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes = true;
        options.ValidateOnBuild = true;
    });

    builder.Services.AddScoped<Probe>();
    builder.Services.AddSingleton<CaptiveHolder>();

    try
    {
        using var _ = builder.Build();
        throw new InvalidOperationException("Doğrulama açıkken hatalı kayıt geçti; ölçüm geçersiz.");
    }
    catch (AggregateException)
    {
        return 0;   // Beklenen: sağlayıcı kurulamadı, tek bir nesne bile üretilmedi.
    }
}

/// <summary>Singleton karşılaştırması için ayrı tip; aynı sayaç kirlenmesin.</summary>
internal sealed class SingletonProbe
{
    private static int _created;

    public SingletonProbe() => InstanceId = Interlocked.Increment(ref _created);

    public int InstanceId { get; }
}

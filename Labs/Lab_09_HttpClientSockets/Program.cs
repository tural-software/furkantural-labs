using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Host;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

const int Calls = 20;

// Socket tükenmesini gerçekten tüketerek göstermek gerekmiyor: mekanizmayı ölçmek yeter.
// Sunucu, her isteğin geldiği istemci portunu not ediyor. Aynı port = aynı TCP bağlantısı.
var ports = new HashSet<int>();
var gate = new Lock();

await using var app = await LabsWebHost.StartAsync(application =>
    application.MapGet("/ping", (HttpContext context) =>
    {
        lock (gate) ports.Add(context.Connection.RemotePort);
        return Results.Ok();
    }));

var address = app.BaseAddress;

var services = new ServiceCollection();
services.AddHttpClient("lab", client => client.BaseAddress = address);
await using var provider = services.BuildServiceProvider();
var factory = provider.GetRequiredService<IHttpClientFactory>();

var report = new LabReport(
    title: "Lab_09 — HttpClient: kaç TCP bağlantısı açılıyor",
    claim: $"Dört yaklaşımın dördü de aynı {Calls} isteği gönderiyor ve hepsi çalışıyor. " +
           "Fark, sunucunun kaç farklı istemci portu görmesinde — port tükenmesi buradan doğar.",
    metric: "Port");

// ── 1. Her çağrıda yeni HttpClient ───────────────────────────────────────────
// using bloğu istemciyi dispose eder ama altındaki socket hemen kapanmaz: TIME_WAIT'te bekler.
await report.MeasureAsync(
    "1) Her çağrıda new HttpClient",
    Expectation.Exactly(Calls),
    () => MeasureAsync(async () =>
    {
        for (var i = 0; i < Calls; i++)
        {
            using var client = new HttpClient { BaseAddress = address };
            using var _ = await client.GetAsync("/ping");
        }
    }),
    note: $"{Calls} istek, {Calls} bağlantı. Trafik arttıkça bu sayı port havuzunu tüketir.");

// ── 2. Tek paylaşılan HttpClient ─────────────────────────────────────────────
await report.MeasureAsync(
    "2) Paylaşılan tek HttpClient",
    Expectation.Exactly(1),
    () => MeasureAsync(async () =>
    {
        using var client = new HttpClient { BaseAddress = address };
        for (var i = 0; i < Calls; i++)
            using (await client.GetAsync("/ping")) { }
    }),
    note: "Aynı bağlantı yeniden kullanıldı. Bedeli: DNS değişikliğini fark etmez.");

// ── 3. IHttpClientFactory ────────────────────────────────────────────────────
// Her CreateClient yeni bir HttpClient döndürür ama altındaki handler havuzlanır;
// bağlantı havuzu handler'ın içinde olduğu için yeniden kullanım korunur.
await report.MeasureAsync(
    "3) IHttpClientFactory",
    Expectation.Exactly(1),
    () => MeasureAsync(async () =>
    {
        for (var i = 0; i < Calls; i++)
        {
            var client = factory.CreateClient("lab");
            using var _ = await client.GetAsync("/ping");
        }
    }),
    note: "Nesne her seferinde yeni, bağlantı aynı: 2)'nin DNS sorunu olmadan aynı sonuç.");

// ── 4. Yeni HttpClient, paylaşılan handler ───────────────────────────────────
// Belirleyici olanın HttpClient değil handler olduğunun kanıtı.
await report.MeasureAsync(
    "4) new HttpClient, ortak handler",
    Expectation.Exactly(1),
    () => MeasureAsync(async () =>
    {
        using var handler = new SocketsHttpHandler();
        for (var i = 0; i < Calls; i++)
        {
            using var client = new HttpClient(handler, disposeHandler: false) { BaseAddress = address };
            using var _ = await client.GetAsync("/ping");
        }
    }),
    note: "Sorun HttpClient'ı new'lemek değil, her seferinde yeni bir handler yaratmaktı.");

return report.Print();

// Sayacı sıfırlar, işi çalıştırır ve sunucunun gördüğü farklı port sayısını döndürür.
async Task<int> MeasureAsync(Func<Task> work)
{
    lock (gate) ports.Clear();
    await work();
    lock (gate) return ports.Count;
}

using FurkanTural_Labs_Application.Diagnostics;
using Lab_13_OptionsLifetimes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

const int Onceki = 10;
const int Sonraki = 20;

var settingsPath = Path.Combine(AppContext.BaseDirectory, "lab-ayarlar.json");
WriteLimit(settingsPath, Onceki);

// İki sağlayıcı, tek fark: dosyayı izleyip izlemediği.
var watching = BuildProvider(settingsPath, reloadOnChange: true);
var notWatching = BuildProvider(settingsPath, reloadOnChange: false);

// Değişiklikten ÖNCE çözümlemek şart. IOptions ilk çözümlemede donar, singleton'lar da
// o an kurulur — yani uygulamanın açılışta ayarları okuması. Sonra çözümlenseydi hepsi
// yeni değeri görürdü ve laboratuvar hiçbir şey ölçmemiş olurdu.
var frozen = watching.GetRequiredService<IOptions<ApiSettings>>();
_ = frozen.Value;

var live = watching.GetRequiredService<LiveGuard>();
var copying = watching.GetRequiredService<CopyingGuard>();
var captive = watching.GetRequiredService<CaptiveGuard>();
_ = (live.Limit, copying.Limit, captive.Limit);

var blindMonitor = notWatching.GetRequiredService<IOptionsMonitor<ApiSettings>>();
_ = blindMonitor.CurrentValue;

// Operasyonun yaptığı iş: sunucudaki dosyada tek bir sayıyı değiştirmek.
var monitor = watching.GetRequiredService<IOptionsMonitor<ApiSettings>>();
var reloaded = new TaskCompletionSource();
using var subscription = monitor.OnChange(_ => reloaded.TrySetResult());

WriteLimit(settingsPath, Sonraki);

// Yeniden okuma dosya sistemi bildirimiyle tetiklenir, yani eşzamansızdır. Sabit bir
// bekleme koymak yerine bildirimin kendisi beklenir; gelmezse laboratuvar sessizce
// yanlış ölçmek yerine gürültüyle durur.
await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(15));

var report = new LabReport(
    title: "Lab_13 — IOptions / Snapshot / Monitor: dosya değişti, kim gördü",
    claim: $"Sunucudaki dosyada limit {Onceki} iken {Sonraki} yapıldı. Altı tüketici de aynı " +
           "ayarı okuyor; farkı yaratan seçilen arayüz ve değerin okunduğu an.",
    metric: "Okunan");

// ── 1. IOptions<T> — süreç boyunca tek değer ─────────────────────────────────
await report.MeasureAsync(
    "1) IOptions<T>",
    Expectation.Exactly(Onceki),
    () => Task.FromResult(frozen.Value.MaxRequestsPerMinute),
    note: "Singleton ve ilk çözümlemede hesaplanır; dosya değişse de bu nesne güncellenmez.");

// ── 2. IOptionsSnapshot<T> — kapsam başına yeniden hesap ─────────────────────
// Kapsam = istek. Yeni kapsam açmak, yeni bir HTTP isteğinin geldiği andır.
await report.MeasureAsync(
    "2) IOptionsSnapshot<T>, yeni kapsam",
    Expectation.Exactly(Sonraki),
    () =>
    {
        using var scope = watching.CreateScope();
        var snapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<ApiSettings>>();
        return Task.FromResult(snapshot.Value.MaxRequestsPerMinute);
    },
    note: "Her kapsamda yeniden hesaplanır; değişimi bir sonraki istekte görür.");

// ── 3. IOptionsMonitor<T>, kullanım noktasında okunuyor ──────────────────────
await report.MeasureAsync(
    "3) Singleton, CurrentValue kullanımda",
    Expectation.Exactly(Sonraki),
    () => Task.FromResult(live.Limit),
    note: "Singleton servislerde tek doğru seçenek: değer erişildiği anda okunuyor.");

// ── 4. Aynı arayüz, constructor'da kopyalanmış ───────────────────────────────
await report.MeasureAsync(
    "4) Singleton, CurrentValue ctor'da kopyalanmış",
    Expectation.Exactly(Onceki),
    () => Task.FromResult(copying.Limit),
    note: "Arayüz doğru, an yanlış. Kopyalandığı anda IOptions davranışına dönülüyor.");

// ── 5. Scoped arayüz, singleton'a enjekte edilmiş ────────────────────────────
// Yazının "en kötü hâlde hiçbir şey patlamaz" dediği yer; kapsam doğrulaması kapalı.
await report.MeasureAsync(
    "5) Singleton'a IOptionsSnapshot enjekte",
    Expectation.Exactly(Onceki),
    () => Task.FromResult(captive.Limit),
    note: "Captive dependency: kök kapsamdan alınan snapshot süresiz tutuluyor, değer donuyor.");

// ── 6. Dosya izlenmiyor ──────────────────────────────────────────────────────
await report.MeasureAsync(
    "6) reloadOnChange kapalı, IOptionsMonitor",
    Expectation.Exactly(Onceki),
    () => Task.FromResult(blindMonitor.CurrentValue.MaxRequestsPerMinute),
    note: "Arayüz canlı ama kaynak sessiz; izleme sağlayıcıda açılmazsa değişim hiç duyulmaz.");

return report.Print();

static void WriteLimit(string path, int limit)
    => File.WriteAllText(path, $$"""{ "Api": { "MaxRequestsPerMinute": {{limit}} } }""");

static ServiceProvider BuildProvider(string path, bool reloadOnChange)
{
    var configuration = new ConfigurationBuilder()
        .AddJsonFile(path, optional: false, reloadOnChange: reloadOnChange)
        .Build();

    var services = new ServiceCollection();
    services.AddOptions<ApiSettings>().Bind(configuration.GetSection("Api"));

    services.AddSingleton<LiveGuard>();
    services.AddSingleton<CopyingGuard>();

    // Kapsam doğrulaması kapalı olduğu için bu kayıt sorunsuz kurulur ve sorunsuz çalışır.
    // Development'ta ValidateScopes açıktır ve aynı kayıt istisna fırlatır; ölçülen sessiz
    // hâli görmek için varsayılan (Production) davranış korunuyor.
    services.AddSingleton<CaptiveGuard>();

    return services.BuildServiceProvider();
}

using System.Reflection;
using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Persistence.Cache;
using FurkanTural_Labs_Persistence.Contexts;
using FurkanTural_Labs_Persistence.Registration;
using Lab_11_MemoryVsDistributedCache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

const string Key = "blog:42";
const string Value = "42 numaralı yazının gövdesi";

var assembly = Assembly.GetExecutingAssembly();

// Veritabanının var olduğundan emin ol, sonra cache tablosunu kur.
await using (var bootstrap = await LabsHost.StartAsync(assembly))
await using (var scope = bootstrap.CreateAsyncScope())
{
    await SqlCacheTable.EnsureAsync(scope.ServiceProvider.GetRequiredService<LabsDbContext>());
}

var connectionString = LabsHost.BuildConfiguration(assembly)
    .GetConnectionString(PersistenceServiceRegistration.ConnectionName)!;

await using var servers = new ServerPair(() => BuildInstance(connectionString));

// Önceki koşumdan kalan kayıt ölçümü kirletmesin.
await ServerPair.DistributedOf(servers.First).RemoveAsync(Key);

var report = new LabReport(
    title: "Lab_11 — Load balancer arkasında iki sunucu, tek anahtar",
    claim: "Her senaryoda iki sunucu var ve aynı anahtar soruluyor. Ölçülen şey, " +
           "kaçının değeri okuyabildiği.",
    metric: "Gören");

// ── 1. Bellek içi cache, bir sunucu yazdı ────────────────────────────────────
await report.MeasureAsync(
    "1) IMemoryCache, A yazdı",
    Expectation.Exactly(1),
    () =>
    {
        ServerPair.MemoryOf(servers.First).Set(Key, Value);
        return Task.FromResult(servers.MemoryReaders(Key));
    },
    note: "Yazan sunucu görüyor, diğeri görmüyor. İkinci sunucu ekleyince başlayan tutarsızlık bu.");

// ── 2. Aynı iş, dağıtık cache ────────────────────────────────────────────────
await report.MeasureAsync(
    "2) IDistributedCache, A yazdı",
    Expectation.Exactly(2),
    async () =>
    {
        await ServerPair.DistributedOf(servers.First).SetStringAsync(Key, Value, FiveMinutes());
        return await servers.DistributedReadersAsync(Key);
    },
    note: "Kayıt süreçlerin dışında; yazan da yazmayan da aynı yerden okuyor.");

// ── 3. Geçersizleştirme yayılmıyor ───────────────────────────────────────────
// Gerçek hayattaki sıra: iki sunucu da kendi kopyasını üretti, sonra veri değişti ve
// yazma isteğini alan sunucu anahtarı düşürdü.
await report.MeasureAsync(
    "3) IMemoryCache, A anahtarı düşürdü",
    Expectation.Exactly(1),
    () =>
    {
        ServerPair.MemoryOf(servers.First).Set(Key, Value);
        ServerPair.MemoryOf(servers.Second).Set(Key, Value);

        ServerPair.MemoryOf(servers.First).Remove(Key);

        return Task.FromResult(servers.MemoryReaders(Key));
    },
    note: "Doğru sayı 0'dı. B bayat kopyayı süresi dolana kadar servis etmeye devam eder.");

// ── 4. Aynı geçersizleştirme, dağıtık cache ──────────────────────────────────
await report.MeasureAsync(
    "4) IDistributedCache, A anahtarı düşürdü",
    Expectation.Exactly(0),
    async () =>
    {
        await ServerPair.DistributedOf(servers.First).SetStringAsync(Key, Value, FiveMinutes());
        await ServerPair.DistributedOf(servers.First).RemoveAsync(Key);

        return await servers.DistributedReadersAsync(Key);
    },
    note: "Tek yerden silindi, ikisi de göremiyor. Geçersizleştirmenin yayılması gerekmiyor.");

// ── 5. Yeniden başlatma ──────────────────────────────────────────────────────
await report.MeasureAsync(
    "5) IMemoryCache, iki sunucu yeniden başladı",
    Expectation.Exactly(0),
    async () =>
    {
        ServerPair.MemoryOf(servers.First).Set(Key, Value);
        ServerPair.MemoryOf(servers.Second).Set(Key, Value);

        await servers.RestartAsync();

        return servers.MemoryReaders(Key);
    },
    note: "Deploy sonrası ilk isteklerin hepsi ıskalar; cache soğuk başlar.");

// ── 6. Aynı yeniden başlatma, dağıtık cache ──────────────────────────────────
await report.MeasureAsync(
    "6) IDistributedCache, iki sunucu yeniden başladı",
    Expectation.Exactly(2),
    async () =>
    {
        await ServerPair.DistributedOf(servers.First).SetStringAsync(Key, Value, FiveMinutes());

        await servers.RestartAsync();

        return await servers.DistributedReadersAsync(Key);
    },
    note: "Kayıt süreçten bağımsız yaşıyor; yeni kalkan sunucular onu hazır buluyor.");

// Kalıntı bırakma: tablo paylaşılan veritabanında duruyor.
await ServerPair.DistributedOf(servers.First).RemoveAsync(Key);

return report.Print();

// Üst düzey deyimler alan ya da özellik taşıyamaz; yerel fonksiyon taşıyabilir.
static DistributedCacheEntryOptions FiveMinutes()
    => new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };

// Tek bir sunucunun servis kabı. İki kap kurulunca iki ayrı IMemoryCache olur —
// load balancer arkasındaki iki instance'ın farkı tam olarak budur.
static ServiceProvider BuildInstance(string connectionString)
{
    var services = new ServiceCollection();

    services.AddMemoryCache();
    services.AddDistributedSqlServerCache(options =>
    {
        options.ConnectionString = connectionString;
        options.SchemaName = SqlCacheTable.SchemaName;
        options.TableName = SqlCacheTable.TableName;
    });

    return services.BuildServiceProvider();
}

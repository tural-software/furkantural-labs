using System.Reflection;
using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Persistence.Contexts;
using FurkanTural_Labs_Persistence.Diagnostics;
using FurkanTural_Labs_Persistence.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

const int BlogsToLoad = 100;

var assembly = Assembly.GetExecutingAssembly();

// Sayaç dışarıda yaratılıp iki provider'a da veriliyor: lazy loading proxy'leri
// MODEL düzeyinde açılır, tek bir context üzerinde açılıp kapatılamaz. Sayaç
// paylaşılmazsa proxy'li senaryo ayrı bir sayaca yazar ve tabloda karşılaştırılamaz.
var counter = new QueryCounter();

await using var plain = await LabsHost.StartAsync(assembly, sharedCounter: counter);
await using var proxied = await LabsHost.StartAsync(assembly, useLazyLoadingProxies: true, sharedCounter: counter);

var report = new LabReport(
    title: "Lab_04 — N+1 Problemi: Include() ve Lazy Loading Tuzakları",
    claim: $"{BlogsToLoad} bloğun yorumlarına dokunmak, Include olmadan 1+{BlogsToLoad} sorgu üretir; " +
           "Include ile 1 sorguya iner. Fark kod okunarak değil sayılarak görülür.",
    counter: counter);

// ── 1. Include yok: döngü içinde explicit load ───────────────────────────────
// Klasik N+1. En azından dürüst: sorguları sen yazdın, kodda görünüyorlar.
await report.ScenarioAsync(
    "1) Explicit load, döngüde",
    Expectation.Exactly(1 + BlogsToLoad),
    () => WithContextAsync(plain, async db =>
    {
        var blogs = await db.Blogs.OrderBy(b => b.Id).Take(BlogsToLoad).ToListAsync();
        foreach (var blog in blogs)
            await db.Entry(blog).Collection(b => b.Comments).LoadAsync();

        return blogs.Sum(b => b.Comments.Count);
    }),
    note: "1 sorgu blogları, 100 sorgu yorumları çeker.");

// ── 2. Lazy loading proxy: kodda tek bir sorgu yok, veritabanında 101 tane var ─
// Asıl tuzak bu. Yukarıdaki döngüyü siler, tek satır LINQ yazarsın; maliyet aynı kalır
// ama artık görünmez. Kod incelemesinde kimse fark etmez.
await report.ScenarioAsync(
    "2) Lazy loading proxy",
    Expectation.Exactly(1 + BlogsToLoad),
    () => WithContextAsync(proxied, async db =>
    {
        var blogs = await db.Blogs.OrderBy(b => b.Id).Take(BlogsToLoad).ToListAsync();
        return blogs.Sum(b => b.Comments.Count);   // ← tek satır, 100 gizli sorgu
    }),
    note: "Kodda hiçbir yükleme çağrısı yok; sayı 1) ile birebir aynı.");

// ── 3. Include: tek sorgu ─────────────────────────────────────────────────────
await report.ScenarioAsync(
    "3) Include",
    Expectation.Exactly(1),
    () => WithContextAsync(plain, async db =>
    {
        var blogs = await db.Blogs
            .Include(b => b.Comments)
            .OrderBy(b => b.Id)
            .Take(BlogsToLoad)
            .ToListAsync();

        return blogs.Sum(b => b.Comments.Count);
    }),
    note: "JOIN'li tek sorgu. Satırlar blog başına tekrar eder (kartezyen genişleme).");

// ── 4. AsSplitQuery: "Include = 1 sorgu" ezberini bozar ───────────────────────
// Include otomatik olarak tek sorgu demek değildir; split query kartezyen genişlemeyi
// keser ama sorgu sayısını artırır. Doğru seçim veri şekline bağlıdır, ezbere değil.
await report.ScenarioAsync(
    "4) Include + AsSplitQuery",
    Expectation.Exactly(2),
    () => WithContextAsync(plain, async db =>
    {
        var blogs = await db.Blogs
            .Include(b => b.Comments)
            .OrderBy(b => b.Id)
            .Take(BlogsToLoad)
            .AsSplitQuery()
            .ToListAsync();

        return blogs.Sum(b => b.Comments.Count);
    }),
    note: "Bloglar için 1, yorumlar için 1. Tekrar eden satır yok.");

// ── 5. Projeksiyon: yalnız ihtiyacın kadarını çek ─────────────────────────────
// Sayı lazımsa entity yüklemek israftır: tek sorgu, çok daha az veri, change tracker boş.
await report.ScenarioAsync(
    "5) Projeksiyon (Select)",
    Expectation.Exactly(1),
    () => WithContextAsync(plain, async db =>
    {
        var rows = await db.Blogs
            .OrderBy(b => b.Id)
            .Take(BlogsToLoad)
            .Select(b => new { b.Id, b.Title, CommentCount = b.Comments.Count })
            .ToListAsync();

        return rows.Sum(r => r.CommentCount);
    }),
    note: "Yorum gövdeleri hiç taşınmaz; sayım veritabanında yapılır.");

return report.Print();

// Her senaryo kendi scope'unda çalışır: paylaşılan bir context, önceki senaryonun
// önbelleğe aldığı entity'ler yüzünden sonrakinin sorgu sayısını düşürürdü.
static async Task WithContextAsync<T>(ServiceProvider provider, Func<LabsDbContext, Task<T>> work)
{
    await using var scope = provider.CreateAsyncScope();
    await work(scope.ServiceProvider.GetRequiredService<LabsDbContext>());
}

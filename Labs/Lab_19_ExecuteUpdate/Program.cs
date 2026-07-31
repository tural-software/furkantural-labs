using System.Reflection;
using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Persistence.Registration;
using FurkanTural_Labs_Persistence.Sandbox;
using FurkanTural_Labs_Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Hedef küme: son 1.000 yazı. Tüm senaryolar aynı satırlara dokunur ve hepsi geri alınır.
var target = LabsSeeder.PublishedAtOf(LabsSeeder.BlogCount - 1_000);
const int TargetRows = 1_000;

// SQL Server sağlayıcısının varsayılan parti boyu 42 komut; 1.000 UPDATE bu partilere
// bölünür. Beklenen komut sayısı bu aritmetikten gelir, ölçümden değil:
// 1 SELECT + ceil(1000 / 42) parti.
const int MaxBatchSize = 42;
var classicCommands = 1 + (int)Math.Ceiling(TargetRows / (double)MaxBatchSize);

await using var provider = await LabsHost.StartAsync(Assembly.GetExecutingAssembly());
var counter = provider.GetRequiredService<IQueryCounter>();

var report = new LabReport(
    title: "Lab_19 — ExecuteUpdateAsync: değiştirmek için yüklemek gerekmiyor",
    claim: $"Aynı {TargetRows:N0} satır iki yoldan güncelleniyor. Klasik yol satır sayısıyla " +
           "büyüyen bir komut hacmi üretir; toplu komut tek cümlede biter.",
    counter: counter);

// ── 1. Klasik: yükle, döngüde değiştir, kaydet ───────────────────────────────
// Tek bir bool alanı için entity'nin bütün kolonları çekilir, her satır nesneye dönüşür
// ve Change Tracker'a snapshot yazılır. Bellek sütunu bu bedeli gösteriyor.
await report.ScenarioAsync(
    "1) Yükle, döngü, SaveChanges",
    Expectation.Exactly(classicCommands),
    () => provider.ScopedAsync(db => DataSandbox.RollbackAsync(db, async ctx =>
    {
        var blogs = await ctx.Blogs.Where(b => b.PublishedAt > target).ToListAsync();

        foreach (var blog in blogs)
            blog.IsActive = false;

        return await ctx.SaveChangesAsync();
    })),
    note: $"1 SELECT + {classicCommands - 1} UPDATE partisi. Parti boyu sabit, komut sayısı satırla büyür.");

// ── 2. Toplu komut ───────────────────────────────────────────────────────────
await report.ScenarioAsync(
    "2) ExecuteUpdateAsync",
    Expectation.Exactly(1),
    () => provider.ScopedAsync(db => DataSandbox.RollbackAsync(db, async ctx =>
        await ctx.Blogs
            .Where(b => b.PublishedAt > target)
            .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.IsActive, false)))),
    note: "Tek UPDATE ... WHERE. Hiçbir satır nesneye dönüşmedi, tracker'a hiçbir şey yazılmadı.");

// ── 3. Yeni değeri satırın kendisinden türetmek ──────────────────────────────
// Klasik yolda bunun için önce okumak gerekir; burada artırma veritabanı tarafında yapılır.
await report.ScenarioAsync(
    "3) ExecuteUpdate, hesaplanmış değer",
    Expectation.Exactly(1),
    () => provider.ScopedAsync(db => DataSandbox.RollbackAsync(db, async ctx =>
        await ctx.Blogs
            .Where(b => b.PublishedAt > target)
            .ExecuteUpdateAsync(setters => setters.SetProperty(b => b.ViewCount, b => b.ViewCount + 1)))),
    note: "Okumadan artırma: sayaç güncellemelerinde yarış koşulunu da ortadan kaldırır.");

// ── 4. Denetim alanları kendiliğinden dolmaz ─────────────────────────────────
// Toplu komut SaveChanges'ten geçmez: UpdatedAt dolduran override'ınız, soft-delete
// mantığınız ve SaveChanges interceptor'larınız tetiklenmez. Komut sayısı değişmiyor —
// değişen, sorumluluğun kimde olduğu.
await report.ScenarioAsync(
    "4) ExecuteUpdate + denetim alanı",
    Expectation.Exactly(1),
    () => provider.ScopedAsync(db => DataSandbox.RollbackAsync(db, async ctx =>
        await ctx.Blogs
            .Where(b => b.PublishedAt > target)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(b => b.IsActive, false)
                .SetProperty(b => b.UpdatedAt, DateTime.UtcNow)))),
    note: "İkinci SetProperty olmasaydı UpdatedAt boş kalırdı ve kimse fark etmezdi.");

// ── 5. ExecuteDelete soft-delete tanımaz ─────────────────────────────────────
// Entity ISoftDeletable olsa bile bu komut satırı gerçekten siler; bağımlı kayıtların
// akıbeti veritabanındaki FK kuralına kalır (burada ON DELETE CASCADE tanımlı).
await report.ScenarioAsync(
    "5) ExecuteDeleteAsync",
    Expectation.Exactly(1),
    () => provider.ScopedAsync(db => DataSandbox.RollbackAsync(db, async ctx =>
        await ctx.Blogs
            .Where(b => b.PublishedAt > target)
            .ExecuteDeleteAsync())),
    note: "IsDeleted işaretlenmedi, satır silindi. Cascade da EF'in değil veritabanının işi.");

return report.Print();

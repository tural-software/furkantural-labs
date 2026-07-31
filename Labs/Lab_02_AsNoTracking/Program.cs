using System.Reflection;
using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Persistence.Registration;
using FurkanTural_Labs_Persistence.Sandbox;
using FurkanTural_Labs_Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

await using var provider = await LabsHost.StartAsync(Assembly.GetExecutingAssembly());
var counter = provider.GetRequiredService<IQueryCounter>();

var report = new LabReport(
    title: "Lab_02 — AsNoTracking(): okuma sorgularının görünmeyen maliyeti",
    claim: $"Aynı {LabsSeeder.BlogCount:N0} satır beş farklı şekilde okunuyor. Sorgu sayısı hepsinde 1; " +
           "değişen tek şey Change Tracker'da kaç nesnenin snapshot'ıyla birlikte tutulduğu.",
    counter: counter,
    metric: "İzlenen");

// ── 1. Varsayılan: her satır takip altında ───────────────────────────────────
// Ödediğiniz şey nesnelerin kendisi değil, her biri için tutulan orijinal değer kopyası.
await report.MeasureAsync(
    "1) Varsayılan (tracking açık)",
    Expectation.Exactly(LabsSeeder.BlogCount),
    () => provider.ScopedAsync(async db =>
    {
        _ = await db.Blogs.ToListAsync();
        return db.ChangeTracker.Entries().Count();
    }),
    note: "Bellek sütunu snapshot'ların bedelini gösterir; 2) ile karşılaştırın.");

// ── 2. AsNoTracking ──────────────────────────────────────────────────────────
await report.MeasureAsync(
    "2) AsNoTracking()",
    Expectation.Exactly(0),
    () => provider.ScopedAsync(async db =>
    {
        _ = await db.Blogs.AsNoTracking().ToListAsync();
        return db.ChangeTracker.Entries().Count();
    }),
    note: "Aynı satırlar, aynı tek sorgu; takip defteri boş.");

// ── 3. Projeksiyon zaten takip edilmez ───────────────────────────────────────
// Sık görülen boş yere yazılmış AsNoTracking: entity dönmeyen sorguda takip edilecek
// bir şey yoktur. Takip, entity tipiyle gelir; DTO ile değil.
await report.MeasureAsync(
    "3) Projeksiyon (tracking açık)",
    Expectation.Exactly(0),
    () => provider.ScopedAsync(async db =>
    {
        _ = await db.Blogs
            .Select(b => new BlogSummary(b.Id, b.Title))
            .ToListAsync();

        return db.ChangeTracker.Entries().Count();
    }),
    note: "AsNoTracking yazılmadı ama takip yine yok: dönen tip entity değil.");

// ── 4. Aynı sorgu iki kez: identity map ──────────────────────────────────────
// Takip yalnızca maliyet değildir. İkinci okuma yeni nesne üretmez, aynı örneği döndürür;
// entity'yi okuyup güncelleyeceğiniz senaryolarda güvendiğiniz davranış budur.
await report.MeasureAsync(
    "4) Aynı sorgu iki kez (tracking açık)",
    Expectation.Exactly(LabsSeeder.BlogCount),
    () => provider.ScopedAsync(async db =>
    {
        var first = await db.Blogs.ToListAsync();
        var second = await db.Blogs.ToListAsync();

        // Aynı satır, aynı nesne: iki liste de takip defterindeki tek örneği gösterir.
        if (!ReferenceEquals(first[0], second[0]))
            throw new InvalidOperationException("Identity map beklenen davranışı göstermedi.");

        return db.ChangeTracker.Entries().Count();
    }),
    note: "İki sorgu, tek nesne kümesi; sayı ikiye katlanmıyor.");

// ── 5. AsNoTracking ile okunanı güncellemeye çalışmak ────────────────────────
// Takibin bedeli kadar faydası da var. Takip edilmeyen nesnede yapılan değişikliği
// SaveChanges göremez: hata da vermez, tek bir komut bile üretmez.
await report.MeasureAsync(
    "5) AsNoTracking + SaveChanges",
    Expectation.Exactly(0),
    () => provider.ScopedAsync(db => DataSandbox.RollbackAsync(db, async ctx =>
    {
        var blog = await ctx.Blogs.AsNoTracking().OrderBy(b => b.Id).FirstAsync();
        blog.Title = "değişti";

        var written = await ctx.SaveChangesAsync();
        if (written != 0)
            throw new InvalidOperationException($"Beklenmedik yazma: {written} satır.");

        return ctx.ChangeTracker.Entries().Count();
    })),
    note: "Değişiklik sessizce kayboldu; güncelleyecekseniz AsNoTracking yanlış araçtır.");

return report.Print();

/// <summary>Entity olmayan dönüş tipi: takip mekanizması bunu görmez.</summary>
internal sealed record BlogSummary(int Id, string Title);

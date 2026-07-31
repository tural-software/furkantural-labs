using System.Reflection;
using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Domain.Entities;
using FurkanTural_Labs_Persistence.Registration;
using FurkanTural_Labs_Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Sınırlar tohumdan türetilir, sorguyla bulunmaz: PublishedAt = Epoch + i saat olduğu için
// "son 1.000 yazı" ile "son 100 yazı" tam olarak bilinen satır sayılarına karşılık gelir.
var lastThousand = LabsSeeder.PublishedAtOf(LabsSeeder.BlogCount - 1_000);
var lastHundred = LabsSeeder.PublishedAtOf(LabsSeeder.BlogCount - 100);

const int Popular = 4_000;   // ViewCount eşiği
const int PageSize = 10;

await using var provider = await LabsHost.StartAsync(Assembly.GetExecutingAssembly());
var counter = provider.GetRequiredService<IQueryCounter>();

var report = new LabReport(
    title: "Lab_01 — IQueryable vs IEnumerable: sorgu nerede çalışır",
    claim: "Dört zincir de aynı soruyu sorar ve dördü de tek sorgu çalıştırır. " +
           "Fark sorgu sayısında değil, veritabanından çıkan satır sayısındadır.",
    counter: counter,
    metric: "Satır");

// ── 1. Dönüş tipi IEnumerable: zincirin tamamı bellekte ──────────────────────
// Yazının 4. bölümündeki repository hatası. Tip değişimi tek satır, bedeli tüm tablo.
await report.MeasureAsync(
    "1) Repository IEnumerable döndürüyor",
    Expectation.Exactly(LabsSeeder.BlogCount),
    () => provider.ScopedAsync(db =>
    {
        IEnumerable<Blog> source = db.Blogs;          // ← tip burada düştü

        _ = source
            .Where(b => b.ViewCount > Popular)        // LINQ to Objects
            .OrderBy(b => b.PublishedAt)              // bellekte sıralama
            .Take(PageSize)
            .ToList();

        return Task.FromResult(counter.RowCount);
    }),
    note: "Üretilen SQL'de ne WHERE ne TOP var; 10 kayıt için tablonun tamamı ağdan geçti.");

// ── 2. Zincirin ortasında ToList(): sınır erken kapandı ──────────────────────
// Ön filtre SQL'e gitti, geri kalanı bellekte kaldı. Hata daha küçük ama aynı cinsten.
await report.MeasureAsync(
    "2) Zincirin ortasında ToList()",
    Expectation.Exactly(1_000),
    () => provider.ScopedAsync(async db =>
    {
        var loaded = await db.Blogs
            .Where(b => b.PublishedAt > lastThousand)   // SQL WHERE
            .ToListAsync();                             // ← sınır burada kapandı

        _ = loaded
            .Where(b => b.ViewCount > Popular)          // bellekte
            .Take(PageSize)
            .ToList();

        return counter.RowCount;
    }),
    note: "1.000 satır belleğe çekildi, onda biri kullanıldı.");

// ── 3. Bilinçli AsEnumerable: geçiş noktası olabildiğince sona itilmiş ───────
// SQL'e çevrilemeyen bir kural zorunluysa doğru yol bu: önce SQL süzsün, sonra bellek.
await report.MeasureAsync(
    "3) Bilinçli AsEnumerable",
    Expectation.Exactly(100),
    () => provider.ScopedAsync(async db =>
    {
        var narrowed = await db.Blogs
            .Where(b => b.PublishedAt > lastHundred)    // SQL ön filtresi elemeyi yapıyor
            .Select(b => new BlogRow(b.Id, b.Title, b.ViewCount))
            .ToListAsync();

        _ = narrowed
            .Where(IsWorthReading)                      // SQL'e çevrilemez, bilerek bellekte
            .Take(PageSize)
            .ToList();

        return counter.RowCount;
    }),
    note: "Geçiş kaçınılmazsa maliyeti belirleyen şey, geçişten önce kaç satır kaldığıdır.");

// ── 4. Baştan sona IQueryable ────────────────────────────────────────────────
await report.MeasureAsync(
    "4) Baştan sona IQueryable",
    Expectation.Exactly(PageSize),
    () => provider.ScopedAsync(async db =>
    {
        _ = await db.Blogs
            .Where(b => b.ViewCount > Popular)
            .OrderBy(b => b.PublishedAt)
            .Take(PageSize)
            .ToListAsync();

        return counter.RowCount;
    }),
    note: "WHERE ve TOP(10) sunucuda; ağdan 10 satır geçti.");

return report.Print();

// Gövdesi ifade ağacına çevrilemeyen bir kural: EF bunu Where içinde göremez.
// Yazının 6. bölümündeki durum — çözüm kuralı bozmak değil, geçişi bilinçli ve geç yapmak.
bool IsWorthReading(BlogRow row) => row.ViewCount > Popular && row.Title.Length > 10;

/// <summary>Yalnız gereken üç kolonu taşıyan projeksiyon satırı.</summary>
internal sealed record BlogRow(int Id, string Title, int ViewCount);

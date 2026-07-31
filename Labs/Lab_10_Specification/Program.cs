using System.Reflection;
using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Persistence.Registration;
using FurkanTural_Labs_Persistence.Seed;
using FurkanTural_Labs_Persistence.Specifications;
using Lab_10_Specification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Tohum: ilk 1.000 blog yorumlu, PublishedAt = Epoch + i saat.
var firstThousand = LabsSeeder.PublishedAtOf(1_000);
var firstHundredOne = LabsSeeder.PublishedAtOf(101);

const int PageSize = 20;

await using var provider = await LabsHost.StartAsync(Assembly.GetExecutingAssembly());
var counter = provider.GetRequiredService<IQueryCounter>();

var report = new LabReport(
    title: "Lab_10 — Specification Pattern: koşul nereye çevriliyor",
    claim: "Specification'ın değeri repository'yi ince tutmasında değil, koşulu SQL'e " +
           "taşıyabilmesinde. Koşulun tipi Expression yerine Func olduğunda desen aynen " +
           "durur, sorgu ise tabloyu belleğe çeker.",
    counter: counter,
    metric: "Satır");

// ── 1. Sızdıran specification: Func<Blog, bool> ──────────────────────────────
// Kod okunduğunda hiçbir şey yanlış görünmez; dönen sonuç da doğrudur.
await report.MeasureAsync(
    "1) Sızdıran spec (Func)",
    Expectation.Exactly(LabsSeeder.BlogCount),
    () => provider.ScopedAsync(db =>
    {
        var spec = new LeakyPublishedBeforeSpec(firstThousand);
        var matched = LeakySpecificationEvaluator.Apply(db.Blogs.AsNoTracking(), spec).ToList();

        if (matched.Count != 1_000)
            throw new InvalidOperationException($"Sonuç beklenmedik: {matched.Count} kayıt.");

        return Task.FromResult(counter.RowCount);
    }),
    note: "Sonuç doğru: 1.000 kayıt. Bedeli, 1.000'i bulmak için 10.000 satır taşımak.");

// ── 2. Aynı koşul, Expression olarak ─────────────────────────────────────────
await report.MeasureAsync(
    "2) Doğru spec (Expression)",
    Expectation.Exactly(1_000),
    () => provider.ScopedAsync(async db =>
    {
        var spec = new PublishedBeforeSpec(firstThousand);
        _ = await SpecificationEvaluator.Apply(db.Blogs, spec).ToListAsync();

        return counter.RowCount;
    }),
    note: "Aynı koşul, aynı sonuç; koşul bu kez WHERE'e çevrildi.");

// ── 3. İki specification'ı birleştirmek ──────────────────────────────────────
// Deseni değerli kılan da bu: yeni bir repository metodu yazmadan koşul eklemek.
await report.MeasureAsync(
    "3) İki spec, And ile birleşik",
    Expectation.Exactly(101),
    () => provider.ScopedAsync(async db =>
    {
        var spec = new HasCommentsSpec().And(new PublishedBeforeSpec(firstHundredOne));
        _ = await SpecificationEvaluator.Apply(db.Blogs, spec).ToListAsync();

        return counter.RowCount;
    }),
    note: "İki ifade ağacı tek parametreye yeniden bağlandı; tek WHERE, iki koşul.");

// ── 4. Sayfalama da specification'ın parçası ─────────────────────────────────
await report.MeasureAsync(
    "4) Sayfalama spec'in içinde",
    Expectation.Exactly(PageSize),
    () => provider.ScopedAsync(async db =>
    {
        var spec = new CommentedBlogsPageSpec(skip: 0, take: PageSize);
        _ = await SpecificationEvaluator.Apply(db.Blogs, spec).ToListAsync();

        return counter.RowCount;
    }),
    note: "Skip/Take de ifade ağacına giriyor; servis katmanı sayfalamayı bellekte yapmıyor.");

return report.Print();

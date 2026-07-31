using System.Reflection;
using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Domain.Entities;
using FurkanTural_Labs_Persistence.Contexts;
using FurkanTural_Labs_Persistence.Registration;
using FurkanTural_Labs_Persistence.Sandbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Zaman dilimi makineden alınmıyor: DateTime.Now kullanılsaydı ölçüm, testi çalıştıran
// makinenin saat dilimine bağlı olurdu ve UTC'de duran bir sunucuda hata görünmezdi.
// İstanbul'un +03:00'ı sabit yazılıyor; "yerel" burada bu offset demek.
var offset = TimeSpan.FromHours(3);

DateTime[] instants =
[
    new(2026, 1, 15, 9, 30, 0, DateTimeKind.Utc),
    new(2026, 6, 1, 23, 45, 12, DateTimeKind.Utc),   // +03:00'da ertesi güne taşar
    new(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc)      // -03:00'da bir önceki güne taşar
];

await using var provider = await LabsHost.StartAsync(Assembly.GetExecutingAssembly());
var counter = provider.GetRequiredService<IQueryCounter>();

var report = new LabReport(
    title: "Lab_12 — DateTime vs DateTimeOffset: zaman dilimi nerede kayboluyor",
    claim: "Aynı üç an beş farklı şekilde yazılıp geri okunuyor. Sorun tarihin yanlış " +
           "yazılması değil, hangi zaman dilimine ait olduğunun saklanmaması.",
    counter: counter,
    metric: "Doğru");

// ── 1. datetime2 sütununa yerel duvar saati yazmak ───────────────────────────
// DateTime.Now'ın veritabanına gitmesi. Sütun rakamları saklar, Kind'ı saklamaz;
// geri okunduğunda değerin UTC mi yerel mi olduğunu söyleyen hiçbir şey kalmaz.
await report.MeasureAsync(
    "1) datetime2 ← yerel duvar saati",
    Expectation.Exactly(0),
    () => provider.ScopedAsync(db => DataSandbox.RollbackAsync(db, async ctx =>
    {
        var rows = await WriteAndReadAsync(ctx, utc => NewBlog(WallClock(utc, offset), default));

        // Uygulamanın standart varsayımı: sütundaki değer UTC'dir.
        return rows
            .Where((row, index) => DateTime.SpecifyKind(row.PublishedAt, DateTimeKind.Utc) == instants[index])
            .Count();
    })),
    note: "Üç yazımın üçü de üç saat kaymış geri geldi; ne istisna var ne uyarı.");

// ── 2. datetime2 sütununa UTC yazmak ─────────────────────────────────────────
// Rakamlar doğru gider, etiket yine kaybolur: geri gelen değerin Kind'ı Unspecified'dır.
// SpecifyKind gereksiz görünen ama gerekli olan adım; onsuz JSON'a 'Z' eklenmez.
await report.MeasureAsync(
    "2) datetime2 ← UTC + okurken SpecifyKind",
    Expectation.Exactly(3),
    () => provider.ScopedAsync(db => DataSandbox.RollbackAsync(db, async ctx =>
    {
        var rows = await WriteAndReadAsync(ctx, utc => NewBlog(utc, default));

        if (rows.Any(r => r.PublishedAt.Kind != DateTimeKind.Unspecified))
            throw new InvalidOperationException("Sütun Kind'ı korumuş görünüyor; ölçüm geçersiz.");

        return rows
            .Where((row, index) => DateTime.SpecifyKind(row.PublishedAt, DateTimeKind.Utc) == instants[index])
            .Count();
    })),
    note: "Değer korundu, Kind yine Unspecified döndü; işareti geri koymak sizin işiniz.");

// ── 3. datetimeoffset sütunu ─────────────────────────────────────────────────
// Offset veritabanına kadar gider; hem an hem de olayın yerel bağlamı korunur.
await report.MeasureAsync(
    "3) datetimeoffset ← yerel saat + offset",
    Expectation.Exactly(3),
    () => provider.ScopedAsync(db => DataSandbox.RollbackAsync(db, async ctx =>
    {
        var rows = await WriteAndReadAsync(
            ctx, utc => NewBlog(default, new DateTimeOffset(WallClock(utc, offset), offset)));

        if (rows.Any(r => r.PublishedAtOffset.Offset != offset))
            throw new InvalidOperationException("Offset korunmadı; ölçüm geçersiz.");

        return rows
            .Where((row, index) => row.PublishedAtOffset.UtcDateTime == instants[index])
            .Count();
    })),
    note: "Offset de saklandığı için hem an hem yerel karşılığı geri geliyor.");

// ── 4. Karşılaştırmada Kind yok sayılır ──────────────────────────────────────
// Veritabanına hiç gitmeyen, tamamen bellekte olan tuzak: aynı anı gösteren iki değer
// eşit değildir, çünkü karşılaştırma yalnız rakamlara bakar.
await report.MeasureAsync(
    "4) DateTime karşılaştırması",
    Expectation.Exactly(0),
    () => Task.FromResult(instants.Count(utc => utc == WallClock(utc, offset))),
    note: "Aynı anın iki temsili; == ikisini farklı sayıyor. Filtrede sessiz hata kaynağı.");

// ── 5. DateTimeOffset karşılaştırması ────────────────────────────────────────
await report.MeasureAsync(
    "5) DateTimeOffset karşılaştırması",
    Expectation.Exactly(3),
    () => Task.FromResult(instants.Count(utc =>
        new DateTimeOffset(utc, TimeSpan.Zero) == new DateTimeOffset(WallClock(utc, offset), offset))),
    note: "Aynı üç an, aynı iki temsil; offset hesaba katıldığı için eşitler.");

return report.Print();

// Bir UTC anının verilen offset'teki duvar saati karşılığı; Kind bilerek Unspecified.
static DateTime WallClock(DateTime utc, TimeSpan offset)
    => DateTime.SpecifyKind(utc + offset, DateTimeKind.Unspecified);

// Ölçüm için tek kullanımlık kayıt. İki tarih sütunu da bu satırda taşınır.
static Blog NewBlog(DateTime publishedAt, DateTimeOffset publishedAtOffset) => new()
{
    Title = "Zaman dilimi ölçümü",
    Content = "Lab_12 tarafından yazıldı; transaction geri alınır.",
    PublishedAt = publishedAt,
    PublishedAtOffset = publishedAtOffset,
    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
};

// Yazıp geri okur. AsNoTracking şart: takip edilen entity geri geldiğinde veritabanından
// değil bellekten döner ve sütunun neyi sakladığı hiç görünmez — yazının uyardığı nokta.
async Task<List<Blog>> WriteAndReadAsync(LabsDbContext context, Func<DateTime, Blog> factory)
{
    var written = instants.Select(factory).ToList();
    context.Blogs.AddRange(written);
    await context.SaveChangesAsync();

    var ids = written.Select(b => b.Id).ToList();
    context.ChangeTracker.Clear();

    return await context.Blogs
        .AsNoTracking()
        .Where(b => ids.Contains(b.Id))
        .OrderBy(b => b.Id)
        .ToListAsync();
}

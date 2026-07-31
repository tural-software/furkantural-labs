using System.Reflection;
using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Domain.Entities;
using FurkanTural_Labs_Persistence.Contexts;
using FurkanTural_Labs_Persistence.Registration;
using FurkanTural_Labs_Persistence.Sandbox;
using FurkanTural_Labs_Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

const int PageSize = 20;
const int DeepPage = 500;
const int DeepSkip = (DeepPage - 1) * PageSize;   // 9.980

await using var provider = await LabsHost.StartAsync(Assembly.GetExecutingAssembly());
var counter = provider.GetRequiredService<IQueryCounter>();

// Keyset'in devam noktası. Ölçümün dışında hazırlanıyor: gerçek bir API'de bu değer
// önceki sayfanın son satırından gelir, sorguyla aranmaz. Keyset'in bedava olmayan
// tarafı da bu — 500. sayfaya doğrudan atlayamazsınız.
var cursor = await provider.ScopedAsync(db => db.Blogs
    .AsNoTracking()
    .OrderByDescending(b => b.PublishedAt).ThenByDescending(b => b.Id)
    .Skip(DeepSkip - 1).Take(1)
    .Select(b => new { b.PublishedAt, b.Id })
    .FirstAsync());

var report = new LabReport(
    title: "Lab_17 — Skip/Take sayfalama: atlanan satırlar da okunur",
    claim: $"Sayfa {DeepPage} ile sayfa 1 istemciye aynı {PageSize} satırı gönderir. Ölçülebilir fark " +
           "sayfalamanın nerede yapıldığında ve liste kaydığında kullanıcının kaç farklı satır gördüğünde.",
    counter: counter,
    metric: "Satır");

// ── 1. Sayfalamayı bellekte yapmak ───────────────────────────────────────────
// En pahalı varyant: sıralama ve dilimleme uygulamada, tablo tamamen ağdan geçtikten sonra.
await report.MeasureAsync(
    "1) Bellekte sayfalama",
    Expectation.Exactly(LabsSeeder.BlogCount),
    () => provider.ScopedAsync(async db =>
    {
        var all = await db.Blogs.AsNoTracking()
            .OrderByDescending(b => b.PublishedAt)
            .ToListAsync();

        _ = all.Skip(DeepSkip).Take(PageSize).ToList();
        return counter.RowCount;
    }),
    note: "20 satır göstermek için 10.000 satır taşındı.");

// ── 2. Offset, ilk sayfa ─────────────────────────────────────────────────────
await report.MeasureAsync(
    "2) Offset, sayfa 1",
    Expectation.Exactly(PageSize),
    () => provider.ScopedAsync(async db =>
    {
        _ = await OffsetPageAsync(db, page: 1);
        return counter.RowCount;
    }),
    note: "Sunucu 20 satır üretip 20 satır gönderdi. Offset'in en iyi hâli.");

// ── 3. Offset, derin sayfa ───────────────────────────────────────────────────
// Yazının başlığındaki "size 20 verir" kısmı. "10.000 satır okur" kısmı ise sunucunun
// içinde kalır: atılan 9.980 satır ağdan geçmediği için bu sütuna yansımaz. 10.000
// satırlık bir tabloda süreye de yansımıyor — offset'in bedeli tablo büyüdükçe doğuyor.
await report.MeasureAsync(
    $"3) Offset, sayfa {DeepPage}",
    Expectation.Exactly(PageSize),
    () => provider.ScopedAsync(async db =>
    {
        _ = await OffsetPageAsync(db, page: DeepPage);
        return counter.RowCount;
    }),
    note: "İstemciye giden satır 2) ile aynı; atılan 9.980 satır sunucunun içinde kalır.");

// ── 4. Keyset ────────────────────────────────────────────────────────────────
// "Kaç satır atlayacağım" değil "en son nerede kalmıştım". Bir fazla satır çekmek,
// "devamı var mı" sorusunu ayrı bir COUNT sorgusu olmadan cevaplar.
await report.MeasureAsync(
    "4) Keyset, aynı derinlik",
    Expectation.Exactly(PageSize),
    () => provider.ScopedAsync(async db =>
    {
        var rows = await db.Blogs.AsNoTracking()
            .Where(b => b.PublishedAt < cursor.PublishedAt ||
                       (b.PublishedAt == cursor.PublishedAt && b.Id < cursor.Id))
            .OrderByDescending(b => b.PublishedAt).ThenByDescending(b => b.Id)
            .Take(PageSize + 1)
            .Select(b => b.Id)
            .ToListAsync();

        var hasMore = rows.Count > PageSize;
        if (hasMore) throw new InvalidOperationException("Sayfa 500 son sayfa olmalıydı.");

        return counter.RowCount;
    }),
    note: $"{PageSize + 1} satır istendi, {PageSize} geldi: devamı yok. " +
          "'Son sayfa mı' sorusu ayrı bir COUNT sorgusu olmadan böyle cevaplanır.");

// ── 5. Offset'in doğruluk kusuru ─────────────────────────────────────────────
// Sayfa 1 okunduktan sonra listenin başına bir kayıt giriyor. Offset sabit bir sıra
// numarasına güvendiği için sayfa 2, sayfa 1'in son satırını ikinci kez gösteriyor.
await report.MeasureAsync(
    "5) Offset — liste kaydığında",
    Expectation.Exactly(2 * PageSize - 1),
    () => provider.ScopedAsync(db => DataSandbox.RollbackAsync(db, async ctx =>
    {
        var first = await OffsetPageAsync(ctx, page: 1);
        await InsertNewestAsync(ctx);
        var second = await OffsetPageAsync(ctx, page: 2);

        return first.Concat(second).Distinct().Count();
    })),
    note: "40 satır gösterildi, 39'u farklı: bir kayıt iki kez göründü, bir kayıt hiç görünmedi.");

// ── 6. Aynı senaryo, keyset ile ──────────────────────────────────────────────
await report.MeasureAsync(
    "6) Keyset — liste kaydığında",
    Expectation.Exactly(2 * PageSize),
    () => provider.ScopedAsync(db => DataSandbox.RollbackAsync(db, async ctx =>
    {
        var first = await KeysetPageAsync(ctx, after: null);
        await InsertNewestAsync(ctx);
        var second = await KeysetPageAsync(ctx, after: first[^1]);

        return first.Select(r => r.Id).Concat(second.Select(r => r.Id)).Distinct().Count();
    })),
    note: "Devam noktası satırın kendisi olduğu için araya giren kayıt sırayı bozmuyor.");

return report.Print();

// Offset sayfası: yalnız Id'ler, sıralama tarihe göre azalan.
static async Task<List<int>> OffsetPageAsync(LabsDbContext context, int page)
    => await context.Blogs.AsNoTracking()
        .OrderByDescending(b => b.PublishedAt).ThenByDescending(b => b.Id)
        .Skip((page - 1) * PageSize)
        .Take(PageSize)
        .Select(b => b.Id)
        .ToListAsync();

// Keyset sayfası: devam noktası verilmişse ondan sonrasını getirir.
static async Task<List<Cursor>> KeysetPageAsync(LabsDbContext context, Cursor? after)
{
    IQueryable<Blog> query = context.Blogs.AsNoTracking();

    if (after is not null)
    {
        // Demeti (PublishedAt, Id) tek karşılaştırmayla yazmayı denemeyin: C#'ta değer
        // demetleri < operatörü tanımlamaz, EF de LINQ'tan satır-değeri karşılaştırması üretmez.
        query = query.Where(b => b.PublishedAt < after.PublishedAt ||
                                (b.PublishedAt == after.PublishedAt && b.Id < after.Id));
    }

    return await query
        .OrderByDescending(b => b.PublishedAt).ThenByDescending(b => b.Id)
        .Take(PageSize)
        .Select(b => new Cursor(b.PublishedAt, b.Id))
        .ToListAsync();
}

// Listenin en başına giren yeni kayıt. Transaction geri alındığı için tohum bozulmaz.
static async Task InsertNewestAsync(LabsDbContext context)
{
    context.Blogs.Add(new Blog
    {
        Title = "Araya giren yazı",
        Content = "Lab_17 tarafından eklendi; transaction geri alınır.",
        PublishedAt = LabsSeeder.PublishedAtOf(LabsSeeder.BlogCount + 1),
        PublishedAtOffset = new DateTimeOffset(LabsSeeder.PublishedAtOf(LabsSeeder.BlogCount + 1), TimeSpan.Zero),
        CreatedAt = LabsSeeder.Epoch
    });

    await context.SaveChangesAsync();
    context.ChangeTracker.Clear();
}

/// <summary>Keyset'in devam noktası: sıralama anahtarı + tie-breaker.</summary>
internal sealed record Cursor(DateTime PublishedAt, int Id);

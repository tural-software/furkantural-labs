using System.Reflection;
using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Domain.Entities;
using FurkanTural_Labs_Persistence.Contexts;
using FurkanTural_Labs_Persistence.Registration;
using FurkanTural_Labs_Persistence.Sandbox;
using FurkanTural_Labs_Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Ölçüm kümesi laboratuvarın kendisi tarafından kurulur. Tohumdaki bloglara 2–5 yorum ve
// 1–3 kategori düşer; o oranlarda çarpım ile toplam birbirine çok yakın çıkar ve fark
// okunmaz. Burada iki koleksiyon da bilerek kalabalık: çarpımın toplamdan ne kadar hızlı
// büyüdüğü ancak öyle görünür. Kurulum DataSandbox içinde yapılır ve geri alınır —
// tohum verisi diğer laboratuvarlar için olduğu gibi kalır.
const int BlogCount = 10;
const int CommentsPerBlog = 20;

await using var provider = await LabsHost.StartAsync(Assembly.GetExecutingAssembly());
var counter = provider.GetRequiredService<IQueryCounter>();

var report = new LabReport(
    title: "Lab_22 — İkinci Include: satırlar toplanmaz, çarpılır",
    claim: $"Aynı {BlogCount} bloğun yorumları ve kategorileri yedi ayrı yoldan çekiliyor. " +
           "Ölçülen şey, veritabanının okuyucuya kaç satır verdiği.",
    counter: counter,
    metric: "Satır");

await provider.ScopedAsync(db => DataSandbox.RollbackAsync(db, async ctx =>
{
    var categoryIds = await ctx.Categories.OrderBy(c => c.Id).Select(c => c.Id).ToListAsync();
    if (categoryIds.Count == 0)
        throw new InvalidOperationException("Tohumda kategori yok; ölçüm kümesi kurulamaz.");

    var blogIds = await BuildFixtureAsync(ctx, categoryIds);
    var categoriesPerBlog = categoryIds.Count;

    // ── Beklentiler ölçümden değil çarpım tablosundan gelir ──────────────────
    // Tek koleksiyon: ana kayıt çocuk sayısı kadar tekrar eder → blog × yorum.
    // İki koleksiyon: JOIN'ler birbirini keser → blog × yorum × kategori.
    // Bölünmüş sorgu: her koleksiyon kendi sorgusunu alır → blog + yorum + kategori.
    var commentRows = BlogCount * CommentsPerBlog;
    var categoryRows = BlogCount * categoriesPerBlog;
    var cartesianRows = BlogCount * CommentsPerBlog * categoriesPerBlog;
    var splitRows = BlogCount + commentRows + categoryRows;
    var splitSingleRows = BlogCount + commentRows;

    // ── 1. Tek koleksiyon: herkesin bildiği ve zararsız olan hâl ─────────────
    await report.MeasureAsync(
        "1) Include(Comments)",
        Expectation.Exactly(commentRows),
        async () =>
        {
            _ = await ctx.Blogs.AsNoTracking()
                .Where(b => blogIds.Contains(b.Id))
                .Include(b => b.Comments)
                .ToListAsync();

            return counter.RowCount;
        },
        note: $"{BlogCount} blog × {CommentsPerBlog} yorum. Ana kayıt yorum sayısı kadar tekrar eder, fazlası yok.");

    // ── 2. İkinci koleksiyon: tek satırlık değişiklik, çarpan bir maliyet ────
    // Aynı ekranda hem yorumlar hem kategoriler isteniyor. Include ikinci kez yazılıyor,
    // sorgu yine tek — ama iki LEFT JOIN birbirini kesiyor.
    await report.MeasureAsync(
        "2) + Include(Categories)",
        Expectation.Exactly(cartesianRows),
        async () =>
        {
            _ = await ctx.Blogs.AsNoTracking()
                .Where(b => blogIds.Contains(b.Id))
                .Include(b => b.Comments)
                .Include(b => b.BlogCategories)
                    .ThenInclude(bc => bc.Category)
                .ToListAsync();

            return counter.RowCount;
        },
        note: $"Satır sayısı {categoriesPerBlog} katına çıktı. Her yorum her kategoriyle eşleşti; " +
              "blog gövdesi bu satırların hepsinde yeniden gönderildi.");

    // ── 3. AsSplitQuery: çarpım toplama döner ───────────────────────────────
    await report.MeasureAsync(
        "3) AsSplitQuery",
        Expectation.Exactly(splitRows),
        async () =>
        {
            _ = await ctx.Blogs.AsNoTracking()
                .Where(b => blogIds.Contains(b.Id))
                .Include(b => b.Comments)
                .Include(b => b.BlogCategories)
                    .ThenInclude(bc => bc.Category)
                .AsSplitQuery()
                .ToListAsync();

            return counter.RowCount;
        },
        note: "Üç ayrı sorgu, her koleksiyon kendi satırlarını getiriyor. Tek satırlık ekleme, çarpımı toplamaya çevirdi.");

    // ── 4. Bölmenin bedeli ──────────────────────────────────────────────────
    // AsSplitQuery ücretsiz değil: ana tablo her sorguda yeniden okunur. Çarpacak
    // ikinci koleksiyon yokken bu, kazanç değil doğrudan zarardır — global olarak
    // UseQuerySplittingBehavior(SplitQuery) açanların ödediği bedel budur.
    await report.MeasureAsync(
        "4) Tek koleksiyon + AsSplitQuery",
        Expectation.Exactly(splitSingleRows),
        async () =>
        {
            _ = await ctx.Blogs.AsNoTracking()
                .Where(b => blogIds.Contains(b.Id))
                .Include(b => b.Comments)
                .AsSplitQuery()
                .ToListAsync();

            return counter.RowCount;
        },
        note: $"1. senaryonun aynısı, üstüne {BlogCount} satır. Bölünecek bir çarpım yokken bölmek yalnızca fatura ekler.");

    // ── 5. Çarpan koleksiyondur, ilişkinin kendisi değil ─────────────────────
    // Aynı veri ters yönden: her yorum kendi bloğuyla birlikte. Referans navigation
    // 1-1 bağlanır ve hiçbir şeyi çoğaltmaz.
    await report.MeasureAsync(
        "5) Include(Comment.Blog)",
        Expectation.Exactly(commentRows),
        async () =>
        {
            _ = await ctx.Comments.AsNoTracking()
                .Where(c => blogIds.Contains(c.BlogId))
                .Include(c => c.Blog)
                .ToListAsync();

            return counter.RowCount;
        },
        note: "Referans navigation tek satıra bağlanır. Include'un sayısı değil, koleksiyon olup olmadığı belirleyici.");

    // ── 6. Projeksiyon kolonu daraltır, satırı daraltmaz ────────────────────
    // Include yerine Select yazmak sık önerilen refleks. Taşınan kolonlar gerçekten
    // azalır; ama iki koleksiyon hâlâ aynı sorguda kesişir, satır sayısı değişmez.
    await report.MeasureAsync(
        "6) Select projeksiyonu",
        Expectation.Exactly(cartesianRows),
        async () =>
        {
            _ = await ctx.Blogs.AsNoTracking()
                .Where(b => blogIds.Contains(b.Id))
                .Select(b => new
                {
                    b.Title,
                    Comments = b.Comments.Select(c => c.Author).ToList(),
                    Categories = b.BlogCategories.Select(bc => bc.Category.Name).ToList()
                })
                .ToListAsync();

            return counter.RowCount;
        },
        note: "Kolonlar daraldı, satırlar 2. senaryodakiyle aynı kaldı. Projeksiyon kartezyen çarpımı çözmez.");

    // ── 7. Taşınmayan satırın maliyeti yok ──────────────────────────────────
    // Liste ekranlarının çoğu koleksiyonun kendisini değil adedini gösterir.
    await report.MeasureAsync(
        "7) Select ile yalnız adet",
        Expectation.Exactly(BlogCount),
        async () =>
        {
            _ = await ctx.Blogs.AsNoTracking()
                .Where(b => blogIds.Contains(b.Id))
                .Select(b => new
                {
                    b.Title,
                    CommentCount = b.Comments.Count(),
                    CategoryCount = b.BlogCategories.Count()
                })
                .ToListAsync();

            return counter.RowCount;
        },
        note: "Adet veritabanında hesaplandı; tek bir çocuk satır taşınmadı.");

    return 0;
}));

return report.Print();

// Ölçüm kümesini kurar: kategorilerin tamamına bağlı, her biri sabit sayıda yoruma sahip
// bloglar. Sayılar rastgele değil ki beklentiler aritmetikle yazılabilsin.
static async Task<List<int>> BuildFixtureAsync(LabsDbContext ctx, List<int> categoryIds)
{
    // Gövde bilerek uzun: kartezyen çarpımda tekrar eden şey satır sayısı değil, satırın
    // taşıdığı ana kayıttır. Content her tekrar satırında yeniden gönderilir ve tabloda
    // bunun karşılığı bellek sütununda görünür.
    var body = string.Join(' ', Enumerable.Repeat("Bu gövde ölçümün ağırlığını taşır.", 60));

    var blogs = new List<Blog>(BlogCount);
    for (var i = 1; i <= BlogCount; i++)
    {
        var published = LabsSeeder.PublishedAtOf(LabsSeeder.BlogCount + i);
        blogs.Add(new Blog
        {
            Title = $"Lab_22 ölçüm bloğu #{i:D2}",
            Content = body,
            PublishedAt = published,
            PublishedAtOffset = new DateTimeOffset(published, TimeSpan.Zero),
            CreatedAt = LabsSeeder.Epoch
        });
    }

    ctx.Blogs.AddRange(blogs);
    await ctx.SaveChangesAsync();

    var comments = new List<Comment>(BlogCount * CommentsPerBlog);
    var links = new List<BlogCategory>(BlogCount * categoryIds.Count);

    foreach (var blog in blogs)
    {
        for (var c = 1; c <= CommentsPerBlog; c++)
        {
            comments.Add(new Comment
            {
                BlogId = blog.Id,
                Author = $"okur{c:D2}",
                Body = $"{blog.Id} numaralı ölçüm bloğuna {c}. yorum.",
                CreatedAt = LabsSeeder.Epoch
            });
        }

        foreach (var categoryId in categoryIds)
            links.Add(new BlogCategory { BlogId = blog.Id, CategoryId = categoryId, CreatedAt = LabsSeeder.Epoch });
    }

    ctx.Comments.AddRange(comments);
    ctx.BlogCategories.AddRange(links);
    await ctx.SaveChangesAsync();

    // Kurulumdan kalan izleme kayıtları ölçüme karışmasın: sorgular veriyi
    // veritabanından okusun, tracker'dan değil.
    ctx.ChangeTracker.Clear();

    return blogs.Select(b => b.Id).ToList();
}

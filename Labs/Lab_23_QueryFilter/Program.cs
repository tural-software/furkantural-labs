using System.Reflection;
using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Domain.Entities;
using FurkanTural_Labs_Persistence.Contexts;
using FurkanTural_Labs_Persistence.Diagnostics;
using FurkanTural_Labs_Persistence.Interceptors;
using FurkanTural_Labs_Persistence.Registration;
using FurkanTural_Labs_Persistence.Sandbox;
using FurkanTural_Labs_Persistence.Seed;
using Lab_23_QueryFilter;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Ölçüm kümesi iki filtrenin kesişimini görünür kılacak şekilde kuruluyor: silinmiş/duran
// ve yayınlanmış/beklemede eksenleri çaprazlanıyor. Hücreler bilerek eşit DEĞİL — eşit
// olsalardı "yalnız SoftDelete'i kapat" ile "yalnız Published'ı kapat" aynı sayıyı verir
// ve hangi filtrenin neyi tuttuğu tabloda okunmazdı.
const int LivePublished = 3;      // filtrelerin ikisini de geçen tek hücre
const int LiveFuture = 2;         // duruyor ama yayın anı gelmemiş
const int DeletedPublished = 4;   // yayınlanmış, sonra silinmiş
const int DeletedFuture = 1;      // hem silinmiş hem beklemede

const int BlogTotal = LivePublished + LiveFuture + DeletedPublished + DeletedFuture;
const int VisibleBlogs = LivePublished;                      // iki filtre birden açıkken
const int LiveBlogs = LivePublished + LiveFuture;             // yalnız Published kapalıyken
const int PublishedBlogs = LivePublished + DeletedPublished;  // yalnız SoftDelete kapalıyken

// Yorumlar bilerek bloğun durumundan bağımsız: silinmiş bloğun yorumu silinmiş değil.
// Gerçek hayatta da öyle — soft delete kaskad etmez, bunu yapan kod yazılmadıysa.
const int CommentsPerBlog = 10;
const int DeletedCommentsPerBlog = 3;
const int LiveCommentsPerBlog = CommentsPerBlog - DeletedCommentsPerBlog;

const string TitlePrefix = "Lab_23 ölçüm bloğu";

var assembly = Assembly.GetExecutingAssembly();

// Sayaç dışarıda yaratılıyor: veritabanını kuran host ile ölçümün yapıldığı filtreli
// context iki ayrı provider'dır, ikisi de aynı sayaca yazsın.
var counter = new QueryCounter();
await using var provider = await LabsHost.StartAsync(assembly, sharedCounter: counter);

// Filtreli context DI'dan gelmiyor çünkü AddLabsPersistence filtresiz olanı kaydeder.
// Aynı bağlantı dizesi, aynı interceptor; tek fark modeldeki iki filtre.
var connectionString = LabsHost.BuildConfiguration(assembly)
    .GetConnectionString(PersistenceServiceRegistration.ConnectionName)!;

var options = new DbContextOptionsBuilder<LabsDbContext>()
    .UseSqlServer(connectionString)
    .AddInterceptors(new QueryCountInterceptor(counter))
    .Options;

await using var db = new SoftDeleteContext(options);

// Isınma: bu context tipi kendi modelini ilk sorguda kurar. Ölçüme yazılırsa ilk senaryo
// haksız yere yavaş görünür. Sayaç her senaryodan önce sıfırlandığı için satırları kirletmez.
_ = await db.Blogs.AsNoTracking().Take(1).ToListAsync();

var report = new LabReport(
    title: "Lab_23 — Global query filter: yazmadığınız WHERE",
    claim: $"Aynı {BlogTotal} blog ve {BlogTotal * CommentsPerBlog} yorum sekiz ayrı yoldan sorgulanıyor. " +
           "Ölçülen şey, veritabanının okuyucuya kaç satır verdiği.",
    counter: counter,
    metric: "Satır");

await DataSandbox.RollbackAsync(db, async ctx =>
{
    var blogIds = await BuildFixtureAsync(ctx);

    // ── 1. Filtre açık: kodda görünmeyen daralma ─────────────────────────────
    // Sorguda tek bir Where yok, yine de tablodaki satırların çoğu gelmiyor.
    await report.MeasureAsync(
        "1) Blogs (iki filtre açık)",
        Expectation.Exactly(VisibleBlogs),
        async () =>
        {
            _ = await ctx.Blogs.AsNoTracking()
                .Where(b => blogIds.Contains(b.Id))
                .ToListAsync();

            return counter.RowCount;
        },
        note: $"{BlogTotal} satırın {VisibleBlogs} tanesi geldi. Sorguda tek bir koşul yazılmadı; " +
              "daraltan şey modeldeki iki filtre.");

    // ── 2. Tabloda gerçekte ne var ───────────────────────────────────────────
    await report.MeasureAsync(
        "2) IgnoreQueryFilters()",
        Expectation.Exactly(BlogTotal),
        async () =>
        {
            _ = await ctx.Blogs.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(b => blogIds.Contains(b.Id))
                .ToListAsync();

            return counter.RowCount;
        },
        note: "Silinen satır gitmedi, yalnızca görünmüyordu. Soft delete diskte yer kaplamaya devam eder.");

    // ── 3. Filtre Include'a da iner ──────────────────────────────────────────
    // Yorumların kendi filtresi var ve bu sorguda o da uygulanıyor: kök daralırken
    // koleksiyon da daralıyor. İki daralma çarpılarak görünüyor.
    await report.MeasureAsync(
        "3) + Include(Comments)",
        Expectation.Exactly(VisibleBlogs * LiveCommentsPerBlog),
        async () =>
        {
            _ = await ctx.Blogs.AsNoTracking()
                .Where(b => blogIds.Contains(b.Id))
                .Include(b => b.Comments)
                .ToListAsync();

            return counter.RowCount;
        },
        note: $"{VisibleBlogs} blog × {LiveCommentsPerBlog} yorum. Filtre yalnız köke değil, " +
              "Include edilen koleksiyona da uygulandı.");

    // ── 4. Kapatma anahtarı tek ve topluca ───────────────────────────────────
    // "Silinmiş bloğu da görmek istiyorum" diye yazılan IgnoreQueryFilters(), silinmiş
    // yorumları da geri getirir. Yönetim ekranındaki listenin neden şiştiği burada.
    await report.MeasureAsync(
        "4) IgnoreQueryFilters + Include",
        Expectation.Exactly(BlogTotal * CommentsPerBlog),
        async () =>
        {
            _ = await ctx.Blogs.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(b => blogIds.Contains(b.Id))
                .Include(b => b.Comments)
                .ToListAsync();

            return counter.RowCount;
        },
        note: "Tek çağrı sorgudaki bütün filtreleri kapattı; istenen blog filtresiydi, " +
              "kapanan yorum filtresi de oldu.");

    // ── 5. Adlandırılmış filtre: yalnız birini kapat (EF Core 10) ────────────
    await report.MeasureAsync(
        "5) Ignore(SoftDelete)",
        Expectation.Exactly(PublishedBlogs),
        async () =>
        {
            _ = await ctx.Blogs.AsNoTracking()
                .IgnoreQueryFilters([SoftDeleteContext.SoftDelete])
                .Where(b => blogIds.Contains(b.Id))
                .ToListAsync();

            return counter.RowCount;
        },
        note: $"Silinmişler geri geldi, yayın anı gelmemiş {LiveFuture + DeletedFuture} blog hâlâ dışarıda.");

    // ── 6. Aynı sorgu, öteki filtre kapalı ───────────────────────────────────
    await report.MeasureAsync(
        "6) Ignore(Published)",
        Expectation.Exactly(LiveBlogs),
        async () =>
        {
            _ = await ctx.Blogs.AsNoTracking()
                .IgnoreQueryFilters([SoftDeleteContext.Published])
                .Where(b => blogIds.Contains(b.Id))
                .ToListAsync();

            return counter.RowCount;
        },
        note: "Beklemedekiler geri geldi, silinmişler dışarıda kaldı. 5 ve 6 aynı tabloyu " +
              "iki farklı doğruyla okuyor.");

    // ── 7. Filtre kalıtsal değil ─────────────────────────────────────────────
    // Blog silindi, yorumları silinmedi. Yorum tablosuna doğrudan sorulduğunda ortada
    // olmayan blogların yorumları geliyor ve hiçbir filtre bunu engellemiyor.
    await report.MeasureAsync(
        "7) Comments (doğrudan)",
        Expectation.Exactly(BlogTotal * LiveCommentsPerBlog),
        async () =>
        {
            _ = await ctx.Comments.AsNoTracking()
                .Where(c => blogIds.Contains(c.BlogId))
                .ToListAsync();

            return counter.RowCount;
        },
        note: $"{BlogTotal} bloğun tamamının yorumları geldi; {BlogTotal - VisibleBlogs} tanesi " +
              "listede görünmeyen bloglara ait. Soft delete kaskad etmez.");

    // ── 8. Ebeveyne dokunan koşul filtreyi de getirir ────────────────────────
    // Aynı yorumlar, tek fark sorgunun bloğa uzanması. Blog'un filtresi devreye girdiği
    // an satır sayısı düşüyor: aynı veri, sorgunun şekline göre iki farklı gerçek.
    await report.MeasureAsync(
        "8) Comments + blog koşulu",
        Expectation.Exactly(VisibleBlogs * LiveCommentsPerBlog),
        async () =>
        {
            _ = await ctx.Comments.AsNoTracking()
                .Where(c => blogIds.Contains(c.BlogId) && c.Blog.Title.StartsWith(TitlePrefix))
                .ToListAsync();

            return counter.RowCount;
        },
        note: "Koşul blogların tamamı için doğru; buna rağmen 7. senaryonun satırları düştü. " +
              "Bloğa uzanan sorgu, bloğun filtresini de yanında getirdi.");

    return 0;
});

return report.Print();

// Ölçüm kümesini kurar: iki eksen çaprazlanmış bloglar, her birine sabit sayıda yorum.
// Sayılar rastgele değil ki beklentiler aritmetikle yazılabilsin.
static async Task<List<int>> BuildFixtureAsync(LabsDbContext ctx)
{
    // Yayınlanmış için tohumun başlangıcı, beklemede için bir yüzyıl sonrası. Filtre
    // GETUTCDATE() ile çevrildiği için sınırın ölçüm anına yakın olması sayıyı saate
    // bağlar; yüz yıllık pay bunu imkânsız kılar.
    var publishedAt = LabsSeeder.Epoch;
    var pendingAt = LabsSeeder.Epoch.AddYears(100);

    var blogs = new List<Blog>(BlogTotal);
    var index = 0;

    void Add(int count, bool deleted, bool pending)
    {
        for (var i = 0; i < count; i++)
        {
            var moment = pending ? pendingAt : publishedAt;
            blogs.Add(new Blog
            {
                Title = $"{TitlePrefix} #{++index:D2}",
                Content = "Filtrenin hangi satırı tuttuğunu ölçmek için üretilmiştir.",
                PublishedAt = moment,
                PublishedAtOffset = new DateTimeOffset(moment, TimeSpan.Zero),
                CreatedAt = LabsSeeder.Epoch,
                IsDeleted = deleted,
                DeletedAt = deleted ? LabsSeeder.Epoch.AddDays(1) : null
            });
        }
    }

    Add(LivePublished, deleted: false, pending: false);
    Add(LiveFuture, deleted: false, pending: true);
    Add(DeletedPublished, deleted: true, pending: false);
    Add(DeletedFuture, deleted: true, pending: true);

    ctx.Blogs.AddRange(blogs);
    await ctx.SaveChangesAsync();

    var comments = new List<Comment>(BlogTotal * CommentsPerBlog);
    foreach (var blog in blogs)
    {
        for (var c = 1; c <= CommentsPerBlog; c++)
        {
            // Yorumun silinmişliği bloğunkinden bağımsız: son üçü her blokta silinmiş.
            var deleted = c > LiveCommentsPerBlog;
            comments.Add(new Comment
            {
                BlogId = blog.Id,
                Author = $"okur{c:D2}",
                Body = $"{blog.Id} numaralı ölçüm bloğuna {c}. yorum.",
                CreatedAt = LabsSeeder.Epoch,
                IsDeleted = deleted,
                DeletedAt = deleted ? LabsSeeder.Epoch.AddDays(1) : null
            });
        }
    }

    ctx.Comments.AddRange(comments);
    await ctx.SaveChangesAsync();

    // Kurulumdan kalan izleme kayıtları ölçüme karışmasın.
    ctx.ChangeTracker.Clear();

    return blogs.Select(b => b.Id).ToList();
}

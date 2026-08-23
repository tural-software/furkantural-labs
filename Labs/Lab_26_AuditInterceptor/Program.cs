using System.Reflection;
using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Domain.Entities;
using FurkanTural_Labs_Persistence.Contexts;
using FurkanTural_Labs_Persistence.Diagnostics;
using FurkanTural_Labs_Persistence.Interceptors;
using FurkanTural_Labs_Persistence.Registration;
using FurkanTural_Labs_Persistence.Sandbox;
using FurkanTural_Labs_Persistence.Seed;
using Lab_26_AuditInterceptor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Her senaryoda aynı 10 satır güncelleniyor. Ölçülen şey, güncellemeden sonra veritabanında
// UpdatedAt damgası taşıyan satır sayısı. Değişen tek şey yazmanın hangi yoldan yapıldığı.
//
// Beklentiler ölçümden değil interceptor'ın çalışma yerinden geliyor: damga
// ChangeTracker girdileri üzerinden basılır, dolayısıyla
//   · tracker'dan geçen her yazma damgalanır → 10
//   · tracker'a uğramayan her yazma damgasız kalır → 0
//   · hiçbir alanı değişmemiş girdi Modified olmadığı için damgalanmaz → 0
//   · interceptor yokken damga koda kalır; unutulursa 0, yazılırsa 10

const int SatirSayisi = 10;
const string BaslikOneki = "Lab_26 ölçüm bloğu";

var assembly = Assembly.GetExecutingAssembly();

// Damga sabit bir değer: ölçüm saate bağlı olmasın. Tohumun başlangıcından bir yıl sonrası,
// yani fikstürün CreatedAt'inden ayırt edilebilir ama gerçek bir tarih.
var damga = LabsSeeder.Epoch.AddYears(1);

var counter = new QueryCounter();
await using var provider = await LabsHost.StartAsync(assembly, sharedCounter: counter);

var connectionString = LabsHost.BuildConfiguration(assembly)
    .GetConnectionString(PersistenceServiceRegistration.ConnectionName)!;

// Interceptor seçenek düzeyinde takılır, model düzeyinde değil: aynı context tipinin iki
// seçenek kümesiyle iki örneği yetiyor, türetilmiş bir context gerekmiyor.
var damgasiz = SecenekKur(interceptor: null);
var damgali = SecenekKur(new AuditInterceptor(damga));

var report = new LabReport(
    title: "Lab_26 — Audit damgasını interceptor basıyor, peki hangi yazma ona uğramıyor",
    claim: $"Her senaryoda aynı {SatirSayisi} satır güncelleniyor. Ölçülen şey, güncellemeden " +
           "sonra veritabanında UpdatedAt damgası taşıyan satır sayısı.",
    metric: "Damgalı");

// ── 1. Interceptor yok: damga koda kalır ve unutulur ─────────────────────────
await report.MeasureAsync(
    "1) Interceptor yok, damga unutulmuş",
    Expectation.Exactly(0),
    () => OlcAsync(damgasiz, async (ctx, idler) =>
    {
        var bloglar = await ctx.Blogs.Where(b => idler.Contains(b.Id)).ToListAsync();
        foreach (var blog in bloglar)
            blog.Title += " (guncellendi)";

        await ctx.SaveChangesAsync();
    }),
    note: "Güncelleme başarılı, satırlar değişti, tek bir damga yok. Hatanın görünmesi için " +
          "kimsenin \"bu kayıt en son ne zaman değişti\" diye sorması gerekiyor.");

// ── 2. Interceptor yok, damga elle yazılmış ──────────────────────────────────
await report.MeasureAsync(
    "2) Interceptor yok, damga elle",
    Expectation.Exactly(SatirSayisi),
    () => OlcAsync(damgasiz, async (ctx, idler) =>
    {
        var bloglar = await ctx.Blogs.Where(b => idler.Contains(b.Id)).ToListAsync();
        foreach (var blog in bloglar)
        {
            blog.Title += " (guncellendi)";
            blog.UpdatedAt = damga;
        }

        await ctx.SaveChangesAsync();
    }),
    note: "Aynı iş, iki satır fazlasıyla. Sorun bu yolun yanlış olması değil, her serviste " +
          "tekrarlanması ve bir kez unutulduğunda 1. senaryoya dönmesi.");

// ── 3. Interceptor açık: çağıran hiçbir şey yapmıyor ─────────────────────────
await report.MeasureAsync(
    "3) Interceptor açık, izlenen güncelleme",
    Expectation.Exactly(SatirSayisi),
    () => OlcAsync(damgali, async (ctx, idler) =>
    {
        var bloglar = await ctx.Blogs.Where(b => idler.Contains(b.Id)).ToListAsync();
        foreach (var blog in bloglar)
            blog.Title += " (guncellendi)";

        await ctx.SaveChangesAsync();
    }),
    note: "1. senaryonun birebir aynı kodu. Damga artık serviste değil kaydetme yolunda; " +
          "unutulacak bir yer kalmıyor.");

// ── 4. Bağlantısız güncelleme de tracker'dan geçer ───────────────────────────
// Nesne başka bir context'te okundu, dışarıda değiştirildi, geri bağlandı — web
// uygulamalarının en yaygın yazma şekli.
await report.MeasureAsync(
    "4) Interceptor açık, bağlantısız güncelleme",
    Expectation.Exactly(SatirSayisi),
    () => OlcAsync(damgali, async (ctx, idler) =>
    {
        var kopuk = await ctx.Blogs.AsNoTracking()
            .Where(b => idler.Contains(b.Id))
            .ToListAsync();

        foreach (var blog in kopuk)
            blog.Title += " (guncellendi)";

        ctx.UpdateRange(kopuk);
        await ctx.SaveChangesAsync();
    }),
    note: "Nesneler izlenmeden okundu ama Update ile geri bağlandı; girdi Modified olduğu " +
          "an damga da basıldı. Belirleyici olan nesnenin nereden geldiği değil, hangi durumda kaydedildiği.");

// ── 5. Değişmemiş girdi damgalanmaz ──────────────────────────────────────────
// Damga niyeti değil durumu izler: hiçbir alanı değişmemiş girdi Modified olmaz.
await report.MeasureAsync(
    "5) Interceptor açık, hiçbir alan değişmedi",
    Expectation.Exactly(0),
    () => OlcAsync(damgali, async (ctx, idler) =>
    {
        _ = await ctx.Blogs.Where(b => idler.Contains(b.Id)).ToListAsync();
        await ctx.SaveChangesAsync();
    }),
    note: "SaveChanges çağrıldı ama ortada değişiklik yok. \"Kaydet\" düğmesine basılması " +
          "kaydın değiştiği anlamına gelmiyor; damga da öyle düşünüyor.");

// ── 6. ExecuteUpdate: tracker'a hiç uğramaz ──────────────────────────────────
// Toplu güncellemenin doğru aracı budur ve nesne yüklemediği için hızlıdır — ama
// interceptor'ın dinlediği yerden geçmez. Damga sessizce düşer.
await report.MeasureAsync(
    "6) Interceptor açık, ExecuteUpdateAsync",
    Expectation.Exactly(0),
    () => OlcAsync(damgali, async (ctx, idler) =>
    {
        await ctx.Blogs
            .Where(b => idler.Contains(b.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Title, b => b.Title + " (guncellendi)"));
    }),
    note: "Tek sorgu, nesne yok, izleme yok — ve damga yok. Interceptor kurulu olduğu için " +
          "kimse damgayı aramıyor; yolun tamamı sessiz.");

// ── 7. Aynı çağrı, damga elle eklenmiş ───────────────────────────────────────
await report.MeasureAsync(
    "7) ExecuteUpdate + SetProperty(UpdatedAt)",
    Expectation.Exactly(SatirSayisi),
    () => OlcAsync(damgali, async (ctx, idler) =>
    {
        await ctx.Blogs
            .Where(b => idler.Contains(b.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Title, b => b.Title + " (guncellendi)")
                .SetProperty(b => b.UpdatedAt, _ => damga));
    }),
    note: "6. senaryonun aynısı, tek satır fazlasıyla. Toplu yolda damga yine koda dönüyor — " +
          "yani 2. senaryodaki unutma riski bu yolda hâlâ duruyor.");

// ── 8. Ham SQL: kural ExecuteUpdate'e özgü değil ─────────────────────────────
await report.MeasureAsync(
    "8) Interceptor açık, ham SQL",
    Expectation.Exactly(0),
    () => OlcAsync(damgali, async (ctx, idler) =>
    {
        // Id'ler bu laboratuvarın kendi ürettiği int değerler; birleştirme güvenli.
        var sql = "UPDATE Blogs SET Title = Title + N' (guncellendi)' WHERE Id IN ("
                  + string.Join(",", idler) + ")";

        await ctx.Database.ExecuteSqlRawAsync(sql);
    }),
    note: "Kayıp ExecuteUpdate'e özgü değil: tracker'a uğramayan her yazma damgasız kalır. " +
          "Dapper ile açılan bir yazma da bu satırdadır.");

return report.Print();

// Bir senaryoyu kendi context'inde ve kendi transaction'ında çalıştırır; sonunda geri alır.
// Ölçüm tracker'dan değil veritabanından okunur: damganın gerçekten yazılıp yazılmadığı sorulur.
async Task<int> OlcAsync(DbContextOptions<LabsDbContext> secenekler, Func<LabsDbContext, List<int>, Task> yaz)
{
    await using var db = new LabsDbContext(secenekler);

    return await DataSandbox.RollbackAsync(db, async ctx =>
    {
        var idler = await KurAsync(ctx);
        await yaz(ctx, idler);

        ctx.ChangeTracker.Clear();

        return await ctx.Blogs.AsNoTracking()
            .Where(b => idler.Contains(b.Id) && b.UpdatedAt != null)
            .CountAsync();
    });
}

// Ölçüm kümesi: UpdatedAt'i boş 10 satır. Tohumdaki bloglar kullanılmadı çünkü onların
// audit alanları başka laboratuvarların da baktığı ortak veridir.
async Task<List<int>> KurAsync(LabsDbContext ctx)
{
    var bloglar = new List<Blog>(SatirSayisi);
    for (var i = 1; i <= SatirSayisi; i++)
    {
        bloglar.Add(new Blog
        {
            Title = $"{BaslikOneki} #{i:D2}",
            Content = "Audit damgasının hangi yazma yolunda basıldığını ölçmek için üretilmiştir.",
            PublishedAt = LabsSeeder.Epoch,
            PublishedAtOffset = new DateTimeOffset(LabsSeeder.Epoch, TimeSpan.Zero),
            CreatedAt = LabsSeeder.Epoch
        });
    }

    ctx.Blogs.AddRange(bloglar);
    await ctx.SaveChangesAsync();
    ctx.ChangeTracker.Clear();

    return [.. bloglar.Select(b => b.Id)];
}

DbContextOptions<LabsDbContext> SecenekKur(AuditInterceptor? interceptor)
{
    var kurucu = new DbContextOptionsBuilder<LabsDbContext>()
        .UseSqlServer(connectionString)
        .AddInterceptors(new QueryCountInterceptor(counter));

    if (interceptor is not null)
        kurucu.AddInterceptors(interceptor);

    return kurucu.Options;
}

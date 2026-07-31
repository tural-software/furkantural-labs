using FurkanTural_Labs_Domain.Entities;
using FurkanTural_Labs_Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FurkanTural_Labs_Persistence.Seed;

/// <summary>
/// Tüm laboratuvarların paylaştığı sabit veri kümesi.
/// <para>
/// <b>Determinizm şart.</b> Laboratuvarlar "101 sorgu" gibi kesin sayılar iddia eder ve
/// yazıdaki rakamlar bu sayılardan gelir. Bu yüzden rastgelelik sabit tohumludur ve
/// satır sayıları aşağıdaki sabitlerle kilitlidir; değiştirirsen ilgili laboratuvarların
/// beklentileri de güncellenmeli (aksi halde kanıt kapısı zaten kırmızı yanar).
/// </para>
/// </summary>
public static class LabsSeeder
{
    /// <summary>Toplam blog sayısı. Lab_17 sayfa 500'ü (Skip 9.980) gösterebilsin diye 10.000.</summary>
    public const int BlogCount = 10_000;

    /// <summary>İlk kaç bloğa yorum eklenecek. N+1 için 100 blog yeterli; 1.000 rahat pay bırakır.</summary>
    public const int BlogsWithComments = 1_000;

    private static readonly (string Name, string Color)[] Categories =
    [
        (".NET", "#D6F0FF"), (".NET CORE", "#89CFF0"), ("ASP.NET", "#A7C7E7"),
        ("ASP.NET MVC", "#87CEEB"), ("ASP.NET WEB API", "#40E0D0"), ("C#", "#228B22"),
        ("JS", "#F7DF1E"), ("HTML", "#E34F26"), ("REACT", "#61DAFB"), ("YAZILIM", "#6C757D")
    ];

    /// <summary>Veritabanını oluşturur ve boşsa doldurur. Doluysa hiçbir şey yapmaz.</summary>
    /// <returns>Veri bu çağrıda yazıldıysa <c>true</c>.</returns>
    public static async Task<bool> EnsureAsync(LabsDbContext context, CancellationToken ct = default)
    {
        await context.Database.EnsureCreatedAsync(ct);

        if (await context.Blogs.AnyAsync(ct)) return false;

        var previousAutoDetect = context.ChangeTracker.AutoDetectChangesEnabled;
        context.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            var rng = new Random(20260731);            // sabit tohum → her makinede aynı veri
            var epoch = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var categories = Categories
                .Select(c => new Category { Name = c.Name, Color = c.Color, CreatedAt = epoch })
                .ToList();

            context.Categories.AddRange(categories);
            await context.SaveChangesAsync(ct);
            context.ChangeTracker.Clear();

            var blogs = new List<Blog>(BlogCount);
            for (var i = 1; i <= BlogCount; i++)
            {
                var published = epoch.AddHours(i);
                blogs.Add(new Blog
                {
                    Title = $"Laboratuvar yazısı #{i:D5}",
                    Content = $"Bu, {i} numaralı örnek yazının gövdesidir. Ölçüm için üretilmiştir.",
                    ViewCount = rng.Next(0, 5_000),
                    PublishedAt = published,
                    PublishedAtOffset = new DateTimeOffset(published, TimeSpan.Zero),
                    CreatedAt = epoch
                });
            }

            await SaveInChunksAsync(context, blogs, ct);

            var comments = new List<Comment>();
            for (var i = 0; i < BlogsWithComments; i++)
            {
                var blogId = blogs[i].Id;
                var howMany = 2 + rng.Next(0, 4);      // 2–5 yorum
                for (var c = 1; c <= howMany; c++)
                {
                    comments.Add(new Comment
                    {
                        BlogId = blogId,
                        Author = $"okur{rng.Next(1, 400):D3}",
                        Body = $"{blogId} numaralı yazıya {c}. yorum.",
                        CreatedAt = epoch
                    });
                }
            }

            await SaveInChunksAsync(context, comments, ct);

            var links = new List<BlogCategory>();
            foreach (var blog in blogs)
            {
                var howMany = 1 + rng.Next(0, 3);      // 1–3 kategori
                foreach (var categoryId in Enumerable.Range(0, categories.Count)
                                                     .OrderBy(_ => rng.Next())
                                                     .Take(howMany)
                                                     .Select(idx => categories[idx].Id))
                {
                    links.Add(new BlogCategory { BlogId = blog.Id, CategoryId = categoryId, CreatedAt = epoch });
                }
            }

            await SaveInChunksAsync(context, links, ct);
            return true;
        }
        finally
        {
            context.ChangeTracker.AutoDetectChangesEnabled = previousAutoDetect;
        }
    }

    /// <summary>
    /// Parça parça kaydeder ve her partiden sonra change tracker'ı boşaltır.
    /// Tek <c>SaveChanges</c> ile 30 bin satır eklemek tracker'ı şişirir ve
    /// <c>DetectChanges</c> maliyetini karesel hale getirir.
    /// </summary>
    private static async Task SaveInChunksAsync<TEntity>(
        LabsDbContext context, List<TEntity> items, CancellationToken ct, int chunkSize = 2_000)
        where TEntity : class
    {
        foreach (var chunk in items.Chunk(chunkSize))
        {
            context.Set<TEntity>().AddRange(chunk);
            await context.SaveChangesAsync(ct);
            context.ChangeTracker.Clear();
        }
    }
}

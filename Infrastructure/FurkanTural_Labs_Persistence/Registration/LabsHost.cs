using System.Reflection;
using System.Text;
using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Persistence.Contexts;
using FurkanTural_Labs_Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FurkanTural_Labs_Persistence.Registration;

/// <summary>
/// Konsol laboratuvarlarının tek satırlık açılışı: konfigürasyonu kurar, DI'ı hazırlar,
/// veritabanının var ve dolu olduğundan emin olur. Her laboratuvarın <c>Program.cs</c>'i
/// böylece kurulum değil <b>konu</b> gösterir.
/// </summary>
public static class LabsHost
{
    /// <summary>Ortam değişkeni öneki: <c>FTLABS_ConnectionStrings__LabsConnection</c>.</summary>
    public const string EnvironmentPrefix = "FTLABS_";

    /// <summary>
    /// Sıralama önemli: sonraki kaynak öncekini ezer. Sır (user-secrets) dosyayı,
    /// ortam değişkeni ise sırrı ezer — CI'da dosyaya dokunmadan override edilebilsin.
    /// </summary>
    public static IConfiguration BuildConfiguration(Assembly labAssembly) =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddUserSecrets(labAssembly, optional: true)
            .AddEnvironmentVariables(EnvironmentPrefix)
            .Build();

    /// <param name="labAssembly">Çağıran laboratuvarın assembly'si; user-secrets kimliği buradan okunur.</param>
    /// <param name="useLazyLoadingProxies">Bkz. <see cref="PersistenceServiceRegistration.AddLabsPersistence"/>.</param>
    /// <param name="sharedCounter">Bkz. <see cref="PersistenceServiceRegistration.AddLabsPersistence"/>.</param>
    /// <param name="ct">İptal jetonu.</param>
    public static async Task<ServiceProvider> StartAsync(
        Assembly labAssembly,
        bool useLazyLoadingProxies = false,
        IQueryCounter? sharedCounter = null,
        CancellationToken ct = default)
    {
        // Türkçe çıktı Windows konsolunda bozulmasın. Yönlendirilmiş çıktıda desteklenmeyebilir.
        try { Console.OutputEncoding = Encoding.UTF8; } catch (IOException) { }

        var configuration = BuildConfiguration(labAssembly);

        var services = new ServiceCollection();
        services.AddLabsPersistence(configuration, useLazyLoadingProxies, sharedCounter);
        var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<LabsDbContext>();
            if (await LabsSeeder.EnsureAsync(context, ct))
            {
                Console.WriteLine($"Veri kümesi oluşturuldu: {LabsSeeder.BlogCount:N0} blog, " +
                                  $"ilk {LabsSeeder.BlogsWithComments:N0} bloğa yorum, 10 kategori.");
            }

            await WarmUpAsync(context, ct);
        }

        return provider;
    }

    /// <summary>
    /// Ölçümden önce ilk sorgunun bedelini öder.
    /// <para>
    /// EF ilk sorguda modeli kurar, sağlayıcıyı ve sorgu derleyicisini ayağa kaldırır,
    /// havuzdan ilk bağlantıyı açar. Bu tek seferlik maliyet ilk senaryoya yazılırsa
    /// tablo yanıltır: laboratuvarların birinde derin sayfa, sığ sayfadan hızlı göründü.
    /// Isınma sorgusu ölçümün dışındadır ve sonuçları kullanılmaz.
    /// </para>
    /// </summary>
    private static async Task WarmUpAsync(LabsDbContext context, CancellationToken ct)
    {
        _ = await context.Blogs.AsNoTracking().OrderBy(b => b.Id).Take(1).ToListAsync(ct);
        _ = await context.Comments.AsNoTracking().OrderBy(c => c.Id).Take(1).ToListAsync(ct);
    }
}

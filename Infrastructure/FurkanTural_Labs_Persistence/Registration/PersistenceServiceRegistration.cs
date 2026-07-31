using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Persistence.Contexts;
using FurkanTural_Labs_Persistence.Diagnostics;
using FurkanTural_Labs_Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FurkanTural_Labs_Persistence.Registration;

public static class PersistenceServiceRegistration
{
    /// <summary>Bağlantı dizesinin okunduğu anahtar.</summary>
    public const string ConnectionName = "LabsConnection";

    /// <param name="services">Hedef servis koleksiyonu.</param>
    /// <param name="configuration">Bağlantı dizesinin okunacağı konfigürasyon.</param>
    /// <param name="useLazyLoadingProxies">
    /// Yalnız Lab_04 için <c>true</c>. Proxy'ler açıkken navigation'a dokunmak sessizce
    /// sorgu doğurur; laboratuvarların geri kalanı bunu istemez çünkü sayımı kirletir.
    /// </param>
    /// <param name="sharedCounter">
    /// Aynı laboratuvar birden çok provider kuruyorsa (örn. proxy'li ve proxy'siz iki model)
    /// sayaç dışarıdan verilir; yoksa her provider kendi sayacını yaratır ve tek tabloda
    /// karşılaştırılamayan iki ayrı ölçüm çıkar.
    /// </param>
    public static IServiceCollection AddLabsPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        bool useLazyLoadingProxies = false,
        IQueryCounter? sharedCounter = null)
    {
        // IsNullOrWhiteSpace: appsettings.json anahtarı boş placeholder olarak gelir;
        // null kontrolü tek başına bunu geçirir ve hata anlaşılmaz bir bağlantı hatasına döner.
        var connectionString = configuration.GetConnectionString(ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionName} bulunamadı. Bir kez şunu çalıştır:\n" +
                $"  dotnet user-secrets set \"ConnectionStrings:{ConnectionName}\" \"<baglanti-dizesi>\"\n" +
                $"ya da FTLABS_ConnectionStrings__{ConnectionName} ortam değişkenini tanımla.\n" +
                "Bağlantı dizesi appsettings.json'a YAZILMAZ — bu repo public.");

        if (sharedCounter is null)
            services.AddSingleton<IQueryCounter, QueryCounter>();
        else
            services.AddSingleton(sharedCounter);

        services.AddSingleton<QueryCountInterceptor>();

        services.AddDbContext<LabsDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString)
                   .AddInterceptors(sp.GetRequiredService<QueryCountInterceptor>());

            if (useLazyLoadingProxies)
                options.UseLazyLoadingProxies();
        });

        return services;
    }
}

using FurkanTural_Labs_Persistence.Contexts;
using Microsoft.Extensions.DependencyInjection;

namespace FurkanTural_Labs_Persistence.Registration;

/// <summary>
/// Senaryoların context alma biçimi.
/// <para>
/// Her senaryo <b>kendi scope'unda</b> çalışır. Paylaşılan tek bir context, önceki
/// senaryonun önbelleğe aldığı entity'ler yüzünden sonrakinin sorgu sayısını düşürür;
/// ölçüm o anda kıyaslanamaz hale gelir.
/// </para>
/// </summary>
public static class LabsScopeExtensions
{
    /// <summary>Taze bir scope açar, işi çalıştırır ve scope'u kapatır.</summary>
    /// <typeparam name="T">İşin döndürdüğü tip.</typeparam>
    /// <param name="provider">Laboratuvarın servis sağlayıcısı.</param>
    /// <param name="work">Context ile yapılacak iş.</param>
    public static async Task<T> ScopedAsync<T>(this IServiceProvider provider, Func<LabsDbContext, Task<T>> work)
    {
        await using var scope = provider.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<LabsDbContext>());
    }

    /// <summary>Değer döndürmeyen işler için <see cref="ScopedAsync{T}"/> kısayolu.</summary>
    /// <param name="provider">Laboratuvarın servis sağlayıcısı.</param>
    /// <param name="work">Context ile yapılacak iş.</param>
    public static Task ScopedAsync(this IServiceProvider provider, Func<LabsDbContext, Task> work)
        => provider.ScopedAsync<object?>(async db => { await work(db); return null; });
}

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Lab_11_MemoryVsDistributedCache;

/// <summary>
/// Load balancer arkasındaki iki sunucu.
/// <para>
/// Her sunucu kendi servis kabını kurar; <c>IMemoryCache</c> o kabın içinde yaşar, yani
/// iki sunucunun belleği ayrıdır. <c>IDistributedCache</c> ise ikisinde de aynı SQL
/// tablosuna bakar. Ölçülen fark bundan başkası değildir.
/// </para>
/// <para>
/// <b>Yeniden başlatma</b> yeni kap kurularak taklit edilir. <c>IMemoryCache</c>'in ömrü
/// kabın ömrüdür, sürecin değil; bu yüzden ölçüm gerçek bir restart ile aynı sonucu verir.
/// </para>
/// </summary>
public sealed class ServerPair(Func<ServiceProvider> factory) : IAsyncDisposable
{
    private ServiceProvider[] _instances = [factory(), factory()];

    /// <summary>Yazma işlemini yapan sunucu. Diğeri yalnız okur.</summary>
    public ServiceProvider First => _instances[0];

    public ServiceProvider Second => _instances[1];

    public static IMemoryCache MemoryOf(ServiceProvider instance)
        => instance.GetRequiredService<IMemoryCache>();

    public static IDistributedCache DistributedOf(ServiceProvider instance)
        => instance.GetRequiredService<IDistributedCache>();

    /// <summary>Kaç sunucu anahtarın değerini kendi belleğinde bulabiliyor.</summary>
    public int MemoryReaders(string key)
        => _instances.Count(instance => MemoryOf(instance).TryGetValue(key, out _));

    /// <summary>Kaç sunucu anahtarın değerini dağıtık cache'ten okuyabiliyor.</summary>
    public async Task<int> DistributedReadersAsync(string key, CancellationToken ct = default)
    {
        var seen = 0;

        foreach (var instance in _instances)
        {
            if (await DistributedOf(instance).GetStringAsync(key, ct) is not null)
                seen++;
        }

        return seen;
    }

    /// <summary>İki sunucuyu da kapatır ve yeniden kurar.</summary>
    public async Task RestartAsync()
    {
        foreach (var instance in _instances)
            await instance.DisposeAsync();

        _instances = [factory(), factory()];
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var instance in _instances)
            await instance.DisposeAsync();
    }
}

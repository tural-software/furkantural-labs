using Microsoft.Extensions.DependencyInjection;

namespace Lab_05_DiScope;

/// <summary>
/// Kaç kez yaratıldığını söyleyebilen en küçük servis. Kimliği kurucusunda alır;
/// iki istek aynı kimliği görüyorsa aradaki nesne aynı nesnedir.
/// </summary>
public sealed class Probe
{
    private static int _created;

    public Probe() => InstanceId = Interlocked.Increment(ref _created);

    public int InstanceId { get; }
}

/// <summary>Yazıdaki <c>ReportBackgroundService</c>'in karşılığı: Singleton, Scoped tutuyor.</summary>
/// <param name="probe">Scoped kaydedilmiş bağımlılık — ilk örnek burada hapsolur.</param>
public sealed class CaptiveHolder(Probe probe)
{
    public Probe Held { get; } = probe;
}

/// <summary>Aynı işin doğrusu: bağımlılık tutulmaz, her kullanımda yeni scope açılır.</summary>
/// <param name="scopeFactory">Scope üreteci.</param>
public sealed class ScopeAwareHolder(IServiceScopeFactory scopeFactory)
{
    /// <summary>Taze bir scope'tan taze bir örnek alır ve kimliğini döndürür.</summary>
    public int ResolveInstanceId()
    {
        using var scope = scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<Probe>().InstanceId;
    }
}

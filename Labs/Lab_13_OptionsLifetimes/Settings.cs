using Microsoft.Extensions.Options;

namespace Lab_13_OptionsLifetimes;

/// <summary>Dosyadan bağlanan tek ayar. Ölçülen sayı bu alandır.</summary>
public sealed class ApiSettings
{
    public int MaxRequestsPerMinute { get; set; }
}

/// <summary>
/// <b>Doğru kullanım.</b> Singleton, ama değeri kullanım noktasında okuyor.
/// <see cref="IOptionsMonitor{T}.CurrentValue"/> her erişimde güncel değeri döndürür.
/// </summary>
public sealed class LiveGuard(IOptionsMonitor<ApiSettings> monitor)
{
    public int Limit => monitor.CurrentValue.MaxRequestsPerMinute;
}

/// <summary>
/// <b>Yanlış kullanım — doğru arayüz, yanlış an.</b> Arayüz <see cref="IOptionsMonitor{T}"/>,
/// yani "canlı" olan; ama değer constructor'da bir kez kopyalanıyor. Kopyalandığı anda
/// <c>IOptions</c> davranışına dönülür: sayı, nesnenin kurulduğu andaki sayıdır.
/// </summary>
public sealed class CopyingGuard(IOptionsMonitor<ApiSettings> monitor)
{
    private readonly int _limit = monitor.CurrentValue.MaxRequestsPerMinute;

    public int Limit => _limit;
}

/// <summary>
/// <b>Yanlış kullanım — captive dependency.</b> Scoped olan <see cref="IOptionsSnapshot{T}"/>,
/// singleton bir servise enjekte edilmiş. Kapsam doğrulaması kapalıyken (Production
/// varsayılanı) kimse engellemez: singleton, kök kapsamdan aldığı snapshot'ı süresiz tutar.
/// </summary>
public sealed class CaptiveGuard(IOptionsSnapshot<ApiSettings> snapshot)
{
    public int Limit => snapshot.Value.MaxRequestsPerMinute;
}

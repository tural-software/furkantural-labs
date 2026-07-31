namespace FurkanTural_Labs_Application.Diagnostics;

/// <summary>
/// Veritabanına giden komutları sayar. Laboratuvarların tamamı iddialarını bu sayaç
/// üzerinden kanıtlar: "N+1 var" demek yerine sorgu sayısını basar.
/// Uygulaması Persistence katmanındadır (EF Core <c>DbCommandInterceptor</c>).
/// </summary>
public interface IQueryCounter
{
    /// <summary>Son <see cref="Reset"/> çağrısından beri çalıştırılan komut sayısı.</summary>
    int Count { get; }

    /// <summary>Yakalanan SQL metinleri. Yalnız <see cref="CaptureSql"/> açıkken dolar.</summary>
    IReadOnlyList<string> Commands { get; }

    /// <summary>Açıkken her komutun SQL metni saklanır. Varsayılan kapalı (bellek + gürültü).</summary>
    bool CaptureSql { get; set; }

    /// <summary>Sayacı ve yakalanan SQL listesini sıfırlar.</summary>
    void Reset();

    /// <summary>Interceptor tarafından çağrılır; laboratuvar kodu bunu çağırmaz.</summary>
    void Record(string commandText);
}

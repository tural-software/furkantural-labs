namespace FurkanTural_Labs_Application.Diagnostics;

/// <summary>
/// Veritabanına giden komutları ve o komutlardan okunan satırları sayar.
/// Laboratuvarların tamamı iddialarını bu sayaç üzerinden kanıtlar: "N+1 var" ya da
/// "tüm tabloyu çekti" demek yerine sayıyı basar.
/// Uygulaması Persistence katmanındadır (EF Core <c>DbCommandInterceptor</c>).
/// </summary>
public interface IQueryCounter
{
    /// <summary>Son <see cref="Reset"/> çağrısından beri çalıştırılan komut sayısı.</summary>
    int Count { get; }

    /// <summary>
    /// Son <see cref="Reset"/> çağrısından beri veritabanından <b>okunan satır</b> sayısı.
    /// <para>
    /// Sorgu sayısının yetmediği yerde ölçülmesi gereken budur: <c>IEnumerable</c>'a düşen
    /// bir zincir de, doğru yazılmış bir zincir de tek sorgu çalıştırır — fark, birinin
    /// 10.000 satır taşıyıp diğerinin 10 satır taşımasıdır.
    /// </para>
    /// </summary>
    int RowCount { get; }

    /// <summary>Yakalanan SQL metinleri. Yalnız <see cref="CaptureSql"/> açıkken dolar.</summary>
    IReadOnlyList<string> Commands { get; }

    /// <summary>Açıkken her komutun SQL metni saklanır. Varsayılan kapalı (bellek + gürültü).</summary>
    bool CaptureSql { get; set; }

    /// <summary>Sayaçları ve yakalanan SQL listesini sıfırlar.</summary>
    void Reset();

    /// <summary>Interceptor tarafından çağrılır; laboratuvar kodu bunu çağırmaz.</summary>
    void Record(string commandText);

    /// <summary>Interceptor tarafından çağrılır; laboratuvar kodu bunu çağırmaz.</summary>
    /// <param name="rows">Kapanan okuyucudan çekilen satır sayısı.</param>
    void RecordRows(int rows);
}

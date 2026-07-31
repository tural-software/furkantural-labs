using System.Diagnostics;

namespace FurkanTural_Labs_Application.Diagnostics;

/// <summary>
/// Her laboratuvarın ortak koşum ve raporlama düzeneği.
/// <para>
/// Amaç tek: laboratuvarın iddiasını <b>çalıştırılabilir</b> kılmak. Senaryolar
/// beklentileriyle birlikte tanımlanır, sonuçlar tablo olarak basılır ve
/// <see cref="Print"/> süreç çıkış kodunu döndürür — beklenti tutmazsa <c>1</c>.
/// <c>/blogger</c> akışının "kanıt kapısı" adımı bu çıkış koduna bakar; yani
/// yazının iddiası ile laboratuvarın ölçtüğü sayı ayrışırsa yayın durur.
/// </para>
/// <para>
/// <b>Bir laboratuvar tek birim ölçer.</b> Sorgu sayan bir tabloya satır sayısı
/// karıştırılırsa okuyucu neyin neyle kıyaslandığını kaybeder; bu kısıt aynı zamanda
/// "her laboratuvar tek iddia taşır" kuralını zorlar.
/// </para>
/// </summary>
/// <param name="title">Laboratuvarın başlığı.</param>
/// <param name="claim">Kanıtlanan iddia, tek cümlede.</param>
/// <param name="counter">Sorgu/satır sayacı. Her senaryodan önce sıfırlanır.</param>
/// <param name="metric">
/// Ölçüm biriminin sütun başlığı: "Sorgu", "Satır", "İzlenen"… Varsayılan
/// <see cref="ScenarioAsync(string, Expectation, Func{Task}, string?)"/> ile uyumlu olsun
/// diye "Sorgu"dur; başka bir birim ölçen laboratuvar
/// <see cref="MeasureAsync"/> kullanır ve başlığı burada değiştirir.
/// </param>
public sealed class LabReport(string title, string claim, IQueryCounter counter, string metric = "Sorgu")
{
    private readonly List<ScenarioResult> _results = [];

    /// <summary>Bir senaryoyu çalıştırır ve <b>sorgu sayısını</b> ölçer.</summary>
    /// <param name="name">Tabloda görünecek senaryo adı.</param>
    /// <param name="expectation">Sorgu sayısı beklentisi.</param>
    /// <param name="action">Ölçülecek iş.</param>
    /// <param name="note">Tablonun altına düşülecek kısa açıklama.</param>
    public Task ScenarioAsync(string name, Expectation expectation, Func<Task> action, string? note = null)
        => RunAsync(name, expectation, async () => { await action(); return counter.Count; }, note);

    /// <summary>
    /// Bir senaryoyu çalıştırır ve <b>işin döndürdüğü sayıyı</b> ölçer.
    /// Sayaçtan okunacaksa <c>counter.RowCount</c>, laboratuvarın kendi hesabıysa
    /// (izlenen entity, benzersiz satır, doğru dönen değer…) o hesap döndürülür.
    /// Sayaç bu iş başlamadan önce sıfırlandığı için <c>counter</c> okumaları güvenlidir.
    /// </summary>
    /// <param name="name">Tabloda görünecek senaryo adı.</param>
    /// <param name="expectation">Ölçülen sayının karşılaması gereken beklenti.</param>
    /// <param name="measure">Ölçülecek iş; sonuç olarak ölçümü döndürür.</param>
    /// <param name="note">Tablonun altına düşülecek kısa açıklama.</param>
    public Task MeasureAsync(string name, Expectation expectation, Func<Task<int>> measure, string? note = null)
        => RunAsync(name, expectation, measure, note);

    private async Task RunAsync(string name, Expectation expectation, Func<Task<int>> measure, string? note)
    {
        counter.Reset();

        // precise: true — ölçüm süresi milisaniyeler; hızlı yol tahmini burada yanıltır.
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var sw = Stopwatch.StartNew();
        var measured = await measure();
        sw.Stop();
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;

        _results.Add(new ScenarioResult(name, measured, expectation, sw.ElapsedMilliseconds, allocated, note));
    }

    /// <summary>Sonuç tablosunu basar. Dönüş: süreç çıkış kodu (0 = kanıt tuttu).</summary>
    public int Print()
    {
        var nameWidth = Math.Max(8, _results.Max(r => r.Name.Length));
        var metricWidth = Math.Max(6, metric.Length);
        var expWidth = Math.Max(9, _results.Max(r => r.Expectation.Text.Length));

        Console.WriteLine();
        Console.WriteLine(title);
        Console.WriteLine(new string('═', Math.Max(title.Length, 60)));
        Console.WriteLine($"İddia: {claim}");
        Console.WriteLine();

        Console.WriteLine(
            $"{"Senaryo".PadRight(nameWidth)}  {metric.PadLeft(metricWidth)}  " +
            $"{"Beklenen".PadRight(expWidth)}  {"Süre",7}  {"Bellek",9}  Sonuç");
        Console.WriteLine(
            $"{new string('─', nameWidth)}  {new string('─', metricWidth)}  " +
            $"{new string('─', expWidth)}  {new string('─', 7)}  {new string('─', 9)}  ─────");

        foreach (var r in _results)
        {
            var verdict = r.Passed ? "GEÇTİ" : "KALDI";
            Console.WriteLine(
                $"{r.Name.PadRight(nameWidth)}  {r.Measured.ToString("N0").PadLeft(metricWidth)}  " +
                $"{r.Expectation.Text.PadRight(expWidth)}  {r.ElapsedMs + " ms",7}  " +
                $"{Humanize(r.AllocatedBytes),9}  {verdict}");
        }

        var notes = _results.Where(r => !string.IsNullOrWhiteSpace(r.Note)).ToList();
        if (notes.Count > 0)
        {
            Console.WriteLine();
            foreach (var r in notes)
                Console.WriteLine($"  · {r.Name}: {r.Note}");
        }

        var failed = _results.Where(r => !r.Passed).ToList();
        Console.WriteLine();

        if (failed.Count == 0)
        {
            Console.WriteLine($"KANIT TUTTU — {_results.Count} senaryonun {_results.Count}'i beklentiyi karşıladı.");
            return 0;
        }

        Console.WriteLine($"KANIT TUTMADI — {failed.Count} senaryo beklentiyi karşılamadı:");
        foreach (var r in failed)
            Console.WriteLine($"  · {r.Name}: beklenen {r.Expectation.Text}, ölçülen {r.Measured:N0}");
        Console.WriteLine();
        Console.WriteLine("Yazıdaki sayılar bu tablodan gelir. Ayrışma varsa düzeltilecek olan yazıdır.");
        return 1;
    }

    private static string Humanize(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):0.0} MB",
        >= 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes} B"
    };
}

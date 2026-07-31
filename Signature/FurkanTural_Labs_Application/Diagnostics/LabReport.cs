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
/// </summary>
public sealed class LabReport(string title, string claim, IQueryCounter counter)
{
    private readonly List<ScenarioResult> _results = [];

    /// <summary>Bir senaryoyu ölçerek çalıştırır ve sonucu rapora ekler.</summary>
    /// <param name="name">Tabloda görünecek senaryo adı.</param>
    /// <param name="expectation">Sorgu sayısı beklentisi.</param>
    /// <param name="action">Ölçülecek iş.</param>
    /// <param name="note">Tablonun altına düşülecek kısa açıklama.</param>
    public async Task ScenarioAsync(string name, Expectation expectation, Func<Task> action, string? note = null)
    {
        counter.Reset();
        var sw = Stopwatch.StartNew();
        await action();
        sw.Stop();
        _results.Add(new ScenarioResult(name, counter.Count, expectation, sw.ElapsedMilliseconds, note));
    }

    /// <summary>Sonuç tablosunu basar. Dönüş: süreç çıkış kodu (0 = kanıt tuttu).</summary>
    public int Print()
    {
        var nameWidth = Math.Max(8, _results.Max(r => r.Name.Length));
        var expWidth = Math.Max(9, _results.Max(r => r.Expectation.Text.Length));

        Console.WriteLine();
        Console.WriteLine(title);
        Console.WriteLine(new string('═', Math.Max(title.Length, 60)));
        Console.WriteLine($"İddia: {claim}");
        Console.WriteLine();

        Console.WriteLine($"{"Senaryo".PadRight(nameWidth)}  {"Sorgu",6}  {"Beklenen".PadRight(expWidth)}  {"Süre",7}  Sonuç");
        Console.WriteLine($"{new string('─', nameWidth)}  {new string('─', 6)}  {new string('─', expWidth)}  {new string('─', 7)}  ─────");

        foreach (var r in _results)
        {
            var verdict = r.Passed ? "GEÇTİ" : "KALDI";
            Console.WriteLine($"{r.Name.PadRight(nameWidth)}  {r.QueryCount,6}  {r.Expectation.Text.PadRight(expWidth)}  {r.ElapsedMs + " ms",7}  {verdict}");
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
            Console.WriteLine($"  · {r.Name}: beklenen {r.Expectation.Text}, ölçülen {r.QueryCount}");
        Console.WriteLine();
        Console.WriteLine("Yazıdaki sayılar bu tablodan gelir. Ayrışma varsa düzeltilecek olan yazıdır.");
        return 1;
    }
}

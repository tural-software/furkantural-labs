namespace FurkanTural_Labs_Application.Diagnostics;

/// <summary>
/// Bir senaryonun sorgu sayısı için beklenti. Laboratuvarın iddiası burada
/// çalıştırılabilir hale gelir: tutmazsa süreç sıfırdan farklı kodla biter.
/// </summary>
/// <param name="Text">Tabloda gösterilecek kısa gösterim (örn. <c>= 101</c>).</param>
/// <param name="Holds">Ölçülen sayıyı değerlendiren yüklem.</param>
public sealed record Expectation(string Text, Func<int, bool> Holds)
{
    public static Expectation Exactly(int n) => new($"= {n}", c => c == n);
    public static Expectation AtLeast(int n) => new($"≥ {n}", c => c >= n);
    public static Expectation AtMost(int n) => new($"≤ {n}", c => c <= n);
    public static Expectation Between(int low, int high) => new($"{low}–{high}", c => c >= low && c <= high);

    /// <summary>Sayı denetlenmez; senaryo yalnız ölçüm için çalıştırılır.</summary>
    public static Expectation Any { get; } = new("—", _ => true);
}

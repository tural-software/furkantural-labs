namespace FurkanTural_Labs_Application.Diagnostics;

/// <summary>Tek bir senaryonun ölçüm sonucu.</summary>
/// <param name="Name">Tabloda görünen senaryo adı.</param>
/// <param name="Measured">Ölçülen sayı. Neyi saydığı laboratuvarın ölçüm birimine bağlıdır.</param>
/// <param name="Expectation">Bu sayının karşılaması gereken beklenti.</param>
/// <param name="ElapsedMs">Senaryonun süresi.</param>
/// <param name="AllocatedBytes">Senaryo boyunca ayrılan toplam bellek.</param>
/// <param name="Note">Tablonun altına düşülecek kısa açıklama.</param>
public sealed record ScenarioResult(
    string Name,
    int Measured,
    Expectation Expectation,
    long ElapsedMs,
    long AllocatedBytes,
    string? Note)
{
    public bool Passed => Expectation.Holds(Measured);
}

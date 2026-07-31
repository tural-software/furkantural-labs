namespace FurkanTural_Labs_Application.Diagnostics;

/// <summary>Tek bir senaryonun ölçüm sonucu.</summary>
public sealed record ScenarioResult(
    string Name,
    int QueryCount,
    Expectation Expectation,
    long ElapsedMs,
    string? Note)
{
    public bool Passed => Expectation.Holds(QueryCount);
}

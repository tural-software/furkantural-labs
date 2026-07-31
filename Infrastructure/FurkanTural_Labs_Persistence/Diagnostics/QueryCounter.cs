using FurkanTural_Labs_Application.Diagnostics;

namespace FurkanTural_Labs_Persistence.Diagnostics;

/// <inheritdoc cref="IQueryCounter"/>
public sealed class QueryCounter : IQueryCounter
{
    private readonly Lock _gate = new();
    private readonly List<string> _commands = [];
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public IReadOnlyList<string> Commands
    {
        get { lock (_gate) return [.. _commands]; }
    }

    public bool CaptureSql { get; set; }

    public void Reset()
    {
        lock (_gate)
        {
            _commands.Clear();
            Volatile.Write(ref _count, 0);
        }
    }

    public void Record(string commandText)
    {
        Interlocked.Increment(ref _count);
        if (!CaptureSql) return;
        lock (_gate) _commands.Add(commandText);
    }
}

using Microsoft.Extensions.Logging;

namespace Lab_18_StructuredLogging;

/// <summary>Sink'e ulaşan tek bir kayıt: şablon, adlandırılmış alanlar, scope alanları, exception.</summary>
/// <param name="Template">Değişmeyen mesaj şablonu.</param>
/// <param name="Fields">Kaydın kendi adlandırılmış alanları.</param>
/// <param name="ScopeFields">Kaydı çevreleyen scope'lardan gelen alanlar.</param>
/// <param name="Exception">Ayrı parametre olarak geçirilen exception; gömüldüyse <c>null</c>.</param>
public sealed record LogEntry(
    string Template,
    IReadOnlyDictionary<string, object?> Fields,
    IReadOnlyDictionary<string, object?> ScopeFields,
    Exception? Exception)
{
    /// <summary>Alanı adıyla arar; önce kaydın kendi alanlarına, sonra scope'a bakar.</summary>
    /// <param name="name">Alan adı.</param>
    public object? Field(string name)
        => Fields.TryGetValue(name, out var value) ? value
         : ScopeFields.TryGetValue(name, out var scoped) ? scoped
         : null;
}

/// <summary>
/// Gerçek bir sink'in yaptığını yapan en küçük sağlayıcı: kaydı metne çevirmeden,
/// geldiği hâliyle saklar. Ölçmek istediğimiz şey tam olarak budur — konsola basılan
/// cümle değil, sink'e ulaşan veri.
/// </summary>
public sealed class RecordingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly List<LogEntry> _entries = [];
    private readonly Lock _gate = new();
    private IExternalScopeProvider? _scopes;

    /// <summary>Son <see cref="Clear"/> çağrısından beri yakalanan kayıtlar.</summary>
    public IReadOnlyList<LogEntry> Entries
    {
        get { lock (_gate) return [.. _entries]; }
    }

    public void Clear()
    {
        lock (_gate) _entries.Clear();
    }

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose() { }

    private void Add(LogEntry entry)
    {
        lock (_gate) _entries.Add(entry);
    }

    private sealed class RecordingLogger(RecordingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => provider._scopes?.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var fields = ToDictionary(state);

            // {OriginalFormat} şablonun kendisidir; alan değil. Interpolation'da bu değer
            // çoktan birleştirilmiş metindir ve yanında hiçbir adlandırılmış alan bulunmaz.
            var template = fields.TryGetValue("{OriginalFormat}", out var format)
                ? format?.ToString() ?? string.Empty
                : formatter(state, exception);

            fields.Remove("{OriginalFormat}");

            var scopeFields = new Dictionary<string, object?>();
            provider._scopes?.ForEachScope((scope, target) =>
            {
                foreach (var pair in ToDictionary(scope))
                    if (pair.Key != "{OriginalFormat}")
                        target[pair.Key] = pair.Value;
            }, scopeFields);

            provider.Add(new LogEntry(template, fields, scopeFields, exception));
        }

        // IEnumerable üzerinden bakılıyor, IReadOnlyList üzerinden değil: kaydın kendi durumu
        // (FormattedLogValues) listedir ama BeginScope'a verilen Dictionary değildir ve
        // liste araması onu sessizce boş geçer.
        private static Dictionary<string, object?> ToDictionary(object? state)
        {
            var fields = new Dictionary<string, object?>();

            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
                foreach (var pair in pairs)
                    fields[pair.Key] = pair.Value;

            return fields;
        }
    }
}

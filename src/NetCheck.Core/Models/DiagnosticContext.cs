using System.Collections.Concurrent;

namespace NetCheck.Core.Models;

public sealed class DiagnosticContext
{
    private readonly ConcurrentDictionary<string, object> _values =
        new(StringComparer.OrdinalIgnoreCase);

    public DiagnosticContext(NetworkSnapshot network, DiagnosticOptions options)
    {
        Network = network ?? throw new ArgumentNullException(nameof(network));
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public NetworkSnapshot Network { get; }

    public DiagnosticOptions Options { get; }

    public void Set<T>(string key, T value) where T : notnull => _values[key] = value;

    public bool TryGet<T>(string key, out T? value)
    {
        if (_values.TryGetValue(key, out var stored) && stored is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }
}


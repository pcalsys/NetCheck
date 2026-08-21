using System.Text.Json;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;

namespace NetCheck.Infrastructure.Storage;

public sealed class JsonSettingsStore : ISettingsStore, IDisposable
{
    private readonly AppDataPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonSettingsStore(AppDataPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public async Task<DiagnosticOptions> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_paths.SettingsFile))
            {
                return new DiagnosticOptions();
            }

            try
            {
                await using var stream = File.OpenRead(_paths.SettingsFile);
                var settings = await JsonSerializer.DeserializeAsync<DiagnosticOptions>(
                    stream,
                    JsonDefaults.Options,
                    cancellationToken).ConfigureAwait(false);
                return settings is null ? new DiagnosticOptions() : Normalize(settings);
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                return new DiagnosticOptions();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(
        DiagnosticOptions settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = Normalize(settings);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_paths.RootDirectory);
            var content = JsonSerializer.Serialize(normalized, JsonDefaults.Options);
            var temporaryFile = $"{_paths.SettingsFile}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllTextAsync(temporaryFile, content, cancellationToken).ConfigureAwait(false);
                File.Move(temporaryFile, _paths.SettingsFile, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryFile))
                {
                    File.Delete(temporaryFile);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private static DiagnosticOptions Normalize(DiagnosticOptions settings) => settings with
    {
        DnsTestHost = string.IsNullOrWhiteSpace(settings.DnsTestHost)
            ? "www.microsoft.com"
            : settings.DnsTestHost.Trim(),
        InternetPingTargets = settings.InternetPingTargets
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Select(target => target.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray(),
        PingTimeoutMilliseconds = Math.Clamp(settings.PingTimeoutMilliseconds, 500, 5000),
        HttpTimeoutSeconds = Math.Clamp(settings.HttpTimeoutSeconds, 2, 30),
        StabilitySampleCount = Math.Clamp(settings.StabilitySampleCount, 3, 20),
        PacketLossWarningPercent = Math.Clamp(settings.PacketLossWarningPercent, 1, 100),
        LatencyWarningMilliseconds = Math.Clamp(settings.LatencyWarningMilliseconds, 10, 2000)
    };
}


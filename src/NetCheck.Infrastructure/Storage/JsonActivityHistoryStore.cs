using System.Text.Json;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;

namespace NetCheck.Infrastructure.Storage;

public sealed class JsonActivityHistoryStore : IActivityHistoryStore, IDisposable
{
    private readonly AppDataPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonActivityHistoryStore(AppDataPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public async Task SaveAsync(ActivityHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Validate(entry);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_paths.ActivitiesDirectory);
            var destination = Path.Combine(_paths.ActivitiesDirectory, $"{entry.Id:N}.json");
            var json = JsonSerializer.Serialize(entry, JsonDefaults.Options);
            await WriteAtomicallyAsync(destination, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ActivityHistoryEntry>> GetRecentAsync(
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        if (maximumCount <= 0)
        {
            return Array.Empty<ActivityHistoryEntry>();
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(_paths.ActivitiesDirectory))
            {
                return Array.Empty<ActivityHistoryEntry>();
            }

            var files = new DirectoryInfo(_paths.ActivitiesDirectory)
                .EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(Math.Min(maximumCount * 2, 400))
                .ToArray();
            var entries = new List<ActivityHistoryEntry>(Math.Min(maximumCount, files.Length));

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await using var stream = file.OpenRead();
                    var entry = await JsonSerializer.DeserializeAsync<ActivityHistoryEntry>(
                        stream,
                        JsonDefaults.Options,
                        cancellationToken).ConfigureAwait(false);
                    if (entry is not null && IsValid(entry))
                    {
                        entries.Add(entry);
                    }
                }
                catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
                {
                    // A malformed or inaccessible activity must not prevent valid history from loading.
                }

                if (entries.Count >= maximumCount)
                {
                    break;
                }
            }

            return entries
                .OrderByDescending(entry => entry.OccurredAtUtc)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(_paths.ActivitiesDirectory))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(
                         _paths.ActivitiesDirectory,
                         "*.json",
                         SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Delete(file);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private static bool IsValid(ActivityHistoryEntry entry) => entry.Kind switch
    {
        ActivityHistoryKind.SpeedTest => entry.SpeedTestResult is not null,
        ActivityHistoryKind.SettingsChanged or ActivityHistoryKind.LanguageChanged =>
            entry.SettingChanges is { Count: > 0 }
            && entry.SettingChanges.All(change =>
                !string.IsNullOrWhiteSpace(change.SettingName)
                && !string.Equals(change.PreviousValue, change.NewValue, StringComparison.Ordinal)),
        _ => false
    };

    private static void Validate(ActivityHistoryEntry entry)
    {
        if (!IsValid(entry))
        {
            throw new ArgumentException("The activity history entry is incomplete or contains no change.", nameof(entry));
        }
    }

    private static async Task WriteAtomicallyAsync(
        string destination,
        string content,
        CancellationToken cancellationToken)
    {
        var temporaryFile = $"{destination}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryFile, content, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryFile, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }
    }
}

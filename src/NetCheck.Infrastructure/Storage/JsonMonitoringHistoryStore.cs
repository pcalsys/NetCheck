using System.Text.Json;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;

namespace NetCheck.Infrastructure.Storage;

public sealed class JsonMonitoringHistoryStore : IMonitoringHistoryStore, IDisposable
{
    private readonly AppDataPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonMonitoringHistoryStore(AppDataPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public async Task SaveAsync(
        MonitoringSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.CompletedAtUtc < session.StartedAtUtc)
        {
            throw new ArgumentException("The monitoring session has an invalid time range.", nameof(session));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_paths.MonitoringSessionsDirectory);
            var destination = Path.Combine(
                _paths.MonitoringSessionsDirectory,
                $"{session.StartedAtUtc:yyyyMMdd-HHmmss}-{session.Id:N}.json");
            var json = JsonSerializer.Serialize(session, JsonDefaults.Options);
            await WriteAtomicallyAsync(destination, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<MonitoringSession>> GetRecentAsync(
        int maximumCount = 100,
        CancellationToken cancellationToken = default)
    {
        if (maximumCount <= 0)
        {
            return Array.Empty<MonitoringSession>();
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(_paths.MonitoringSessionsDirectory))
            {
                return Array.Empty<MonitoringSession>();
            }

            var candidates = new DirectoryInfo(_paths.MonitoringSessionsDirectory)
                .EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(Math.Min(maximumCount * 2, 400))
                .ToArray();
            var sessions = new List<MonitoringSession>(Math.Min(candidates.Length, maximumCount));
            foreach (var file in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await using var stream = file.OpenRead();
                    var session = await JsonSerializer.DeserializeAsync<MonitoringSession>(
                        stream,
                        JsonDefaults.Options,
                        cancellationToken).ConfigureAwait(false);
                    if (session is not null && session.CompletedAtUtc >= session.StartedAtUtc)
                    {
                        sessions.Add(session);
                    }
                }
                catch (Exception exception) when (exception is JsonException
                    or IOException
                    or UnauthorizedAccessException)
                {
                    // One damaged session must never hide the remaining valid history.
                }

                if (sessions.Count >= maximumCount)
                {
                    break;
                }
            }

            return sessions.OrderByDescending(session => session.CompletedAtUtc).ToArray();
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
            if (!Directory.Exists(_paths.MonitoringSessionsDirectory))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(
                         _paths.MonitoringSessionsDirectory,
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

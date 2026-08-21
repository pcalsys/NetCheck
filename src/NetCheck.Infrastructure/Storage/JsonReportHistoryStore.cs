using System.Text.Json;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;

namespace NetCheck.Infrastructure.Storage;

public sealed class JsonReportHistoryStore : IReportHistoryStore, IDisposable
{
    private readonly AppDataPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonReportHistoryStore(AppDataPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public async Task SaveAsync(DiagnosticReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_paths.ReportsDirectory);
            var destination = Path.Combine(_paths.ReportsDirectory, $"{report.Id:N}.json");
            var json = JsonSerializer.Serialize(report, JsonDefaults.Options);
            await WriteAtomicallyAsync(destination, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<DiagnosticReport>> GetRecentAsync(
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        if (maximumCount <= 0)
        {
            return Array.Empty<DiagnosticReport>();
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(_paths.ReportsDirectory))
            {
                return Array.Empty<DiagnosticReport>();
            }

            var files = new DirectoryInfo(_paths.ReportsDirectory)
                .EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(Math.Min(maximumCount * 2, 200))
                .ToArray();

            var reports = new List<DiagnosticReport>(Math.Min(maximumCount, files.Length));
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await using var stream = file.OpenRead();
                    var report = await JsonSerializer.DeserializeAsync<DiagnosticReport>(
                        stream,
                        JsonDefaults.Options,
                        cancellationToken).ConfigureAwait(false);
                    if (report is not null)
                    {
                        reports.Add(report);
                    }
                }
                catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
                {
                    // A malformed or inaccessible history item must not prevent the rest from loading.
                }

                if (reports.Count >= maximumCount)
                {
                    break;
                }
            }

            return reports
                .OrderByDescending(report => report.CompletedAtUtc)
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
            if (!Directory.Exists(_paths.ReportsDirectory))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(
                         _paths.ReportsDirectory,
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


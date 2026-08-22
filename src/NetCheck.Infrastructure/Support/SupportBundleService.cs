using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NetCheck.Core.Abstractions;
using NetCheck.Infrastructure.Storage;

namespace NetCheck.Infrastructure.Support;

public sealed partial class SupportBundleService : ISupportBundleService
{
    private const long MaximumSourceFileBytes = 2 * 1024 * 1024;
    private const int MaximumFilesPerCategory = 30;
    private readonly AppDataPaths _paths;

    public SupportBundleService(AppDataPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public async Task CreateAsync(
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var destination = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(destination)
            ?? throw new ArgumentException("The support bundle needs a destination directory.", nameof(destinationPath));
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");

        var sourceFiles = EnumerateSources();
        var sensitiveTokens = await CollectSensitiveTokensAsync(sourceFiles, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                await WriteEntryAsync(
                    archive,
                    "README.txt",
                    "NetCheck support bundle\n\nThis archive was created locally. User names, computer names, SSIDs, MAC addresses and IP addresses are automatically redacted. Review the archive before sharing it.\n",
                    cancellationToken).ConfigureAwait(false);
                var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
                    ?? typeof(SupportBundleService).Assembly.GetName().Version?.ToString(3)
                    ?? "unknown";
                var manifest = JsonSerializer.Serialize(new
                {
                    generatedAtUtc = DateTimeOffset.UtcNow,
                    applicationVersion = version,
                    operatingSystem = Environment.OSVersion.VersionString,
                    includedFileCount = sourceFiles.Count
                }, new JsonSerializerOptions { WriteIndented = true });
                await WriteEntryAsync(
                    archive,
                    "manifest.json",
                    Redact(manifest, sensitiveTokens),
                    cancellationToken).ConfigureAwait(false);

                foreach (var source in sourceFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var content = await ReadLimitedTextAsync(source.SourcePath, cancellationToken)
                            .ConfigureAwait(false);
                        await WriteEntryAsync(
                            archive,
                            source.EntryName,
                            Redact(content, sensitiveTokens),
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (exception is IOException
                        or UnauthorizedAccessException
                        or DecoderFallbackException)
                    {
                        // A locked or malformed local file is skipped independently.
                    }
                }
            }

            File.Move(temporaryPath, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private List<SupportSource> EnumerateSources()
    {
        var sources = new List<SupportSource>();
        AddSingleSource(sources, _paths.LogFile, "logs/NetCheck.log");
        AddSingleSource(sources, _paths.SettingsFile, "settings/settings.json");
        AddDirectorySources(sources, _paths.ReportsDirectory, "reports");
        AddDirectorySources(sources, _paths.ActivitiesDirectory, "activities");
        AddDirectorySources(sources, _paths.MonitoringSessionsDirectory, "monitoring");
        return sources;
    }

    private static void AddSingleSource(
        ICollection<SupportSource> sources,
        string sourcePath,
        string entryName)
    {
        if (IsEligibleFile(sourcePath))
        {
            sources.Add(new SupportSource(sourcePath, entryName));
        }
    }

    private static void AddDirectorySources(
        ICollection<SupportSource> sources,
        string sourceDirectory,
        string entryDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        var files = new DirectoryInfo(sourceDirectory)
            .EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
            .Where(file => file.Length <= MaximumSourceFileBytes)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(MaximumFilesPerCategory)
            .ToArray();
        for (var index = 0; index < files.Length; index++)
        {
            sources.Add(new SupportSource(
                files[index].FullName,
                $"{entryDirectory}/{index + 1:D3}.json"));
        }
    }

    private static bool IsEligibleFile(string path)
    {
        try
        {
            return File.Exists(path) && new FileInfo(path).Length <= MaximumSourceFileBytes;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static async Task<HashSet<string>> CollectSensitiveTokensAsync(
        IReadOnlyList<SupportSource> sources,
        CancellationToken cancellationToken)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddSensitiveToken(tokens, Environment.UserName);
        AddSensitiveToken(tokens, Environment.MachineName);
        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var content = await ReadLimitedTextAsync(source.SourcePath, cancellationToken)
                    .ConfigureAwait(false);
                foreach (Match match in SsidJsonRegex().Matches(content))
                {
                    AddSensitiveToken(tokens, match.Groups[1].Value);
                }
                foreach (Match match in SsidTextRegex().Matches(content))
                {
                    AddSensitiveToken(tokens, match.Groups[1].Value.Trim());
                }
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or DecoderFallbackException)
            {
                // Sensitive tokens from unreadable files cannot enter the bundle because those files are skipped.
            }
        }

        return tokens;
    }

    private static void AddSensitiveToken(ISet<string> tokens, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            tokens.Add(value);
        }
    }

    private static string Redact(string content, IEnumerable<string> sensitiveTokens)
    {
        var redacted = content;
        foreach (var token in sensitiveTokens.OrderByDescending(value => value.Length))
        {
            redacted = Regex.Replace(
                redacted,
                Regex.Escape(token),
                "[redacted]",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        redacted = SsidPropertyRegex().Replace(redacted, "$1[redacted]$2");
        redacted = MacAddressRegex().Replace(redacted, "[redacted-mac]");
        redacted = Ipv4Regex().Replace(redacted, match =>
            IPAddress.TryParse(match.Value, out _) ? "[redacted-ip]" : match.Value);
        redacted = Ipv6CandidateRegex().Replace(redacted, match =>
            IPAddress.TryParse(match.Value.Trim('[', ']'), out var address)
                && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                ? "[redacted-ip]"
                : match.Value);
        return redacted;
    }

    private static async Task<string> ReadLimitedTextAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > MaximumSourceFileBytes)
        {
            throw new IOException("The support source file exceeds the size limit.");
        }

        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteEntryAsync(
        ZipArchive archive,
        string entryName,
        string content,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await using var writer = new StreamWriter(
            entryStream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            leaveOpen: false);
        await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    [GeneratedRegex("\\\"ssid\\\"\\s*:\\s*\\\"([^\\\"]*)\\\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SsidJsonRegex();

    [GeneratedRegex("\\bssid\\s*[:=]\\s*[\\\"']?([^\\\"'\\r\\n;,]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SsidTextRegex();

    [GeneratedRegex("(\\\"ssid\\\"\\s*:\\s*\\\")[^\\\"]*(\\\")", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SsidPropertyRegex();

    [GeneratedRegex("(?<![0-9A-Fa-f])(?:[0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}(?![0-9A-Fa-f])", RegexOptions.CultureInvariant)]
    private static partial Regex MacAddressRegex();

    [GeneratedRegex("(?<![0-9])(?:[0-9]{1,3}\\.){3}[0-9]{1,3}(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex Ipv4Regex();

    [GeneratedRegex("(?<![0-9A-Fa-f:])\\[?[0-9A-Fa-f]{0,4}(?::[0-9A-Fa-f]{0,4}){2,7}\\]?(?![0-9A-Fa-f:])", RegexOptions.CultureInvariant)]
    private static partial Regex Ipv6CandidateRegex();

    private sealed record SupportSource(string SourcePath, string EntryName);
}

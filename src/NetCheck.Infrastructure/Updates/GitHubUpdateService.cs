using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;

namespace NetCheck.Infrastructure.Updates;

public sealed class GitHubUpdateService : IUpdateService, IDisposable
{
    public static readonly Uri LatestReleaseApiUri =
        new("https://api.github.com/repos/pcalsys/NetCheck/releases/latest");

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    public GitHubUpdateService(HttpClient? httpClient = null)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? CreateHttpClient();
    }

    public async Task<UpdateCheckResult> CheckAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(currentVersion);

        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("NetCheck/1.2");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString();
        if (string.IsNullOrWhiteSpace(tag)
            || !Version.TryParse(tag.TrimStart('v', 'V'), out var latestVersion))
        {
            throw new InvalidDataException("The official GitHub release has an invalid version tag.");
        }

        var releasePage = ParseTrustedUri(root.GetProperty("html_url").GetString(), allowApiHost: false);
        Uri? packageUri = null;
        Uri? checksumUri = null;
        var expectedPackageName = $"NetCheck-{latestVersion.ToString(3)}-win-x64.zip";
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                if (!string.Equals(name, expectedPackageName, StringComparison.Ordinal)
                    && !string.Equals(name, $"{expectedPackageName}.sha256", StringComparison.Ordinal))
                {
                    continue;
                }

                var uri = ParseTrustedUri(
                    asset.GetProperty("browser_download_url").GetString(),
                    allowApiHost: false);
                if (string.Equals(name, expectedPackageName, StringComparison.Ordinal))
                {
                    packageUri = uri;
                }
                else
                {
                    checksumUri = uri;
                }
            }
        }

        if (packageUri is null || checksumUri is null)
        {
            packageUri = null;
            checksumUri = null;
        }

        DateTimeOffset? publishedAt = null;
        if (root.TryGetProperty("published_at", out var publishedElement)
            && publishedElement.TryGetDateTimeOffset(out var parsedPublishedAt))
        {
            publishedAt = parsedPublishedAt;
        }

        return new UpdateCheckResult
        {
            CurrentVersion = currentVersion,
            LatestVersion = latestVersion,
            UpdateAvailable = latestVersion > currentVersion,
            ReleasePageUri = releasePage,
            PackageUri = packageUri,
            ChecksumUri = checksumUri,
            PublishedAtUtc = publishedAt
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        _disposed = true;
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All
        };
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    private static Uri ParseTrustedUri(string? value, bool allowApiHost)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
                && !(allowApiHost
                    && string.Equals(uri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidDataException("The GitHub release contains an untrusted URL.");
        }

        var expectedPrefix = "/pcalsys/NetCheck/";
        if (!uri.AbsolutePath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The GitHub release URL does not belong to NetCheck.");
        }

        return uri;
    }
}

using System.Net;
using System.Text;
using NetCheck.Infrastructure.Updates;

namespace NetCheck.Infrastructure.Tests;

public sealed class GitHubUpdateServiceTests
{
    [Fact]
    public async Task CheckAsync_RecognizesOnlyTheMatchingZipAndChecksumPair()
    {
        const string json = """
            {
              "tag_name": "v1.2.0",
              "html_url": "https://github.com/pcalsys/NetCheck/releases/tag/v1.2.0",
              "published_at": "2026-08-22T12:00:00Z",
              "assets": [
                {
                  "name": "NetCheck-1.2.0-win-x64.zip",
                  "browser_download_url": "https://github.com/pcalsys/NetCheck/releases/download/v1.2.0/NetCheck-1.2.0-win-x64.zip"
                },
                {
                  "name": "NetCheck-1.2.0-win-x64.zip.sha256",
                  "browser_download_url": "https://github.com/pcalsys/NetCheck/releases/download/v1.2.0/NetCheck-1.2.0-win-x64.zip.sha256"
                }
              ]
            }
            """;
        using var service = CreateService(json);

        var result = await service.CheckAsync(new Version(1, 1, 0));

        Assert.True(result.UpdateAvailable);
        Assert.True(result.HasVerifiedReleaseAssets);
        Assert.Equal(new Version(1, 2, 0), result.LatestVersion);
        Assert.Equal(Uri.UriSchemeHttps, result.PackageUri?.Scheme);
    }

    [Fact]
    public async Task CheckAsync_WhenChecksumIsMissing_DoesNotExposeEitherAsset()
    {
        const string json = """
            {
              "tag_name": "v1.2.0",
              "html_url": "https://github.com/pcalsys/NetCheck/releases/tag/v1.2.0",
              "assets": [
                {
                  "name": "NetCheck-1.2.0-win-x64.zip",
                  "browser_download_url": "https://github.com/pcalsys/NetCheck/releases/download/v1.2.0/NetCheck-1.2.0-win-x64.zip"
                }
              ]
            }
            """;
        using var service = CreateService(json);

        var result = await service.CheckAsync(new Version(1, 1, 0));

        Assert.False(result.HasVerifiedReleaseAssets);
        Assert.Null(result.PackageUri);
        Assert.Null(result.ChecksumUri);
    }

    [Fact]
    public async Task CheckAsync_RejectsAReleaseUrlOutsideTheOfficialRepository()
    {
        const string json = """
            {
              "tag_name": "v1.2.0",
              "html_url": "https://example.com/fake-release",
              "assets": []
            }
            """;
        using var service = CreateService(json);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.CheckAsync(new Version(1, 1, 0)));
    }

    private static GitHubUpdateService CreateService(string json)
    {
        var handler = new StubHandler(request =>
        {
            Assert.Equal(GitHubUpdateService.LatestReleaseApiUri, request.RequestUri);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });
        return new GitHubUpdateService(new HttpClient(handler));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(responseFactory(request));
    }
}

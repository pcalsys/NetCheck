using System.Net;
using NetCheck.Core.Diagnostics;
using NetCheck.Core.Models;

namespace NetCheck.Infrastructure.Diagnostics;

public sealed class WebConnectivityDiagnosticCheck : DiagnosticCheckBase
{
    private const int MaximumResponseCharacters = 1024;

    public override string Id => DiagnosticCheckIds.WebConnectivity;

    public override string Name => "Web connectivity";

    public override string Description => "Checks web access and detects common network sign-in pages.";

    public override DiagnosticCategory Category => DiagnosticCategory.Internet;

    public override int Order => 60;

    public override async Task<DiagnosticCheckResult> ExecuteAsync(
        DiagnosticContext context,
        CancellationToken cancellationToken)
    {
        if (PreviousCheckFailed(context, DiagnosticCheckIds.Adapter)
            || PreviousCheckFailed(context, DiagnosticCheckIds.IpConfiguration))
        {
            return Skip("Skipped because the local network configuration is not usable.");
        }

        var uri = context.Options.ConnectivityCheckUri;
        if (uri.Scheme is not ("http" or "https"))
        {
            return Result(
                CheckStatus.Warning,
                FindingSeverity.Warning,
                "The configured connectivity URL is not valid.",
                recommendations: ["Choose an HTTP or HTTPS connectivity URL in Settings."]);
        }

        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UseProxy = true
        };
        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(Math.Clamp(context.Options.HttpTimeoutSeconds, 2, 30))
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NetCheck/1.3");

        try
        {
            using var response = await client.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (IsRedirect(response.StatusCode))
            {
                var location = response.Headers.Location?.ToString() ?? "Not supplied";
                return CaptivePortal(
                    uri,
                    $"The network redirected the request to {location}.",
                    response.StatusCode.ToString());
            }

            var content = await ReadLimitedContentAsync(response, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode
                && string.Equals(
                    content.Trim(),
                    context.Options.ConnectivityExpectedContent.Trim(),
                    StringComparison.Ordinal))
            {
                context.Set("web-connectivity-confirmed", true);
                return Result(
                    CheckStatus.Passed,
                    FindingSeverity.Information,
                    "Web access is working without redirection.",
                    "The connectivity endpoint returned the expected response.",
                    new Dictionary<string, string>
                    {
                        ["Endpoint"] = uri.ToString(),
                        ["HTTP status"] = $"{(int)response.StatusCode} {response.StatusCode}"
                    });
            }

            if (response.IsSuccessStatusCode)
            {
                return CaptivePortal(
                    uri,
                    "The endpoint returned different content than expected.",
                    $"{(int)response.StatusCode} {response.StatusCode}");
            }

            return Failed(
                uri,
                $"The server returned HTTP {(int)response.StatusCode} ({response.StatusCode}).");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return Failed(uri, exception.Message);
        }
    }

    private DiagnosticCheckResult CaptivePortal(Uri uri, string detail, string status) => Result(
        CheckStatus.Warning,
        FindingSeverity.Warning,
        "A network sign-in page or web interception was detected.",
        detail,
        new Dictionary<string, string>
        {
            ["Issue type"] = "Captive portal",
            ["Endpoint"] = uri.ToString(),
            ["Response"] = status
        },
        [
            "Open a web browser and complete the Wi-Fi or network sign-in page.",
            "After signing in, run the diagnostic again.",
            "On a trusted network without a sign-in page, review proxy, VPN, and security software settings."
        ]);

    private DiagnosticCheckResult Failed(Uri uri, string error) => Result(
        CheckStatus.Failed,
        FindingSeverity.Critical,
        "The web connectivity test failed.",
        "NetCheck could not retrieve the configured connectivity endpoint.",
        new Dictionary<string, string>
        {
            ["Endpoint"] = uri.ToString(),
            ["Error"] = error
        },
        [
            "Verify proxy, VPN, firewall, and security software settings.",
            "Try opening a known website in a browser.",
            "If direct internet checks also fail, restart the router or contact the network provider."
        ]);

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        (int)statusCode is >= 300 and <= 399;

    private static async Task<string> ReadLimitedContentAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        var buffer = new char[MaximumResponseCharacters];
        var count = await reader.ReadBlockAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
            .ConfigureAwait(false);
        return new string(buffer, 0, count);
    }
}

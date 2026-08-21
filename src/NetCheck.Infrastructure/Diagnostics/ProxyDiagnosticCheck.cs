using Microsoft.Win32;
using NetCheck.Core.Diagnostics;
using NetCheck.Core.Models;

namespace NetCheck.Infrastructure.Diagnostics;

public sealed class ProxyDiagnosticCheck : DiagnosticCheckBase
{
    private const string InternetSettingsPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    public override string Id => DiagnosticCheckIds.Proxy;

    public override string Name => "Proxy configuration";

    public override string Description => "Reviews the current user’s Windows proxy settings.";

    public override DiagnosticCategory Category => DiagnosticCategory.SystemConfiguration;

    public override int Order => 80;

    public override Task<DiagnosticCheckResult> ExecuteAsync(
        DiagnosticContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var key = Registry.CurrentUser.OpenSubKey(InternetSettingsPath, writable: false);
        var proxyEnabled = Convert.ToInt32(key?.GetValue("ProxyEnable", 0)) != 0;
        var proxyServer = key?.GetValue("ProxyServer") as string;
        var autoConfigUrl = key?.GetValue("AutoConfigURL") as string;
        var evidence = new Dictionary<string, string>
        {
            ["Manual proxy"] = proxyEnabled ? "Enabled" : "Disabled",
            ["Proxy server"] = string.IsNullOrWhiteSpace(proxyServer) ? "Not configured" : proxyServer,
            ["Automatic configuration"] = string.IsNullOrWhiteSpace(autoConfigUrl) ? "Not configured" : autoConfigUrl
        };

        var webFailed = context.TryGet<DiagnosticCheckResult>(
                $"result:{DiagnosticCheckIds.WebConnectivity}",
                out var webResult)
            && webResult?.Status == CheckStatus.Failed;
        var hasProxyConfiguration = proxyEnabled || !string.IsNullOrWhiteSpace(autoConfigUrl);

        if (hasProxyConfiguration && webFailed)
        {
            return Task.FromResult(Result(
                CheckStatus.Warning,
                FindingSeverity.Warning,
                "A proxy is configured and the web connectivity check failed.",
                "The proxy may be required by your organization, or it may be outdated or unavailable.",
                evidence,
                [
                    "Confirm the proxy address with your network administrator.",
                    "Do not disable an organization-managed proxy without approval.",
                    "If this is a personal computer and the proxy is unexpected, review Windows proxy settings."
                ]));
        }

        return Task.FromResult(Result(
            CheckStatus.Passed,
            FindingSeverity.Information,
            hasProxyConfiguration ? "The configured proxy did not prevent the connectivity test." : "No explicit user proxy is enabled.",
            hasProxyConfiguration
                ? "A proxy configuration is present, and web access was not attributed to it."
                : "Windows is using direct or automatically discovered network settings.",
            evidence));
    }
}


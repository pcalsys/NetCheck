using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;

namespace NetCheck.Core.Diagnostics;

public sealed class DiagnosisAnalyzer : IDiagnosisAnalyzer
{
    public Diagnosis Analyze(IReadOnlyList<DiagnosticCheckResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var byId = results.ToDictionary(result => result.CheckId, StringComparer.OrdinalIgnoreCase);
        var adapter = Find(byId, DiagnosticCheckIds.Adapter);
        var ip = Find(byId, DiagnosticCheckIds.IpConfiguration);
        var gateway = Find(byId, DiagnosticCheckIds.Gateway);
        var dns = Find(byId, DiagnosticCheckIds.Dns);
        var internet = Find(byId, DiagnosticCheckIds.Internet);
        var web = Find(byId, DiagnosticCheckIds.WebConnectivity);
        var stability = Find(byId, DiagnosticCheckIds.Stability);

        if (IsFailure(adapter))
        {
            return Problem(
                "No active network connection",
                "Windows cannot see an active Ethernet or Wi-Fi adapter. The problem is between this computer and the local network.",
                adapter);
        }

        if (IsFailure(ip))
        {
            return Problem(
                "The computer has no valid IP configuration",
                "The network adapter is connected, but it did not receive a usable address from the network.",
                ip);
        }

        if (IsFailure(gateway))
        {
            return Problem(
                "The local gateway is unreachable",
                "The computer has an IP address but cannot communicate with the router or default gateway.",
                gateway);
        }

        if (IsFailure(dns) && IsSuccessful(internet))
        {
            return Problem(
                "DNS is preventing internet access",
                "The internet is reachable by IP address, but domain names cannot be resolved.",
                dns);
        }

        if (IsCaptivePortal(web))
        {
            return Attention(
                "A sign-in page may be blocking access",
                "The network redirected the connectivity test or returned unexpected content, which commonly indicates a captive portal.",
                web);
        }

        if (IsFailure(internet) && IsFailure(web))
        {
            return Problem(
                "The internet connection is unavailable",
                "The local network is reachable, but both direct internet and web connectivity checks failed.",
                internet,
                web);
        }

        if (IsFailure(web) && IsSuccessful(internet))
        {
            return Problem(
                "Web traffic is being blocked",
                "The internet responds to direct network traffic, but the HTTP connectivity check failed. A proxy, firewall, VPN, or upstream policy may be responsible.",
                web);
        }

        if (IsFailure(dns))
        {
            return Problem(
                "Domain name resolution failed",
                "NetCheck could not resolve a known internet hostname. The configured DNS service may be unavailable.",
                dns);
        }

        if (stability?.Status is CheckStatus.Warning or CheckStatus.Failed)
        {
            return Attention(
                "The connection appears unstable",
                "Internet access is available, but packet loss or high latency was detected.",
                stability);
        }

        var warnings = results.Where(result => result.Status == CheckStatus.Warning).ToArray();
        if (warnings.Length > 0)
        {
            return Attention(
                "Internet access works, with some warnings",
                warnings[0].Summary,
                warnings);
        }

        var failures = results.Where(result => result.Status == CheckStatus.Failed).ToArray();
        if (failures.Length > 0)
        {
            return Problem(
                "A network problem was detected",
                failures[0].Summary,
                failures);
        }

        return new Diagnosis
        {
            Outcome = DiagnosticOutcome.Healthy,
            Headline = "Your internet connection looks healthy",
            Summary = "The adapter, local network, DNS, internet access, and connection quality checks completed successfully.",
            RecommendedActions = Array.Empty<string>()
        };
    }

    private static DiagnosticCheckResult? Find(
        IReadOnlyDictionary<string, DiagnosticCheckResult> results,
        string id) => results.TryGetValue(id, out var value) ? value : null;

    private static bool IsFailure(DiagnosticCheckResult? result) =>
        result?.Status == CheckStatus.Failed;

    private static bool IsSuccessful(DiagnosticCheckResult? result) =>
        result?.Status == CheckStatus.Passed;

    private static bool IsCaptivePortal(DiagnosticCheckResult? result) =>
        result is not null
        && result.Evidence.TryGetValue("Issue type", out var issueType)
        && string.Equals(issueType, "Captive portal", StringComparison.OrdinalIgnoreCase);

    private static Diagnosis Problem(
        string headline,
        string summary,
        params DiagnosticCheckResult?[] sources) => Create(
            DiagnosticOutcome.Problem,
            headline,
            summary,
            sources);

    private static Diagnosis Attention(
        string headline,
        string summary,
        params DiagnosticCheckResult?[] sources) => Create(
            DiagnosticOutcome.Attention,
            headline,
            summary,
            sources);

    private static Diagnosis Create(
        DiagnosticOutcome outcome,
        string headline,
        string summary,
        IEnumerable<DiagnosticCheckResult?> sources)
    {
        var actions = sources
            .Where(source => source is not null)
            .SelectMany(source => source!.Recommendations)
            .Where(action => !string.IsNullOrWhiteSpace(action))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();

        return new Diagnosis
        {
            Outcome = outcome,
            Headline = headline,
            Summary = summary,
            RecommendedActions = actions
        };
    }
}


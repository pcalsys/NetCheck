using NetCheck.Core.Diagnostics;
using NetCheck.Core.Models;

namespace NetCheck.Core.Tests;

public sealed class DiagnosisAnalyzerTests
{
    private readonly DiagnosisAnalyzer _analyzer = new();

    [Fact]
    public void Analyze_WhenAdapterFails_IdentifiesLocalConnectionProblem()
    {
        var results = new[]
        {
            Result(DiagnosticCheckIds.Adapter, CheckStatus.Failed, "No adapter", ["Connect Wi-Fi."]),
            Result(DiagnosticCheckIds.IpConfiguration, CheckStatus.Skipped, "Skipped")
        };

        var diagnosis = _analyzer.Analyze(results);

        Assert.Equal(DiagnosticOutcome.Problem, diagnosis.Outcome);
        Assert.Equal("No active network connection", diagnosis.Headline);
        Assert.Contains("Connect Wi-Fi.", diagnosis.RecommendedActions);
    }

    [Fact]
    public void Analyze_WhenDnsFailsButPublicIpWorks_IdentifiesDns()
    {
        var results = Baseline()
            .Append(Result(DiagnosticCheckIds.Dns, CheckStatus.Failed, "DNS failed", ["Check DNS."]))
            .Append(Result(DiagnosticCheckIds.Internet, CheckStatus.Passed, "Internet works"))
            .ToArray();

        var diagnosis = _analyzer.Analyze(results);

        Assert.Equal(DiagnosticOutcome.Problem, diagnosis.Outcome);
        Assert.Equal("DNS is preventing internet access", diagnosis.Headline);
    }

    [Fact]
    public void Analyze_WhenConnectivityEndpointIsIntercepted_IdentifiesCaptivePortal()
    {
        var web = Result(DiagnosticCheckIds.WebConnectivity, CheckStatus.Warning, "Unexpected content") with
        {
            Evidence = new Dictionary<string, string> { ["Issue type"] = "Captive portal" },
            Recommendations = ["Sign in."]
        };
        var results = Baseline()
            .Append(Result(DiagnosticCheckIds.Dns, CheckStatus.Passed, "DNS works"))
            .Append(Result(DiagnosticCheckIds.Internet, CheckStatus.Passed, "Internet works"))
            .Append(web)
            .ToArray();

        var diagnosis = _analyzer.Analyze(results);

        Assert.Equal(DiagnosticOutcome.Attention, diagnosis.Outcome);
        Assert.Equal("A sign-in page may be blocking access", diagnosis.Headline);
    }

    [Fact]
    public void Analyze_WhenStabilityWarns_IdentifiesUnstableConnection()
    {
        var results = Baseline()
            .Append(Result(DiagnosticCheckIds.Dns, CheckStatus.Passed, "DNS works"))
            .Append(Result(DiagnosticCheckIds.Internet, CheckStatus.Passed, "Internet works"))
            .Append(Result(DiagnosticCheckIds.WebConnectivity, CheckStatus.Passed, "Web works"))
            .Append(Result(DiagnosticCheckIds.Stability, CheckStatus.Warning, "Packet loss", ["Try Ethernet."]))
            .ToArray();

        var diagnosis = _analyzer.Analyze(results);

        Assert.Equal(DiagnosticOutcome.Attention, diagnosis.Outcome);
        Assert.Equal("The connection appears unstable", diagnosis.Headline);
    }

    [Fact]
    public void Analyze_WhenAllChecksPass_ReturnsHealthyDiagnosis()
    {
        var results = new[]
        {
            Result(DiagnosticCheckIds.Adapter, CheckStatus.Passed, "Connected"),
            Result(DiagnosticCheckIds.IpConfiguration, CheckStatus.Passed, "Valid IP"),
            Result(DiagnosticCheckIds.Gateway, CheckStatus.Passed, "Gateway works"),
            Result(DiagnosticCheckIds.Dns, CheckStatus.Passed, "DNS works"),
            Result(DiagnosticCheckIds.Internet, CheckStatus.Passed, "Internet works"),
            Result(DiagnosticCheckIds.WebConnectivity, CheckStatus.Passed, "Web works"),
            Result(DiagnosticCheckIds.Stability, CheckStatus.Passed, "Stable"),
            Result(DiagnosticCheckIds.Proxy, CheckStatus.Passed, "No proxy")
        };

        var diagnosis = _analyzer.Analyze(results);

        Assert.Equal(DiagnosticOutcome.Healthy, diagnosis.Outcome);
        Assert.Equal("Your internet connection looks healthy", diagnosis.Headline);
        Assert.Empty(diagnosis.RecommendedActions);
    }

    private static IEnumerable<DiagnosticCheckResult> Baseline()
    {
        yield return Result(DiagnosticCheckIds.Adapter, CheckStatus.Passed, "Connected");
        yield return Result(DiagnosticCheckIds.IpConfiguration, CheckStatus.Passed, "Valid IP");
        yield return Result(DiagnosticCheckIds.Gateway, CheckStatus.Passed, "Gateway works");
    }

    private static DiagnosticCheckResult Result(
        string id,
        CheckStatus status,
        string summary,
        IReadOnlyList<string>? recommendations = null) => new()
        {
            CheckId = id,
            Title = id,
            Category = DiagnosticCategory.Internet,
            Status = status,
            Severity = status == CheckStatus.Failed ? FindingSeverity.Critical : FindingSeverity.Information,
            Summary = summary,
            Recommendations = recommendations ?? Array.Empty<string>()
        };
}

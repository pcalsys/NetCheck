using NetCheck.Core.Diagnostics;
using NetCheck.Core.Models;

namespace NetCheck.Core.Tests;

public sealed class NetworkRepairPlannerTests
{
    private readonly NetworkRepairPlanner _planner = new();

    [Fact]
    public void CreatePlan_WhenConnectionIsHealthy_ReturnsNoActions()
    {
        var plan = _planner.CreatePlan(CreateReport(DiagnosticOutcome.Healthy));

        Assert.False(plan.CanExecute);
        Assert.Empty(plan.Actions);
    }

    [Fact]
    public void CreatePlan_WhenDhcpAddressFails_RenewsAddressAndClearsDns()
    {
        var report = CreateReport(
            DiagnosticOutcome.Problem,
            overrides: new Dictionary<string, DiagnosticCheckResult>
            {
                [DiagnosticCheckIds.IpConfiguration] = Result(
                    DiagnosticCheckIds.IpConfiguration,
                    CheckStatus.Failed)
            });

        var plan = _planner.CreatePlan(report);

        Assert.Equal(
            new[] { NetworkRepairActionId.FlushDnsCache, NetworkRepairActionId.RenewDhcpLease },
            plan.Actions.Select(action => action.Id));
        Assert.True(plan.RequiresElevation);
        Assert.False(plan.RequiresRestart);
    }

    [Fact]
    public void CreatePlan_WhenAddressIsManual_DoesNotOverwriteIt()
    {
        var report = CreateReport(
            DiagnosticOutcome.Problem,
            dhcpEnabled: false,
            overrides: new Dictionary<string, DiagnosticCheckResult>
            {
                [DiagnosticCheckIds.IpConfiguration] = Result(
                    DiagnosticCheckIds.IpConfiguration,
                    CheckStatus.Failed)
            });

        var plan = _planner.CreatePlan(report);

        Assert.Empty(plan.Actions);
        Assert.Contains(plan.ManualGuidance, guidance => guidance.Contains("manual", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreatePlan_WhenDnsFails_OnlyClearsDnsCache()
    {
        var report = CreateReport(
            DiagnosticOutcome.Problem,
            overrides: new Dictionary<string, DiagnosticCheckResult>
            {
                [DiagnosticCheckIds.Dns] = Result(DiagnosticCheckIds.Dns, CheckStatus.Failed)
            });

        var action = Assert.Single(_planner.CreatePlan(report).Actions);

        Assert.Equal(NetworkRepairActionId.FlushDnsCache, action.Id);
    }

    [Fact]
    public void CreatePlan_WhenDirectAndWebAccessFail_ResetsNetworkStack()
    {
        var report = CreateReport(
            DiagnosticOutcome.Problem,
            overrides: new Dictionary<string, DiagnosticCheckResult>
            {
                [DiagnosticCheckIds.Internet] = Result(DiagnosticCheckIds.Internet, CheckStatus.Failed),
                [DiagnosticCheckIds.WebConnectivity] = Result(DiagnosticCheckIds.WebConnectivity, CheckStatus.Failed)
            });

        var plan = _planner.CreatePlan(report);

        Assert.Equal(
            new[]
            {
                NetworkRepairActionId.FlushDnsCache,
                NetworkRepairActionId.ClearArpCache,
                NetworkRepairActionId.ResetWinsockCatalog,
                NetworkRepairActionId.ResetTcpIpStack
            },
            plan.Actions.Select(action => action.Id));
        Assert.True(plan.RequiresRestart);
    }

    [Fact]
    public void CreatePlan_WhenProxyCorrelatesWithWebFailure_OffersExplicitProxyReset()
    {
        var report = CreateReport(
            DiagnosticOutcome.Problem,
            overrides: new Dictionary<string, DiagnosticCheckResult>
            {
                [DiagnosticCheckIds.WebConnectivity] = Result(DiagnosticCheckIds.WebConnectivity, CheckStatus.Failed),
                [DiagnosticCheckIds.Proxy] = Result(DiagnosticCheckIds.Proxy, CheckStatus.Warning)
            });

        var plan = _planner.CreatePlan(report);

        Assert.Contains(plan.Actions, action => action.Id == NetworkRepairActionId.ResetUserProxy);
    }

    [Fact]
    public void CreatePlan_WhenGatewayOnlyBlocksPing_DoesNotChangeTheNetwork()
    {
        var report = CreateReport(
            DiagnosticOutcome.Attention,
            overrides: new Dictionary<string, DiagnosticCheckResult>
            {
                [DiagnosticCheckIds.Gateway] = Result(DiagnosticCheckIds.Gateway, CheckStatus.Warning)
            });

        var plan = _planner.CreatePlan(report);

        Assert.Empty(plan.Actions);
    }

    [Fact]
    public void CreatePlan_WhenCaptivePortalIsDetected_RequiresManualSignIn()
    {
        var captivePortal = Result(DiagnosticCheckIds.WebConnectivity, CheckStatus.Warning) with
        {
            Evidence = new Dictionary<string, string> { ["Issue type"] = "Captive portal" }
        };
        var report = CreateReport(
            DiagnosticOutcome.Attention,
            overrides: new Dictionary<string, DiagnosticCheckResult>
            {
                [DiagnosticCheckIds.WebConnectivity] = captivePortal
            });

        var plan = _planner.CreatePlan(report);

        Assert.Empty(plan.Actions);
        Assert.Contains(plan.ManualGuidance, guidance => guidance.Contains("sign-in", StringComparison.OrdinalIgnoreCase));
    }

    private static DiagnosticReport CreateReport(
        DiagnosticOutcome outcome,
        bool dhcpEnabled = true,
        IReadOnlyDictionary<string, DiagnosticCheckResult>? overrides = null)
    {
        var ids = new[]
        {
            DiagnosticCheckIds.Adapter,
            DiagnosticCheckIds.IpConfiguration,
            DiagnosticCheckIds.Gateway,
            DiagnosticCheckIds.Dns,
            DiagnosticCheckIds.Internet,
            DiagnosticCheckIds.WebConnectivity,
            DiagnosticCheckIds.Stability,
            DiagnosticCheckIds.Proxy
        };
        var checks = ids
            .Select(id => overrides is not null && overrides.TryGetValue(id, out var result)
                ? result
                : Result(id, CheckStatus.Passed))
            .ToArray();
        var adapter = new NetworkAdapterSnapshot
        {
            Id = "adapter-1",
            Name = "Ethernet",
            OperationalStatus = "Up",
            IsDhcpEnabled = dhcpEnabled,
            IpAddresses = ["192.168.1.20"],
            Gateways = ["192.168.1.1"],
            DnsServers = ["192.168.1.1"]
        };
        return new DiagnosticReport
        {
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
            CompletedAtUtc = DateTimeOffset.UtcNow,
            Network = new NetworkSnapshot
            {
                NetworkAvailable = true,
                PrimaryAdapter = adapter,
                Adapters = [adapter]
            },
            Checks = checks,
            Diagnosis = new Diagnosis
            {
                Outcome = outcome,
                Headline = "Test diagnosis",
                Summary = "Test summary"
            }
        };
    }

    private static DiagnosticCheckResult Result(string id, CheckStatus status) => new()
    {
        CheckId = id,
        Title = id,
        Category = DiagnosticCategory.Internet,
        Status = status,
        Severity = status == CheckStatus.Failed ? FindingSeverity.Critical : FindingSeverity.Information,
        Summary = "Test result"
    };
}

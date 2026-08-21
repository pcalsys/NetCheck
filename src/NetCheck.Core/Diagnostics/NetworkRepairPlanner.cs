using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;

namespace NetCheck.Core.Diagnostics;

public sealed class NetworkRepairPlanner : INetworkRepairPlanner
{
    public NetworkRepairPlan CreatePlan(DiagnosticReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (report.Diagnosis.Outcome is DiagnosticOutcome.Healthy or DiagnosticOutcome.Cancelled)
        {
            return NetworkRepairPlan.Empty;
        }

        var actionIds = new HashSet<NetworkRepairActionId>();
        var manualGuidance = new List<string>();
        var adapter = Find(report, DiagnosticCheckIds.Adapter);
        var ip = Find(report, DiagnosticCheckIds.IpConfiguration);
        var gateway = Find(report, DiagnosticCheckIds.Gateway);
        var dns = Find(report, DiagnosticCheckIds.Dns);
        var internet = Find(report, DiagnosticCheckIds.Internet);
        var web = Find(report, DiagnosticCheckIds.WebConnectivity);
        var stability = Find(report, DiagnosticCheckIds.Stability);
        var proxy = Find(report, DiagnosticCheckIds.Proxy);
        var usesDhcp = report.Network.PrimaryAdapter?.IsDhcpEnabled == true;

        if (adapter?.Status == CheckStatus.Failed)
        {
            manualGuidance.Add("Turn on Wi-Fi, reconnect Ethernet, or enable the adapter in Windows before trying again.");
        }

        if (ip?.Status == CheckStatus.Failed)
        {
            if (usesDhcp)
            {
                actionIds.Add(NetworkRepairActionId.RenewDhcpLease);
                actionIds.Add(NetworkRepairActionId.FlushDnsCache);
            }
            else
            {
                manualGuidance.Add("The adapter uses manual or system-managed addressing. Review its IP, subnet, gateway, and DNS settings.");
            }
        }

        if (gateway?.Status == CheckStatus.Failed && usesDhcp)
        {
            actionIds.Add(NetworkRepairActionId.ClearArpCache);
            actionIds.Add(NetworkRepairActionId.RenewDhcpLease);
        }

        if (dns?.Status == CheckStatus.Failed)
        {
            actionIds.Add(NetworkRepairActionId.FlushDnsCache);
        }

        if (proxy?.Status == CheckStatus.Warning)
        {
            actionIds.Add(NetworkRepairActionId.ResetUserProxy);
        }

        var localConfigurationWorks = adapter?.Status == CheckStatus.Passed
            && ip?.Status == CheckStatus.Passed;
        var directAndWebFailed = internet?.Status == CheckStatus.Failed
            && web?.Status == CheckStatus.Failed;
        if (localConfigurationWorks && directAndWebFailed)
        {
            actionIds.Add(NetworkRepairActionId.ClearArpCache);
            actionIds.Add(NetworkRepairActionId.FlushDnsCache);
            actionIds.Add(NetworkRepairActionId.ResetWinsockCatalog);
            actionIds.Add(NetworkRepairActionId.ResetTcpIpStack);
        }
        else if (localConfigurationWorks
                 && internet?.Status == CheckStatus.Passed
                 && web?.Status == CheckStatus.Failed
                 && proxy?.Status != CheckStatus.Warning)
        {
            actionIds.Add(NetworkRepairActionId.ResetWinsockCatalog);
        }

        if (IsCaptivePortal(web))
        {
            manualGuidance.Add("Open a browser and complete the network sign-in page, then run NetCheck again.");
        }

        if (stability?.Status == CheckStatus.Warning)
        {
            manualGuidance.Add("Connection quality problems need a signal, cable, router, or provider check and cannot be repaired safely by software.");
        }

        var actions = actionIds
            .OrderBy(GetExecutionOrder)
            .Select(NetworkRepairActions.Get)
            .ToArray();
        return new NetworkRepairPlan
        {
            Actions = actions,
            ManualGuidance = manualGuidance.Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    private static DiagnosticCheckResult? Find(DiagnosticReport report, string checkId) =>
        report.Checks.FirstOrDefault(
            result => string.Equals(result.CheckId, checkId, StringComparison.OrdinalIgnoreCase));

    private static bool IsCaptivePortal(DiagnosticCheckResult? result) =>
        result?.Status == CheckStatus.Warning
        && result.Evidence.TryGetValue("Issue type", out var issueType)
        && string.Equals(issueType, "Captive portal", StringComparison.OrdinalIgnoreCase);

    private static int GetExecutionOrder(NetworkRepairActionId id) => id switch
    {
        NetworkRepairActionId.ResetUserProxy => 10,
        NetworkRepairActionId.FlushDnsCache => 20,
        NetworkRepairActionId.ClearArpCache => 30,
        NetworkRepairActionId.RenewDhcpLease => 40,
        NetworkRepairActionId.ResetWinsockCatalog => 50,
        NetworkRepairActionId.ResetTcpIpStack => 60,
        _ => 100
    };
}

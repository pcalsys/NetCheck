using NetCheck.Core.Diagnostics;
using NetCheck.Core.Models;

namespace NetCheck.Infrastructure.Diagnostics;

public sealed class AdapterDiagnosticCheck : DiagnosticCheckBase
{
    public override string Id => DiagnosticCheckIds.Adapter;

    public override string Name => "Network adapter";

    public override string Description => "Checks whether Windows has an active Ethernet or Wi-Fi connection.";

    public override DiagnosticCategory Category => DiagnosticCategory.Hardware;

    public override int Order => 10;

    public override Task<DiagnosticCheckResult> ExecuteAsync(
        DiagnosticContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var active = context.Network.Adapters
            .Where(adapter => string.Equals(adapter.OperationalStatus, "Up", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (!context.Network.NetworkAvailable || active.Length == 0 || context.Network.PrimaryAdapter is null)
        {
            return Task.FromResult(Result(
                CheckStatus.Failed,
                FindingSeverity.Critical,
                "No active Ethernet or Wi-Fi adapter was detected.",
                "Windows reports that no usable network interface is connected.",
                new Dictionary<string, string>
                {
                    ["Adapters found"] = context.Network.Adapters.Count.ToString(),
                    ["Active adapters"] = active.Length.ToString()
                },
                [
                    "Turn on Wi-Fi or reconnect the Ethernet cable.",
                    "Disable Airplane mode.",
                    "Open Windows Network Connections and make sure the adapter is enabled.",
                    "If the adapter is missing, reinstall or update its driver in Device Manager."
                ]));
        }

        var adapter = context.Network.PrimaryAdapter;
        return Task.FromResult(Result(
            CheckStatus.Passed,
            FindingSeverity.Information,
            $"{adapter.Name} is connected.",
            "Windows reports an operational network link.",
            new Dictionary<string, string>
            {
                ["Adapter"] = adapter.Name,
                ["Type"] = adapter.InterfaceType,
                ["Status"] = adapter.OperationalStatus,
                ["Link speed"] = FormatSpeed(adapter.LinkSpeedBitsPerSecond)
            }));
    }

    private static string FormatSpeed(long bitsPerSecond)
    {
        if (bitsPerSecond <= 0)
        {
            return "Unknown";
        }

        return bitsPerSecond >= 1_000_000_000
            ? $"{bitsPerSecond / 1_000_000_000d:0.##} Gbps"
            : $"{bitsPerSecond / 1_000_000d:0.##} Mbps";
    }
}


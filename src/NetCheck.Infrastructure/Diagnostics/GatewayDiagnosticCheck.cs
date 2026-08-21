using System.Net;
using System.Net.NetworkInformation;
using NetCheck.Core.Diagnostics;
using NetCheck.Core.Models;

namespace NetCheck.Infrastructure.Diagnostics;

public sealed class GatewayDiagnosticCheck : DiagnosticCheckBase
{
    public override string Id => DiagnosticCheckIds.Gateway;

    public override string Name => "Default gateway";

    public override string Description => "Checks the path from this computer to the local router.";

    public override DiagnosticCategory Category => DiagnosticCategory.LocalNetwork;

    public override int Order => 30;

    public override async Task<DiagnosticCheckResult> ExecuteAsync(
        DiagnosticContext context,
        CancellationToken cancellationToken)
    {
        if (PreviousCheckFailed(context, DiagnosticCheckIds.Adapter)
            || PreviousCheckFailed(context, DiagnosticCheckIds.IpConfiguration)
            || context.Network.PrimaryAdapter is null)
        {
            return Skip("Skipped because the local network configuration is not usable.");
        }

        var gatewayTexts = context.Network.PrimaryAdapter.Gateways;
        var gateways = gatewayTexts
            .Select(value => IPAddress.TryParse(value, out var parsed) ? parsed : null)
            .Where(value => value is not null)
            .Cast<IPAddress>()
            .ToArray();

        if (gateways.Length == 0)
        {
            return Result(
                CheckStatus.Failed,
                FindingSeverity.Critical,
                "No default gateway is configured.",
                "Without a default gateway, the computer normally cannot reach the internet.",
                new Dictionary<string, string> { ["Default gateway"] = "Not configured" },
                [
                    "Renew the DHCP lease or verify the manually configured default gateway.",
                    "Compare the IP settings with another working device on the same network."
                ]);
        }

        var attempts = new List<string>();
        foreach (var gateway in gateways)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reply = await SendPingSafelyAsync(
                gateway,
                context.Options.PingTimeoutMilliseconds,
                cancellationToken).ConfigureAwait(false);

            attempts.Add($"{gateway}: {(reply is null ? "No reply" : $"{reply.RoundtripTime} ms")}");
            if (reply is not null)
            {
                context.Set("responsive-gateway", gateway);
                return Result(
                    CheckStatus.Passed,
                    FindingSeverity.Information,
                    $"The gateway {gateway} responded in {reply.RoundtripTime} ms.",
                    "Communication with the local router is working.",
                    new Dictionary<string, string>
                    {
                        ["Gateway"] = gateway.ToString(),
                        ["Round-trip time"] = $"{reply.RoundtripTime} ms"
                    });
            }
        }

        return Result(
            CheckStatus.Warning,
            FindingSeverity.Warning,
            "The default gateway did not answer ping requests.",
            "Some routers block ping, so NetCheck will continue with direct internet checks before drawing a conclusion.",
            new Dictionary<string, string> { ["Attempts"] = string.Join("; ", attempts) },
            [
                "Check the Wi-Fi signal or Ethernet cable.",
                "Restart the router if internet checks also fail.",
                "Verify that the configured gateway belongs to the adapter’s local subnet."
            ]);
    }

    private static async Task<PingReply?> SendPingSafelyAsync(
        IPAddress address,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(address, timeoutMilliseconds)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return reply.Status == IPStatus.Success ? reply : null;
        }
        catch (PingException)
        {
            return null;
        }
    }
}


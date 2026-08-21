using System.Net;
using System.Net.NetworkInformation;
using NetCheck.Core.Diagnostics;
using NetCheck.Core.Models;

namespace NetCheck.Infrastructure.Diagnostics;

public sealed class InternetReachabilityDiagnosticCheck : DiagnosticCheckBase
{
    public override string Id => DiagnosticCheckIds.Internet;

    public override string Name => "Internet reachability";

    public override string Description => "Tests direct connectivity to reliable public IP addresses.";

    public override DiagnosticCategory Category => DiagnosticCategory.Internet;

    public override int Order => 50;

    public override async Task<DiagnosticCheckResult> ExecuteAsync(
        DiagnosticContext context,
        CancellationToken cancellationToken)
    {
        if (PreviousCheckFailed(context, DiagnosticCheckIds.Adapter)
            || PreviousCheckFailed(context, DiagnosticCheckIds.IpConfiguration))
        {
            return Skip("Skipped because the local network configuration is not usable.");
        }

        var targets = context.Options.InternetPingTargets
            .Select(value => IPAddress.TryParse(value, out var address) ? address : null)
            .Where(address => address is not null)
            .Cast<IPAddress>()
            .Distinct()
            .ToArray();

        if (targets.Length == 0)
        {
            return Result(
                CheckStatus.Warning,
                FindingSeverity.Warning,
                "No valid internet ping target is configured.",
                recommendations: ["Restore the default diagnostic targets in Settings."]);
        }

        var attempts = new List<string>(targets.Length);
        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reply = await SendPingAsync(target, context.Options.PingTimeoutMilliseconds, cancellationToken)
                .ConfigureAwait(false);
            attempts.Add($"{target}: {(reply is null ? "No reply" : $"{reply.RoundtripTime} ms")}");

            if (reply is not null)
            {
                context.Set("internet-ping-target", target);
                return Result(
                    CheckStatus.Passed,
                    FindingSeverity.Information,
                    $"The internet responded in {reply.RoundtripTime} ms.",
                    "A public IP address is reachable without relying on DNS.",
                    new Dictionary<string, string>
                    {
                        ["Target"] = target.ToString(),
                        ["Round-trip time"] = $"{reply.RoundtripTime} ms",
                        ["All attempts"] = string.Join("; ", attempts)
                    });
            }
        }

        return Result(
            CheckStatus.Failed,
            FindingSeverity.Critical,
            "No public ping target responded.",
            "Direct internet reachability could not be confirmed. Some networks block ping, so the web check will provide additional evidence.",
            new Dictionary<string, string> { ["Attempts"] = string.Join("; ", attempts) },
            [
                "Check whether other devices on the same network can reach the internet.",
                "Restart the modem or router if all devices are affected.",
                "Temporarily disconnect a VPN and run the diagnostic again.",
                "Contact the internet provider if the local network works but all internet checks fail."
            ]);
    }

    private static async Task<PingReply?> SendPingAsync(
        IPAddress target,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(target, timeoutMilliseconds)
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


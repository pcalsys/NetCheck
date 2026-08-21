using System.Net;
using System.Net.Sockets;
using NetCheck.Core.Diagnostics;
using NetCheck.Core.Models;

namespace NetCheck.Infrastructure.Diagnostics;

public sealed class DnsDiagnosticCheck : DiagnosticCheckBase
{
    public override string Id => DiagnosticCheckIds.Dns;

    public override string Name => "DNS resolution";

    public override string Description => "Checks whether domain names can be translated into IP addresses.";

    public override DiagnosticCategory Category => DiagnosticCategory.NameResolution;

    public override int Order => 40;

    public override async Task<DiagnosticCheckResult> ExecuteAsync(
        DiagnosticContext context,
        CancellationToken cancellationToken)
    {
        if (PreviousCheckFailed(context, DiagnosticCheckIds.Adapter)
            || PreviousCheckFailed(context, DiagnosticCheckIds.IpConfiguration))
        {
            return Skip("Skipped because the local network configuration is not usable.");
        }

        var host = context.Options.DnsTestHost.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            return Result(
                CheckStatus.Warning,
                FindingSeverity.Warning,
                "No DNS test host is configured.",
                recommendations: ["Choose a valid public hostname in Settings, then run the diagnostic again."]);
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(6), cancellationToken)
                .ConfigureAwait(false);
            var usableAddresses = addresses
                .Where(address => !IPAddress.IsLoopback(address))
                .Select(address => address.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (usableAddresses.Length == 0)
            {
                return Failed(host, "The DNS response contained no usable address.", context);
            }

            context.Set("dns-resolved-addresses", usableAddresses);
            return Result(
                CheckStatus.Passed,
                FindingSeverity.Information,
                $"{host} resolved successfully.",
                "The configured DNS resolver returned one or more addresses.",
                new Dictionary<string, string>
                {
                    ["Test host"] = host,
                    ["Resolved addresses"] = string.Join(", ", usableAddresses.Take(6)),
                    ["Configured DNS"] = context.Network.PrimaryAdapter?.DnsServers.Count > 0
                        ? string.Join(", ", context.Network.PrimaryAdapter.DnsServers)
                        : "System default"
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is SocketException or TimeoutException)
        {
            return Failed(host, exception.Message, context);
        }
    }

    private DiagnosticCheckResult Failed(string host, string error, DiagnosticContext context) => Result(
        CheckStatus.Failed,
        FindingSeverity.Critical,
        $"The hostname {host} could not be resolved.",
        "The DNS server did not return a usable answer within the allowed time.",
        new Dictionary<string, string>
        {
            ["Test host"] = host,
            ["Configured DNS"] = context.Network.PrimaryAdapter?.DnsServers.Count > 0
                ? string.Join(", ", context.Network.PrimaryAdapter.DnsServers)
                : "None detected",
            ["Error"] = error
        },
        [
            "Restart the router and run the diagnostic again.",
            "Verify the DNS addresses in the adapter settings.",
            "Temporarily test a trusted public DNS resolver, if permitted by your organization.",
            "On managed networks, contact the network administrator before changing DNS settings."
        ]);
}


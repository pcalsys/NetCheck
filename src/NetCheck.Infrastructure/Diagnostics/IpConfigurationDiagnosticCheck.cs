using System.Net;
using System.Net.Sockets;
using NetCheck.Core.Diagnostics;
using NetCheck.Core.Models;

namespace NetCheck.Infrastructure.Diagnostics;

public sealed class IpConfigurationDiagnosticCheck : DiagnosticCheckBase
{
    public override string Id => DiagnosticCheckIds.IpConfiguration;

    public override string Name => "IP configuration";

    public override string Description => "Validates the address assigned to the active adapter.";

    public override DiagnosticCategory Category => DiagnosticCategory.LocalConfiguration;

    public override int Order => 20;

    public override Task<DiagnosticCheckResult> ExecuteAsync(
        DiagnosticContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (PreviousCheckFailed(context, DiagnosticCheckIds.Adapter) || context.Network.PrimaryAdapter is null)
        {
            return Task.FromResult(Skip("Skipped because no active network adapter is available."));
        }

        var adapter = context.Network.PrimaryAdapter;
        var parsedAddresses = adapter.IpAddresses
            .Select(address => IPAddress.TryParse(address, out var parsed) ? parsed : null)
            .Where(address => address is not null)
            .Cast<IPAddress>()
            .ToArray();

        var apipa = parsedAddresses.FirstOrDefault(IsAutomaticPrivateAddress);
        if (apipa is not null)
        {
            return Task.FromResult(Result(
                CheckStatus.Failed,
                FindingSeverity.Critical,
                $"Windows assigned the automatic address {apipa}.",
                "An address in 169.254.0.0/16 normally means the computer could not obtain an IPv4 address from DHCP.",
                Evidence(adapter),
                [
                    "Reconnect to the network, then run the diagnostic again.",
                    "Restart the router or DHCP server if other devices are also affected.",
                    "In an elevated Command Prompt, run ‘ipconfig /release’ followed by ‘ipconfig /renew’.",
                    "Verify that the adapter is configured to obtain an IP address automatically."
                ]));
        }

        var usable = parsedAddresses.Any(IsUsableAddress);
        if (!usable)
        {
            return Task.FromResult(Result(
                CheckStatus.Failed,
                FindingSeverity.Critical,
                "The active adapter has no usable IP address.",
                "A valid IPv4 or globally routable IPv6 address is required to reach other networks.",
                Evidence(adapter),
                [
                    "Disconnect and reconnect the network adapter.",
                    "Verify DHCP or the manually configured address, subnet, and gateway.",
                    "Restart the computer and router if the address remains unavailable."
                ]));
        }

        return Task.FromResult(Result(
            CheckStatus.Passed,
            FindingSeverity.Information,
            $"The adapter has a valid address ({context.Network.PrimaryIpAddress}).",
            "The local IP configuration appears usable.",
            Evidence(adapter)));
    }

    private static Dictionary<string, string> Evidence(NetworkAdapterSnapshot adapter) => new()
    {
        ["IP addresses"] = adapter.IpAddresses.Count == 0 ? "None" : string.Join(", ", adapter.IpAddresses),
        ["Address assignment"] = adapter.IsDhcpEnabled ? "DHCP" : "Manual or system-managed",
        ["Default gateways"] = adapter.Gateways.Count == 0 ? "None" : string.Join(", ", adapter.Gateways),
        ["DNS servers"] = adapter.DnsServers.Count == 0 ? "None" : string.Join(", ", adapter.DnsServers)
    };

    private static bool IsAutomaticPrivateAddress(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return address.AddressFamily == AddressFamily.InterNetwork
            && bytes.Length == 4
            && bytes[0] == 169
            && bytes[1] == 254;
    }

    private static bool IsUsableAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        if (IsAutomaticPrivateAddress(address))
        {
            return false;
        }

        return address.AddressFamily != AddressFamily.InterNetworkV6 || !address.IsIPv6LinkLocal;
    }
}


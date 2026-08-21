using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;

namespace NetCheck.Infrastructure.Network;

public sealed class WindowsNetworkSnapshotProvider : INetworkSnapshotProvider
{
    public Task<NetworkSnapshot> CaptureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(IsRelevantAdapter)
            .Select(CaptureAdapterSafely)
            .Where(adapter => adapter is not null)
            .Cast<NetworkAdapterSnapshot>()
            .OrderByDescending(adapter => adapter.OperationalStatus == OperationalStatus.Up.ToString())
            .ThenByDescending(adapter => adapter.Gateways.Count > 0)
            .ThenBy(adapter => adapter.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        var primary = adapters
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up.ToString())
            .OrderByDescending(adapter => adapter.Gateways.Count > 0)
            .ThenByDescending(adapter => adapter.IpAddresses.Any(IsRoutableIpv4Text))
            .FirstOrDefault();

        var properties = IPGlobalProperties.GetIPGlobalProperties();
        var snapshot = new NetworkSnapshot
        {
            MachineName = Environment.MachineName,
            DomainName = properties.DomainName,
            NetworkAvailable = NetworkInterface.GetIsNetworkAvailable(),
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Adapters = adapters,
            PrimaryAdapter = primary
        };

        return Task.FromResult(snapshot);
    }

    private static bool IsRelevantAdapter(NetworkInterface adapter) =>
        adapter.NetworkInterfaceType is not NetworkInterfaceType.Loopback
        and not NetworkInterfaceType.Tunnel
        && !adapter.Description.Contains("Kernel Debug", StringComparison.OrdinalIgnoreCase);

    private static NetworkAdapterSnapshot? CaptureAdapterSafely(NetworkInterface adapter)
    {
        try
        {
            var properties = adapter.GetIPProperties();
            var ipv4Properties = adapter.Supports(NetworkInterfaceComponent.IPv4)
                ? properties.GetIPv4Properties()
                : null;

            return new NetworkAdapterSnapshot
            {
                Id = adapter.Id,
                Name = adapter.Name,
                Description = adapter.Description,
                InterfaceType = adapter.NetworkInterfaceType.ToString(),
                OperationalStatus = adapter.OperationalStatus.ToString(),
                MacAddress = FormatMacAddress(adapter.GetPhysicalAddress()),
                LinkSpeedBitsPerSecond = adapter.Speed,
                SupportsIpv4 = adapter.Supports(NetworkInterfaceComponent.IPv4),
                SupportsIpv6 = adapter.Supports(NetworkInterfaceComponent.IPv6),
                IsDhcpEnabled = ipv4Properties?.IsDhcpEnabled ?? false,
                IpAddresses = properties.UnicastAddresses
                    .Where(address => address.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                    .Select(address => address.Address.ToString())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                Gateways = properties.GatewayAddresses
                    .Select(gateway => gateway.Address.ToString())
                    .Where(address => address is not "0.0.0.0" and not "::")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                DnsServers = properties.DnsAddresses
                    .Select(address => address.ToString())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            };
        }
        catch (NetworkInformationException)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private static string FormatMacAddress(PhysicalAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 0 ? string.Empty : string.Join("-", bytes.Select(value => value.ToString("X2")));
    }

    private static bool IsRoutableIpv4Text(string value) =>
        value.Contains('.', StringComparison.Ordinal)
        && !value.StartsWith("169.254.", StringComparison.Ordinal)
        && value != "0.0.0.0";
}


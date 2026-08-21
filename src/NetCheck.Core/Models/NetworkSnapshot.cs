namespace NetCheck.Core.Models;

public sealed record NetworkSnapshot
{
    public string MachineName { get; init; } = Environment.MachineName;

    public string DomainName { get; init; } = string.Empty;

    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public bool NetworkAvailable { get; init; }

    public IReadOnlyList<NetworkAdapterSnapshot> Adapters { get; init; } =
        Array.Empty<NetworkAdapterSnapshot>();

    public NetworkAdapterSnapshot? PrimaryAdapter { get; init; }

    public string PrimaryIpAddress =>
        PrimaryAdapter?.IpAddresses.FirstOrDefault(address => address.Contains('.', StringComparison.Ordinal))
        ?? PrimaryAdapter?.IpAddresses.FirstOrDefault()
        ?? "Not available";

    public string PrimaryGateway => PrimaryAdapter?.Gateways.FirstOrDefault() ?? "Not available";

    public string PrimaryDnsServer => PrimaryAdapter?.DnsServers.FirstOrDefault() ?? "Not available";
}


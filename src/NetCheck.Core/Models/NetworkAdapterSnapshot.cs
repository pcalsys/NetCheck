namespace NetCheck.Core.Models;

public sealed record NetworkAdapterSnapshot
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    public string InterfaceType { get; init; } = string.Empty;

    public string OperationalStatus { get; init; } = string.Empty;

    public string MacAddress { get; init; } = string.Empty;

    public long LinkSpeedBitsPerSecond { get; init; }

    public bool SupportsIpv4 { get; init; }

    public bool SupportsIpv6 { get; init; }

    public bool IsDhcpEnabled { get; init; }

    public IReadOnlyList<string> IpAddresses { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Gateways { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> DnsServers { get; init; } = Array.Empty<string>();
}


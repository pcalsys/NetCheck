using NetCheck.Core.Models;

namespace NetCheck.Core.Abstractions;

public interface INetworkMonitoringProbe
{
    Task<NetworkEnvironmentSnapshot> CaptureEnvironmentAsync(
        MonitoringOptions options,
        CancellationToken cancellationToken);

    Task<NetworkMonitoringProbeResult> ProbeAsync(
        MonitoringOptions options,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WindowsNetworkEvent>> GetWindowsNetworkEventsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);
}

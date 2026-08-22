using NetCheck.Core.Models;

namespace NetCheck.Core.Abstractions;

public interface IMonitoringService
{
    Task<MonitoringSession> RunAsync(
        MonitoringOptions options,
        IProgress<MonitoringProgress>? progress,
        CancellationToken cancellationToken);
}

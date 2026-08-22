using NetCheck.Core.Models;

namespace NetCheck.Core.Abstractions;

public interface IMonitoringHistoryStore
{
    Task SaveAsync(MonitoringSession session, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MonitoringSession>> GetRecentAsync(
        int maximumCount = 100,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

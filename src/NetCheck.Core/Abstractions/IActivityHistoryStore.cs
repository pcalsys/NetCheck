using NetCheck.Core.Models;

namespace NetCheck.Core.Abstractions;

public interface IActivityHistoryStore
{
    Task SaveAsync(ActivityHistoryEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActivityHistoryEntry>> GetRecentAsync(
        int maximumCount,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

using NetCheck.Core.Models;

namespace NetCheck.Core.Abstractions;

public interface IReportHistoryStore
{
    Task SaveAsync(DiagnosticReport report, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DiagnosticReport>> GetRecentAsync(
        int maximumCount,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}


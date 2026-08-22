using NetCheck.Core.Models;

namespace NetCheck.Core.Abstractions;

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default);
}

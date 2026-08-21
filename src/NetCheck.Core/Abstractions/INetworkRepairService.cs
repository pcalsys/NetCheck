using NetCheck.Core.Models;

namespace NetCheck.Core.Abstractions;

public interface INetworkRepairService
{
    Task<NetworkRepairResult> ExecuteAsync(
        NetworkRepairPlan plan,
        CancellationToken cancellationToken = default);
}

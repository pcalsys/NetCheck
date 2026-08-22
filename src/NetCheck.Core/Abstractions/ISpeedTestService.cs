using NetCheck.Core.Models;

namespace NetCheck.Core.Abstractions;

public interface ISpeedTestService
{
    Task<SpeedTestResult> RunAsync(
        IProgress<SpeedTestProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

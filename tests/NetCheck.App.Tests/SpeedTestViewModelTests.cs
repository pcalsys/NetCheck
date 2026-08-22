using System.IO;
using NetCheck.App.Localization;
using NetCheck.App.ViewModels;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;
using NetCheck.Infrastructure.Logging;

namespace NetCheck.App.Tests;

public sealed class SpeedTestViewModelTests
{
    [Fact]
    public async Task RunCommand_PresentsMaximumAverageLatencyAndDataInGerman()
    {
        var localization = new LocalizationService();
        localization.SetLanguage("de");
        var service = new StubSpeedTestService(new SpeedTestResult(
            14.4,
            91.26,
            103.76,
            31.5,
            37.26,
            10 * 1024 * 1024,
            2 * 1024 * 1024,
            TimeSpan.FromSeconds(7.35),
            "Cloudflare",
            DateTimeOffset.UtcNow));
        var viewModel = new SpeedTestViewModel(
            service,
            localization,
            new FileLogger(Path.Combine(Path.GetTempPath(), $"NetCheck-{Guid.NewGuid():N}.log")));

        await viewModel.RunCommand.ExecuteAsync();

        Assert.True(viewModel.HasResult);
        Assert.Equal("103,8 Mbit/s", viewModel.PeakDownloadText);
        Assert.Equal("91,3 Mbit/s", viewModel.AverageDownloadText);
        Assert.Equal("37,3 Mbit/s", viewModel.PeakUploadText);
        Assert.Equal("14 ms", viewModel.LatencyText);
        Assert.Equal("12,6 MB", viewModel.DataUsedText);
        Assert.Equal("Speedtest abgeschlossen", viewModel.StatusText);
    }

    [Fact]
    public async Task CancelCommand_CancelsAnActiveMeasurementWithoutAResult()
    {
        var localization = new LocalizationService();
        var service = new WaitingSpeedTestService();
        var viewModel = new SpeedTestViewModel(
            service,
            localization,
            new FileLogger(Path.Combine(Path.GetTempPath(), $"NetCheck-{Guid.NewGuid():N}.log")));

        var run = viewModel.RunCommand.ExecuteAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        viewModel.CancelCommand.Execute(null);
        await run;

        Assert.False(viewModel.IsRunning);
        Assert.False(viewModel.HasResult);
        Assert.Equal("Speed test cancelled. No result was saved.", viewModel.StatusText);
    }

    private sealed class StubSpeedTestService(SpeedTestResult result) : ISpeedTestService
    {
        public Task<SpeedTestResult> RunAsync(
            IProgress<SpeedTestProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            progress?.Report(new SpeedTestProgress(SpeedTestPhase.Complete, 100, 0, 0, 0));
            return Task.FromResult(result);
        }
    }

    private sealed class WaitingSpeedTestService : ISpeedTestService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<SpeedTestResult> RunAsync(
            IProgress<SpeedTestProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The cancellation wait unexpectedly completed.");
        }
    }
}

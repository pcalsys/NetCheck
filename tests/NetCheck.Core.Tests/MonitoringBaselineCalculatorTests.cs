using NetCheck.Core.Models;
using NetCheck.Core.Monitoring;

namespace NetCheck.Core.Tests;

public sealed class MonitoringBaselineCalculatorTests
{
    [Fact]
    public void Compare_WhenCurrentQualityImproves_ReturnsBetterTrend()
    {
        var previous = Session(latency: 100, loss: 4, availability: 96);
        var current = Session(latency: 70, loss: 1, availability: 99.5);

        var result = MonitoringBaselineCalculator.Compare(current, [previous]);

        Assert.Equal(BaselineTrend.Better, result.Trend);
        Assert.Equal(1, result.ComparedSessionCount);
        Assert.Equal(-30, result.LatencyDifferencePercent, precision: 5);
        Assert.Equal(-3, result.PacketLossDifferencePercentagePoints, precision: 5);
    }

    [Fact]
    public void Compare_IgnoresOtherProfilesAndReturnsNoBaseline()
    {
        var current = Session(30, 0, 100, MonitoringProfile.Gaming);
        var previous = Session(30, 0, 100, MonitoringProfile.Streaming);

        var result = MonitoringBaselineCalculator.Compare(current, [previous]);

        Assert.Equal(BaselineTrend.NoBaseline, result.Trend);
        Assert.Equal(0, result.ComparedSessionCount);
    }

    private static MonitoringSession Session(
        double latency,
        double loss,
        double availability,
        MonitoringProfile profile = MonitoringProfile.Standard) => new()
        {
            Profile = profile,
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            CompletedAtUtc = DateTimeOffset.UtcNow,
            Summary = new MonitoringSummary
            {
                TotalSamples = 10,
                AverageLatencyMilliseconds = latency,
                PacketLossPercent = loss,
                AvailabilityPercent = availability
            }
        };
}

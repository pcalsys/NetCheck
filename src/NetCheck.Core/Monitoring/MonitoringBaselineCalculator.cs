using NetCheck.Core.Models;

namespace NetCheck.Core.Monitoring;

public static class MonitoringBaselineCalculator
{
    private const int MaximumBaselineSessions = 10;

    public static BaselineComparison Compare(
        MonitoringSession current,
        IEnumerable<MonitoringSession> previousSessions)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(previousSessions);

        var baseline = previousSessions
            .Where(session => session.Id != current.Id)
            .Where(session => session.Profile == current.Profile)
            .Where(session => session.Summary.TotalSamples > 0)
            .OrderByDescending(session => session.CompletedAtUtc)
            .Take(MaximumBaselineSessions)
            .ToArray();
        if (baseline.Length == 0)
        {
            return new BaselineComparison();
        }

        var averageLatency = baseline.Average(session => session.Summary.AverageLatencyMilliseconds);
        var averageLoss = baseline.Average(session => session.Summary.PacketLossPercent);
        var averageAvailability = baseline.Average(session => session.Summary.AvailabilityPercent);
        var latencyDifference = PercentageDifference(
            current.Summary.AverageLatencyMilliseconds,
            averageLatency);
        var lossDifference = current.Summary.PacketLossPercent - averageLoss;
        var availabilityDifference = current.Summary.AvailabilityPercent - averageAvailability;

        var improvementScore = 0;
        improvementScore += latencyDifference <= -10 ? 1 : latencyDifference >= 10 ? -1 : 0;
        improvementScore += lossDifference <= -1 ? 1 : lossDifference >= 1 ? -1 : 0;
        improvementScore += availabilityDifference >= 0.5 ? 1 : availabilityDifference <= -0.5 ? -1 : 0;

        return new BaselineComparison
        {
            Trend = improvementScore switch
            {
                > 0 => BaselineTrend.Better,
                < 0 => BaselineTrend.Worse,
                _ => BaselineTrend.Similar
            },
            ComparedSessionCount = baseline.Length,
            LatencyDifferencePercent = latencyDifference,
            PacketLossDifferencePercentagePoints = lossDifference,
            AvailabilityDifferencePercentagePoints = availabilityDifference
        };
    }

    private static double PercentageDifference(double current, double baseline)
    {
        if (baseline <= double.Epsilon)
        {
            return current <= double.Epsilon ? 0 : 100;
        }

        return ((current - baseline) / baseline) * 100;
    }
}

namespace NetCheck.Core.Models;

public sealed record SpeedTestResult(
    double LatencyMilliseconds,
    double DownloadMegabitsPerSecond,
    double PeakDownloadMegabitsPerSecond,
    double UploadMegabitsPerSecond,
    double PeakUploadMegabitsPerSecond,
    long DownloadBytes,
    long UploadBytes,
    TimeSpan Duration,
    string Provider,
    DateTimeOffset CompletedAtUtc);

namespace NetCheck.Infrastructure.Network;

public sealed record CloudflareSpeedTestOptions
{
    public Uri DownloadEndpoint { get; init; } = new("https://speed.cloudflare.com/__down");

    public Uri UploadEndpoint { get; init; } = new("https://speed.cloudflare.com/__up");

    public int LatencySampleCount { get; init; } = 5;

    public int DownloadProbeBytes { get; init; } = 1_000_000;

    public long MinimumDownloadBytes { get; init; } = 512 * 1024;

    public long MaximumDownloadBytes { get; init; } = 150_000_000;

    public TimeSpan DownloadTargetDuration { get; init; } = TimeSpan.FromSeconds(17);

    public int DownloadParallelism { get; init; } = 4;

    public int DownloadRoundCount { get; init; } = 5;

    public int UploadProbeBytes { get; init; } = 500_000;

    public long MinimumUploadBytes { get; init; } = 256 * 1024;

    public long MaximumUploadBytes { get; init; } = 44_000_000;

    public TimeSpan UploadTargetDuration { get; init; } = TimeSpan.FromSeconds(11);

    public int UploadParallelism { get; init; } = 3;

    public int UploadRoundCount { get; init; } = 4;

    public TimeSpan SampleInterval { get; init; } = TimeSpan.FromMilliseconds(250);

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(60);
}

namespace NetCheck.Core.Models;

public sealed record UpdateCheckResult
{
    public required Version CurrentVersion { get; init; }

    public required Version LatestVersion { get; init; }

    public bool UpdateAvailable { get; init; }

    public required Uri ReleasePageUri { get; init; }

    public Uri? PackageUri { get; init; }

    public Uri? ChecksumUri { get; init; }

    public bool HasVerifiedReleaseAssets => PackageUri is not null && ChecksumUri is not null;

    public DateTimeOffset? PublishedAtUtc { get; init; }
}

namespace NetCheck.Core.Models;

public sealed record ActivityHistoryEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public ActivityHistoryKind Kind { get; init; }

    public SpeedTestResult? SpeedTestResult { get; init; }

    public IReadOnlyList<SettingChange> SettingChanges { get; init; } =
        Array.Empty<SettingChange>();
}

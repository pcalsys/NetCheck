namespace NetCheck.Core.Models;

public sealed record DiagnosticCheckResult
{
    public required string CheckId { get; init; }

    public required string Title { get; init; }

    public required DiagnosticCategory Category { get; init; }

    public required CheckStatus Status { get; init; }

    public required FindingSeverity Severity { get; init; }

    public required string Summary { get; init; }

    public string Detail { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Evidence { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();

    public TimeSpan Duration { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public static DiagnosticCheckResult Skipped(
        string checkId,
        string title,
        DiagnosticCategory category,
        string reason) => new()
        {
            CheckId = checkId,
            Title = title,
            Category = category,
            Status = CheckStatus.Skipped,
            Severity = FindingSeverity.Information,
            Summary = reason
        };
}


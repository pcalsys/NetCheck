namespace NetCheck.Core.Models;

public sealed record Diagnosis
{
    public required DiagnosticOutcome Outcome { get; init; }

    public required string Headline { get; init; }

    public required string Summary { get; init; }

    public IReadOnlyList<string> RecommendedActions { get; init; } = Array.Empty<string>();
}


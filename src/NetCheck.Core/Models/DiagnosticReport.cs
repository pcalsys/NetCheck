namespace NetCheck.Core.Models;

public sealed record DiagnosticReport
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; }

    public TimeSpan Duration => CompletedAtUtc - StartedAtUtc;

    public required Diagnosis Diagnosis { get; init; }

    public required NetworkSnapshot Network { get; init; }

    public IReadOnlyList<DiagnosticCheckResult> Checks { get; init; } =
        Array.Empty<DiagnosticCheckResult>();

    public string ApplicationVersion { get; init; } = "1.3.0";
}

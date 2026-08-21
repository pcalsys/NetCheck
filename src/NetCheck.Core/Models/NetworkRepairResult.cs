namespace NetCheck.Core.Models;

public sealed record NetworkRepairResult
{
    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset CompletedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public bool Cancelled { get; init; }

    public IReadOnlyList<NetworkRepairStepResult> Steps { get; init; } =
        Array.Empty<NetworkRepairStepResult>();

    public bool Succeeded => !Cancelled && Steps.Count > 0 && Steps.All(step => step.Succeeded);

    public bool HasAppliedChanges => Steps.Any(step => step.Succeeded);

    public bool RequiresRestart => Steps.Any(step => step.Succeeded && step.RequiresRestart);
}

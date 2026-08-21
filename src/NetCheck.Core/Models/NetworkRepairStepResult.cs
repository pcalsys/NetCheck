namespace NetCheck.Core.Models;

public sealed record NetworkRepairStepResult
{
    public required NetworkRepairActionId ActionId { get; init; }

    public required string Title { get; init; }

    public bool Succeeded { get; init; }

    public bool RequiresRestart { get; init; }

    public required string Summary { get; init; }

    public string Detail { get; init; } = string.Empty;
}

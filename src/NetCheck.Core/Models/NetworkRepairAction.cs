namespace NetCheck.Core.Models;

public sealed record NetworkRepairAction
{
    public required NetworkRepairActionId Id { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public bool RequiresElevation { get; init; }

    public bool RequiresRestart { get; init; }
}

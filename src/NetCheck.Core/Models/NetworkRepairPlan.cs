namespace NetCheck.Core.Models;

public sealed record NetworkRepairPlan
{
    public static NetworkRepairPlan Empty { get; } = new();

    public IReadOnlyList<NetworkRepairAction> Actions { get; init; } =
        Array.Empty<NetworkRepairAction>();

    public IReadOnlyList<string> ManualGuidance { get; init; } = Array.Empty<string>();

    public bool CanExecute => Actions.Count > 0;

    public bool RequiresElevation => Actions.Any(action => action.RequiresElevation);

    public bool RequiresRestart => Actions.Any(action => action.RequiresRestart);
}

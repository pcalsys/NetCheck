namespace NetCheck.Core.Models;

public sealed record DiagnosticProgress(
    int CompletedChecks,
    int TotalChecks,
    string CurrentCheckId,
    string CurrentCheckName,
    CheckStatus Status,
    DiagnosticCheckResult? Result = null)
{
    public int Percentage => TotalChecks == 0
        ? 0
        : (int)Math.Round(CompletedChecks * 100d / TotalChecks, MidpointRounding.AwayFromZero);
}


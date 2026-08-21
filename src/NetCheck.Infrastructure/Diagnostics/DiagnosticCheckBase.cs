using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;

namespace NetCheck.Infrastructure.Diagnostics;

public abstract class DiagnosticCheckBase : IDiagnosticCheck
{
    public abstract string Id { get; }

    public abstract string Name { get; }

    public abstract string Description { get; }

    public abstract DiagnosticCategory Category { get; }

    public abstract int Order { get; }

    public abstract Task<DiagnosticCheckResult> ExecuteAsync(
        DiagnosticContext context,
        CancellationToken cancellationToken);

    protected DiagnosticCheckResult Result(
        CheckStatus status,
        FindingSeverity severity,
        string summary,
        string detail = "",
        IReadOnlyDictionary<string, string>? evidence = null,
        IReadOnlyList<string>? recommendations = null) => new()
        {
            CheckId = Id,
            Title = Name,
            Category = Category,
            Status = status,
            Severity = severity,
            Summary = summary,
            Detail = detail,
            Evidence = evidence ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Recommendations = recommendations ?? Array.Empty<string>()
        };

    protected DiagnosticCheckResult Skip(string reason) =>
        DiagnosticCheckResult.Skipped(Id, Name, Category, reason);

    protected static bool PreviousCheckFailed(DiagnosticContext context, string checkId) =>
        context.TryGet<DiagnosticCheckResult>($"result:{checkId}", out var result)
        && result?.Status == CheckStatus.Failed;
}

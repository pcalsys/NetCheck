using NetCheck.Core.Models;

namespace NetCheck.Core.Abstractions;

public interface IDiagnosticCheck
{
    string Id { get; }

    string Name { get; }

    string Description { get; }

    DiagnosticCategory Category { get; }

    int Order { get; }

    Task<DiagnosticCheckResult> ExecuteAsync(
        DiagnosticContext context,
        CancellationToken cancellationToken);
}


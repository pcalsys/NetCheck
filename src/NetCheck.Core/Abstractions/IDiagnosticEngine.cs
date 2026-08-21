using NetCheck.Core.Models;

namespace NetCheck.Core.Abstractions;

public interface IDiagnosticEngine
{
    Task<DiagnosticReport> RunAsync(
        DiagnosticOptions options,
        IProgress<DiagnosticProgress>? progress = null,
        CancellationToken cancellationToken = default);
}


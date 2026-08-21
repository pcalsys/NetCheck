using NetCheck.Core.Models;

namespace NetCheck.Core.Abstractions;

public interface IReportExporter
{
    Task ExportAsync(
        DiagnosticReport report,
        string filePath,
        bool includeComputerName,
        CancellationToken cancellationToken = default);
}


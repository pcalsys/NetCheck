using NetCheck.Core.Models;

namespace NetCheck.Core.Abstractions;

public interface IDiagnosisAnalyzer
{
    Diagnosis Analyze(IReadOnlyList<DiagnosticCheckResult> results);
}


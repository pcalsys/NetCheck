using System.Diagnostics;
using System.Reflection;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;

namespace NetCheck.Core.Diagnostics;

public sealed class DiagnosticEngine : IDiagnosticEngine
{
    private readonly IReadOnlyList<IDiagnosticCheck> _checks;
    private readonly INetworkSnapshotProvider _networkSnapshotProvider;
    private readonly IDiagnosisAnalyzer _analyzer;

    public DiagnosticEngine(
        IEnumerable<IDiagnosticCheck> checks,
        INetworkSnapshotProvider networkSnapshotProvider,
        IDiagnosisAnalyzer analyzer)
    {
        ArgumentNullException.ThrowIfNull(checks);
        _networkSnapshotProvider = networkSnapshotProvider
            ?? throw new ArgumentNullException(nameof(networkSnapshotProvider));
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));

        _checks = checks.OrderBy(check => check.Order).ToArray();
        if (_checks.Count == 0)
        {
            throw new ArgumentException("At least one diagnostic check is required.", nameof(checks));
        }

        var duplicate = _checks
            .GroupBy(check => check.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Diagnostic check identifiers must be unique. Duplicate: {duplicate.Key}",
                nameof(checks));
        }
    }

    public async Task<DiagnosticReport> RunAsync(
        DiagnosticOptions options,
        IProgress<DiagnosticProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var startedAt = DateTimeOffset.UtcNow;
        var snapshot = await CaptureSnapshotSafelyAsync(cancellationToken).ConfigureAwait(false);
        var context = new DiagnosticContext(snapshot, options);
        var results = new List<DiagnosticCheckResult>(_checks.Count);

        for (var index = 0; index < _checks.Count; index++)
        {
            var check = _checks[index];
            progress?.Report(new DiagnosticProgress(
                index,
                _checks.Count,
                check.Id,
                check.Name,
                CheckStatus.Running));

            var stopwatch = Stopwatch.StartNew();
            DiagnosticCheckResult result;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                result = await check.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return CreateCancelledReport(startedAt, snapshot, results);
            }
            catch (Exception exception)
            {
                result = new DiagnosticCheckResult
                {
                    CheckId = check.Id,
                    Title = check.Name,
                    Category = check.Category,
                    Status = CheckStatus.Warning,
                    Severity = FindingSeverity.Warning,
                    Summary = "This check could not be completed.",
                    Detail = "NetCheck handled an unexpected error and continued with the remaining checks.",
                    Evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Error type"] = exception.GetType().Name,
                        ["Error message"] = exception.Message
                    },
                    Recommendations =
                    [
                        "Run the diagnostic again.",
                        "If the issue persists, export the report for technical support."
                    ]
                };
            }
            finally
            {
                stopwatch.Stop();
            }

            result = result with
            {
                CheckId = check.Id,
                Title = check.Name,
                Category = check.Category,
                Duration = stopwatch.Elapsed,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };

            results.Add(result);
            context.Set($"result:{check.Id}", result);

            progress?.Report(new DiagnosticProgress(
                index + 1,
                _checks.Count,
                check.Id,
                check.Name,
                result.Status,
                result));
        }

        return CreateReport(startedAt, snapshot, results, _analyzer.Analyze(results));
    }

    private async Task<NetworkSnapshot> CaptureSnapshotSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _networkSnapshotProvider.CaptureAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new NetworkSnapshot { NetworkAvailable = false };
        }
    }

    private static DiagnosticReport CreateReport(
        DateTimeOffset startedAt,
        NetworkSnapshot snapshot,
        IReadOnlyList<DiagnosticCheckResult> results,
        Diagnosis diagnosis) => new()
    {
        StartedAtUtc = startedAt,
        CompletedAtUtc = DateTimeOffset.UtcNow,
        Network = snapshot,
        Checks = results,
        Diagnosis = diagnosis,
        ApplicationVersion = GetApplicationVersion()
    };

    private static DiagnosticReport CreateCancelledReport(
        DateTimeOffset startedAt,
        NetworkSnapshot snapshot,
        IReadOnlyList<DiagnosticCheckResult> results) => CreateReport(
            startedAt,
            snapshot,
            results,
            new Diagnosis
            {
                Outcome = DiagnosticOutcome.Cancelled,
                Headline = "Diagnostic cancelled",
                Summary = "The diagnostic was stopped before every check completed.",
                RecommendedActions = ["Run a new diagnostic when you are ready."]
            });

    private static string GetApplicationVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";
}


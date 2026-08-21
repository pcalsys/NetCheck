using NetCheck.Core.Abstractions;
using NetCheck.Core.Diagnostics;
using NetCheck.Core.Models;

namespace NetCheck.Core.Tests;

public sealed class DiagnosticEngineTests
{
    [Fact]
    public async Task RunAsync_OrdersChecksAndReportsProgress()
    {
        var checks = new IDiagnosticCheck[]
        {
            new StubCheck("second", 20),
            new StubCheck("first", 10)
        };
        var progressEvents = new List<DiagnosticProgress>();
        var progress = new SynchronousProgress<DiagnosticProgress>(progressEvents.Add);
        var engine = CreateEngine(checks);

        var report = await engine.RunAsync(new DiagnosticOptions(), progress);

        Assert.Equal(new[] { "first", "second" }, report.Checks.Select(check => check.CheckId));
        Assert.Equal(4, progressEvents.Count);
        Assert.Equal(100, progressEvents[^1].Percentage);
    }

    [Fact]
    public async Task RunAsync_WhenOneCheckThrows_ContinuesAndRecordsHandledWarning()
    {
        var engine = CreateEngine(
        [
            new StubCheck("broken", 10, new InvalidOperationException("Simulated failure")),
            new StubCheck("working", 20)
        ]);

        var report = await engine.RunAsync(new DiagnosticOptions());

        Assert.Equal(2, report.Checks.Count);
        Assert.Equal(CheckStatus.Warning, report.Checks[0].Status);
        Assert.Equal("InvalidOperationException", report.Checks[0].Evidence["Error type"]);
        Assert.Equal(CheckStatus.Passed, report.Checks[1].Status);
    }

    [Fact]
    public async Task Constructor_WhenCheckIdsAreDuplicated_Throws()
    {
        var checks = new IDiagnosticCheck[]
        {
            new StubCheck("same", 10),
            new StubCheck("same", 20)
        };

        var exception = Assert.Throws<ArgumentException>(() => CreateEngine(checks));

        Assert.Contains("unique", exception.Message, StringComparison.OrdinalIgnoreCase);
        await Task.CompletedTask;
    }

    private static DiagnosticEngine CreateEngine(IEnumerable<IDiagnosticCheck> checks) => new(
        checks,
        new StubSnapshotProvider(),
        new DiagnosisAnalyzer());

    private sealed class StubSnapshotProvider : INetworkSnapshotProvider
    {
        public Task<NetworkSnapshot> CaptureAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new NetworkSnapshot { NetworkAvailable = true });
    }

    private sealed class StubCheck : IDiagnosticCheck
    {
        private readonly Exception? _exception;

        public StubCheck(string id, int order, Exception? exception = null)
        {
            Id = id;
            Order = order;
            _exception = exception;
        }

        public string Id { get; }

        public string Name => Id;

        public string Description => "Test check";

        public DiagnosticCategory Category => DiagnosticCategory.Internet;

        public int Order { get; }

        public Task<DiagnosticCheckResult> ExecuteAsync(
            DiagnosticContext context,
            CancellationToken cancellationToken)
        {
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(new DiagnosticCheckResult
            {
                CheckId = Id,
                Title = Name,
                Category = Category,
                Status = CheckStatus.Passed,
                Severity = FindingSeverity.Information,
                Summary = "Passed"
            });
        }
    }

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}


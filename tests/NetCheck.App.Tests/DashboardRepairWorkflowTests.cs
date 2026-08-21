using System.IO;
using NetCheck.App.Localization;
using NetCheck.App.Services;
using NetCheck.App.ViewModels;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Diagnostics;
using NetCheck.Core.Models;
using NetCheck.Infrastructure.Logging;

namespace NetCheck.App.Tests;

public sealed class DashboardRepairWorkflowTests
{
    [Fact]
    public async Task FixCommand_WhenRepairSucceeds_ConfirmsAppliesAndVerifiesAgain()
    {
        var diagnosticEngine = new SequenceDiagnosticEngine(
            CreateDnsFailureReport(),
            CreateHealthyReport());
        var repairService = new StubRepairService();
        var messages = new StubMessageService(confirmResult: true);
        var viewModel = CreateViewModel(diagnosticEngine, repairService, messages);
        await viewModel.InitializeAsync();

        await viewModel.FixCommand.ExecuteAsync();

        Assert.Equal(2, diagnosticEngine.RunCount);
        Assert.Equal(1, repairService.ExecutionCount);
        Assert.Contains("Clear DNS cache", messages.LastConfirmation, StringComparison.Ordinal);
        Assert.True(viewModel.HasRepairResult);
        Assert.True(viewModel.RepairResult?.Succeeded);
        Assert.Equal(DiagnosticOutcome.Healthy, viewModel.Report?.Diagnosis.Outcome);
    }

    [Fact]
    public async Task FixCommand_WhenConfirmationIsDeclined_MakesNoChanges()
    {
        var diagnosticEngine = new SequenceDiagnosticEngine(CreateDnsFailureReport());
        var repairService = new StubRepairService();
        var messages = new StubMessageService(confirmResult: false);
        var viewModel = CreateViewModel(diagnosticEngine, repairService, messages);
        await viewModel.InitializeAsync();

        await viewModel.FixCommand.ExecuteAsync();

        Assert.Equal(1, diagnosticEngine.RunCount);
        Assert.Equal(0, repairService.ExecutionCount);
        Assert.False(viewModel.HasRepairResult);
    }

    [Fact]
    public async Task FixCommand_WhenRepairRequiresRestart_DoesNotClaimImmediateVerification()
    {
        var diagnosticEngine = new SequenceDiagnosticEngine(CreateInternetFailureReport());
        var repairService = new StubRepairService();
        var messages = new StubMessageService(confirmResult: true);
        var viewModel = CreateViewModel(diagnosticEngine, repairService, messages);
        await viewModel.InitializeAsync();

        await viewModel.FixCommand.ExecuteAsync();

        Assert.Equal(1, diagnosticEngine.RunCount);
        Assert.True(viewModel.RepairResult?.RequiresRestart);
        Assert.Contains("Restart Windows", viewModel.RepairResultSummary, StringComparison.Ordinal);
    }

    private static DashboardViewModel CreateViewModel(
        IDiagnosticEngine diagnosticEngine,
        INetworkRepairService repairService,
        IMessageService messageService)
    {
        var text = new LocalizationService();
        return new DashboardViewModel(
            diagnosticEngine,
            new StubHistoryStore(),
            new StubReportExporter(),
            new StubSettingsStore(),
            new NetworkRepairPlanner(),
            repairService,
            new StubFileDialogService(),
            messageService,
            text,
            new ReportLocalizationService(text),
            new FileLogger(Path.Combine(Path.GetTempPath(), "NetCheck.Tests", "dashboard-repair.log")));
    }

    private static DiagnosticReport CreateDnsFailureReport()
    {
        var report = CreateHealthyReport();
        var checks = report.Checks
            .Select(check => check.CheckId == DiagnosticCheckIds.Dns
                ? check with { Status = CheckStatus.Failed, Summary = "DNS failed" }
                : check)
            .ToArray();
        return report with
        {
            Checks = checks,
            Diagnosis = report.Diagnosis with
            {
                Outcome = DiagnosticOutcome.Problem,
                Headline = "DNS is preventing internet access"
            }
        };
    }

    private static DiagnosticReport CreateInternetFailureReport()
    {
        var report = CreateHealthyReport();
        var checks = report.Checks
            .Select(check => check.CheckId is DiagnosticCheckIds.Internet or DiagnosticCheckIds.WebConnectivity
                ? check with { Status = CheckStatus.Failed, Summary = "Internet failed" }
                : check)
            .ToArray();
        return report with
        {
            Checks = checks,
            Diagnosis = report.Diagnosis with
            {
                Outcome = DiagnosticOutcome.Problem,
                Headline = "Internet access is unavailable"
            }
        };
    }

    private static DiagnosticReport CreateHealthyReport()
    {
        var adapter = new NetworkAdapterSnapshot
        {
            Id = "adapter-1",
            Name = "Ethernet",
            OperationalStatus = "Up",
            IsDhcpEnabled = true,
            IpAddresses = ["192.168.1.20"],
            Gateways = ["192.168.1.1"],
            DnsServers = ["192.168.1.1"]
        };
        var checks = new[]
        {
            DiagnosticCheckIds.Adapter,
            DiagnosticCheckIds.IpConfiguration,
            DiagnosticCheckIds.Gateway,
            DiagnosticCheckIds.Dns,
            DiagnosticCheckIds.Internet,
            DiagnosticCheckIds.WebConnectivity,
            DiagnosticCheckIds.Stability,
            DiagnosticCheckIds.Proxy
        }.Select(id => new DiagnosticCheckResult
        {
            CheckId = id,
            Title = id,
            Category = DiagnosticCategory.Internet,
            Status = CheckStatus.Passed,
            Severity = FindingSeverity.Information,
            Summary = "Passed"
        }).ToArray();
        return new DiagnosticReport
        {
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
            CompletedAtUtc = DateTimeOffset.UtcNow,
            Network = new NetworkSnapshot
            {
                NetworkAvailable = true,
                PrimaryAdapter = adapter,
                Adapters = [adapter]
            },
            Checks = checks,
            Diagnosis = new Diagnosis
            {
                Outcome = DiagnosticOutcome.Healthy,
                Headline = "Your internet connection looks healthy",
                Summary = "All checks passed."
            }
        };
    }

    private sealed class SequenceDiagnosticEngine(params DiagnosticReport[] reports) : IDiagnosticEngine
    {
        public int RunCount { get; private set; }

        public Task<DiagnosticReport> RunAsync(
            DiagnosticOptions options,
            IProgress<DiagnosticProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var index = Math.Min(RunCount, reports.Length - 1);
            RunCount++;
            return Task.FromResult(reports[index]);
        }
    }

    private sealed class StubRepairService : INetworkRepairService
    {
        public int ExecutionCount { get; private set; }

        public Task<NetworkRepairResult> ExecuteAsync(
            NetworkRepairPlan plan,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return Task.FromResult(new NetworkRepairResult
            {
                StartedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                Steps = plan.Actions.Select(action => new NetworkRepairStepResult
                {
                    ActionId = action.Id,
                    Title = action.Title,
                    Succeeded = true,
                    RequiresRestart = action.RequiresRestart,
                    Summary = "Applied"
                }).ToArray()
            });
        }
    }

    private sealed class StubSettingsStore : ISettingsStore
    {
        private readonly DiagnosticOptions _settings = new()
        {
            AutoRunOnLaunch = true,
            SaveDiagnosticHistory = false
        };

        public Task<DiagnosticOptions> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_settings);

        public Task SaveAsync(DiagnosticOptions settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubHistoryStore : IReportHistoryStore
    {
        public Task SaveAsync(DiagnosticReport report, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<DiagnosticReport>> GetRecentAsync(
            int maximumCount,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DiagnosticReport>>(Array.Empty<DiagnosticReport>());

        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubReportExporter : IReportExporter
    {
        public Task ExportAsync(
            DiagnosticReport report,
            string filePath,
            bool includeComputerName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubFileDialogService : IFileDialogService
    {
        public string? ShowReportSaveDialog(string suggestedFileName) => null;
    }

    private sealed class StubMessageService(bool confirmResult) : IMessageService
    {
        public string LastConfirmation { get; private set; } = string.Empty;

        public void ShowError(string title, string message)
        {
        }

        public void ShowInformation(string title, string message)
        {
        }

        public bool Confirm(string title, string message)
        {
            LastConfirmation = message;
            return confirmResult;
        }
    }
}

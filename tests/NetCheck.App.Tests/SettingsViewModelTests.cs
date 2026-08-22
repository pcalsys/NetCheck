using System.IO;
using NetCheck.App.Localization;
using NetCheck.App.Services;
using NetCheck.App.ViewModels;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;
using NetCheck.Infrastructure.Logging;

namespace NetCheck.App.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task SaveCommand_RecordsOnlyChangedSettings()
    {
        var settingsStore = new InMemorySettingsStore(new DiagnosticOptions());
        var historyStore = new CollectingActivityHistoryStore();
        var viewModel = new SettingsViewModel(
            settingsStore,
            historyStore,
            new StubMessageService(),
            new LocalizationService(),
            new FileLogger(Path.Combine(Path.GetTempPath(), $"NetCheck-{Guid.NewGuid():N}.log")));
        await viewModel.LoadAsync();

        viewModel.PingTimeoutMilliseconds = 2200;
        viewModel.AutoRunOnLaunch = false;
        await viewModel.SaveCommand.ExecuteAsync();

        var entry = Assert.Single(historyStore.Entries);
        Assert.Equal(ActivityHistoryKind.SettingsChanged, entry.Kind);
        Assert.Collection(
            entry.SettingChanges,
            change =>
            {
                Assert.Equal(nameof(DiagnosticOptions.PingTimeoutMilliseconds), change.SettingName);
                Assert.Equal("1500", change.PreviousValue);
                Assert.Equal("2200", change.NewValue);
            },
            change =>
            {
                Assert.Equal(nameof(DiagnosticOptions.AutoRunOnLaunch), change.SettingName);
                Assert.Equal("true", change.PreviousValue);
                Assert.Equal("false", change.NewValue);
            });

        await viewModel.SaveCommand.ExecuteAsync();
        Assert.Single(historyStore.Entries);
    }

    private sealed class InMemorySettingsStore(DiagnosticOptions settings) : ISettingsStore
    {
        private DiagnosticOptions _settings = settings;

        public Task<DiagnosticOptions> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_settings);

        public Task SaveAsync(
            DiagnosticOptions settings,
            CancellationToken cancellationToken = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class CollectingActivityHistoryStore : IActivityHistoryStore
    {
        public List<ActivityHistoryEntry> Entries { get; } = [];

        public Task SaveAsync(
            ActivityHistoryEntry entry,
            CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ActivityHistoryEntry>> GetRecentAsync(
            int maximumCount,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ActivityHistoryEntry>>(Entries.Take(maximumCount).ToArray());

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            Entries.Clear();
            return Task.CompletedTask;
        }
    }

    private sealed class StubMessageService : IMessageService
    {
        public void ShowError(string title, string message)
        {
        }

        public void ShowInformation(string title, string message)
        {
        }

        public bool Confirm(string title, string message) => true;
    }
}

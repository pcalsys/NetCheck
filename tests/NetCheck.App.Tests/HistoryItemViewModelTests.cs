using NetCheck.App.Localization;
using NetCheck.App.ViewModels;
using NetCheck.Core.Models;

namespace NetCheck.App.Tests;

public sealed class HistoryItemViewModelTests
{
    [Fact]
    public void LanguageChange_IsPresentedWithLocalizedOldAndNewValues()
    {
        var text = new LocalizationService();
        text.SetLanguage("de");
        var item = HistoryItemViewModel.FromActivity(new ActivityHistoryEntry
        {
            Kind = ActivityHistoryKind.LanguageChanged,
            SettingChanges =
            [
                new SettingChange(nameof(DiagnosticOptions.MenuLanguage), "en", "de")
            ]
        }, text);

        Assert.Equal("Sprache geändert", item.Title);
        Assert.Equal("Englisch wurde zu Deutsch geändert", item.Summary);
        var change = Assert.Single(item.ChangedSettings);
        Assert.Equal("Menüsprache", change.SettingLabel);
        Assert.Equal("Englisch", change.PreviousValue);
        Assert.Equal("Deutsch", change.NewValue);
    }

    [Fact]
    public void SpeedTest_ExposesCompleteMeasurementForHistoryDetails()
    {
        var result = new SpeedTestResult(
            18.2,
            100.5,
            112.8,
            40.3,
            45.7,
            150_000_000,
            44_000_000,
            TimeSpan.FromSeconds(29.6),
            "Cloudflare",
            DateTimeOffset.UtcNow);
        var item = HistoryItemViewModel.FromActivity(new ActivityHistoryEntry
        {
            Kind = ActivityHistoryKind.SpeedTest,
            SpeedTestResult = result,
            OccurredAtUtc = result.CompletedAtUtc
        }, new LocalizationService());

        Assert.True(item.IsSpeedTest);
        Assert.Equal("100.5 Mbit/s", item.AverageDownloadText);
        Assert.Equal("45.7 Mbit/s", item.PeakUploadText);
        Assert.Equal("18 ms", item.LatencyText);
        Assert.Equal("29.6 s", item.DurationText);
        Assert.Equal("194.0 MB", item.DataUsedText);
    }
}

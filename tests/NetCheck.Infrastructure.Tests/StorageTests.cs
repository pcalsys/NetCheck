using NetCheck.Core.Models;
using NetCheck.Infrastructure.Storage;

namespace NetCheck.Infrastructure.Tests;

public sealed class StorageTests
{
    [Fact]
    public async Task HistoryStore_RoundTripsAndClearsReports()
    {
        using var directory = new TemporaryDirectory();
        var paths = new AppDataPaths(directory.Path);
        using var store = new JsonReportHistoryStore(paths);
        var expected = TestReportFactory.Create();

        await store.SaveAsync(expected);
        var reports = await store.GetRecentAsync(10);

        var actual = Assert.Single(reports);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Diagnosis.Headline, actual.Diagnosis.Headline);

        await store.ClearAsync();
        Assert.Empty(await store.GetRecentAsync(10));
    }

    [Fact]
    public async Task SettingsStore_NormalizesUnsafeRangesAndDuplicateTargets()
    {
        using var directory = new TemporaryDirectory();
        var paths = new AppDataPaths(directory.Path);
        using var store = new JsonSettingsStore(paths);
        var settings = new DiagnosticOptions
        {
            DnsTestHost = "  example.com  ",
            InternetPingTargets = ["1.1.1.1", "1.1.1.1", " 8.8.8.8 "],
            PingTimeoutMilliseconds = 100,
            StabilitySampleCount = 100,
            PacketLossWarningPercent = 0,
            LatencyWarningMilliseconds = 9000,
            MenuLanguage = "DE"
        };

        await store.SaveAsync(settings);
        var loaded = await store.LoadAsync();

        Assert.Equal("example.com", loaded.DnsTestHost);
        Assert.Equal(new[] { "1.1.1.1", "8.8.8.8" }, loaded.InternetPingTargets);
        Assert.Equal(500, loaded.PingTimeoutMilliseconds);
        Assert.Equal(20, loaded.StabilitySampleCount);
        Assert.Equal(1, loaded.PacketLossWarningPercent);
        Assert.Equal(2000, loaded.LatencyWarningMilliseconds);
        Assert.Equal("de", loaded.MenuLanguage);
    }

    [Fact]
    public async Task SettingsStore_UsesEnglishForUnsupportedMenuLanguage()
    {
        using var directory = new TemporaryDirectory();
        var paths = new AppDataPaths(directory.Path);
        using var store = new JsonSettingsStore(paths);

        await store.SaveAsync(new DiagnosticOptions { MenuLanguage = "fr" });

        var loaded = await store.LoadAsync();

        Assert.Equal("en", loaded.MenuLanguage);
    }
}

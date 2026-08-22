using NetCheck.Core.Models;
using NetCheck.Infrastructure.Storage;

namespace NetCheck.Infrastructure.Tests;

public sealed class MonitoringHistoryStoreTests
{
    [Fact]
    public async Task Store_RoundTripsSessionsAndSkipsCorruptFiles()
    {
        using var directory = new TemporaryDirectory();
        var paths = new AppDataPaths(directory.Path);
        using var store = new JsonMonitoringHistoryStore(paths);
        var session = new MonitoringSession
        {
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
            CompletedAtUtc = DateTimeOffset.UtcNow,
            Profile = MonitoringProfile.Gaming,
            Samples = [new MonitoringSample { State = ConnectionState.Online }],
            Summary = new MonitoringSummary { TotalSamples = 1, SuccessfulSamples = 1 }
        };

        await store.SaveAsync(session);
        await File.WriteAllTextAsync(
            Path.Combine(paths.MonitoringSessionsDirectory, "broken.json"),
            "{not valid json");

        var loaded = await store.GetRecentAsync(10);

        var actual = Assert.Single(loaded);
        Assert.Equal(session.Id, actual.Id);
        Assert.Equal(MonitoringProfile.Gaming, actual.Profile);
        await store.ClearAsync();
        Assert.Empty(await store.GetRecentAsync());
    }
}

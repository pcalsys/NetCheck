using System.IO.Compression;
using System.Text.Json;
using NetCheck.Infrastructure.Storage;
using NetCheck.Infrastructure.Support;

namespace NetCheck.Infrastructure.Tests;

public sealed class SupportBundleServiceTests
{
    [Fact]
    public async Task CreateAsync_RedactsIdentifiersAndNetworkAddresses()
    {
        using var directory = new TemporaryDirectory();
        var paths = new AppDataPaths(directory.Path);
        Directory.CreateDirectory(paths.MonitoringSessionsDirectory);
        var sensitive = JsonSerializer.Serialize(new
        {
            user = Environment.UserName,
            computer = Environment.MachineName,
            ssid = "Private Wireless",
            adapter = "AA-BB-CC-DD-EE-FF",
            privateIpv4 = "192.168.50.12",
            privateIpv6 = "fd12:3456:789a::1",
            detail = $"Private Wireless belongs to {Environment.UserName}"
        });
        await File.WriteAllTextAsync(
            Path.Combine(paths.MonitoringSessionsDirectory, "session.json"),
            sensitive);
        await File.WriteAllTextAsync(paths.LogFile, sensitive);
        var destination = Path.Combine(directory.Path, "support.zip");
        var service = new SupportBundleService(paths);

        await service.CreateAsync(destination);

        using var archive = ZipFile.OpenRead(destination);
        Assert.NotEmpty(archive.Entries);
        var combined = string.Join('\n', archive.Entries.Select(ReadEntry));
        Assert.DoesNotContain(Environment.UserName, combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.MachineName, combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Private Wireless", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AA-BB-CC-DD-EE-FF", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("192.168.50.12", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("fd12:3456:789a::1", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[redacted", combined, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadEntry(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

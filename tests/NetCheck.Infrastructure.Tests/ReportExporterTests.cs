using System.Text.Json;
using NetCheck.Infrastructure.Export;

namespace NetCheck.Infrastructure.Tests;

public sealed class ReportExporterTests
{
    [Fact]
    public async Task ExportJson_RedactsComputerNameAndMacAddressesByDefault()
    {
        using var directory = new TemporaryDirectory();
        var destination = Path.Combine(directory.Path, "report.json");
        var exporter = new ReportExporter();

        await exporter.ExportAsync(
            TestReportFactory.Create(),
            destination,
            includeComputerName: false);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(destination));
        var network = document.RootElement.GetProperty("network");
        Assert.Equal("Redacted", network.GetProperty("machineName").GetString());
        Assert.Equal(
            "Redacted",
            network.GetProperty("adapters")[0].GetProperty("macAddress").GetString());
        Assert.DoesNotContain("PRIVATE-PC", await File.ReadAllTextAsync(destination));
        Assert.DoesNotContain("AA-BB-CC-DD-EE-FF", await File.ReadAllTextAsync(destination));
    }

    [Fact]
    public async Task ExportHtml_EncodesDiagnosticAndNetworkText()
    {
        using var directory = new TemporaryDirectory();
        var destination = Path.Combine(directory.Path, "report.html");
        var exporter = new ReportExporter();

        await exporter.ExportAsync(
            TestReportFactory.Create(),
            destination,
            includeComputerName: true);

        var html = await File.ReadAllTextAsync(destination);
        Assert.Contains("Connection &lt;healthy&gt;", html);
        Assert.Contains("All checks passed &amp; completed.", html);
        Assert.DoesNotContain("<healthy>", html);
    }
}


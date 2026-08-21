using System.Globalization;
using System.Text.Json;
using NetCheck.Core.Abstractions;
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

    [Fact]
    public async Task ExportHtml_UsesSelectedPresentationLanguage()
    {
        using var directory = new TemporaryDirectory();
        var destination = Path.Combine(directory.Path, "bericht.html");
        var exporter = new ReportExporter(new StubGermanLocalizer());

        await exporter.ExportAsync(
            TestReportFactory.Create(),
            destination,
            includeComputerName: false);

        var html = await File.ReadAllTextAsync(destination);
        Assert.Contains("<html lang=\"de\">", html);
        Assert.Contains("Abgeschlossen", html);
        Assert.Contains("Bericht", html);
    }

    private sealed class StubGermanLocalizer : ITextLocalizer
    {
        public string Language => "de";

        public CultureInfo Culture => CultureInfo.GetCultureInfo("de-DE");

        public string Translate(string source) => source switch
        {
            "Completed" => "Abgeschlossen",
            "Report" => "Bericht",
            "NetCheck report" => "NetCheck-Bericht",
            "Redacted" => "Ausgeblendet",
            _ => source
        };

        public string Format(string sourceFormat, params object?[] arguments) =>
            string.Format(Culture, Translate(sourceFormat), arguments);
    }
}

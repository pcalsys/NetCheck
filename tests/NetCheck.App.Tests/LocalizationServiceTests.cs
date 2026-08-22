using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NetCheck.App.Localization;
using NetCheck.Core.Diagnostics;
using NetCheck.Core.Models;
using NetCheck.Infrastructure.Export;

namespace NetCheck.App.Tests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public void GermanLanguage_LocalizesInterfaceFormatsAndDiagnosticPatterns()
    {
        var text = new LocalizationService();
        text.SetLanguage("de");

        Assert.Equal("Übersicht", text.Translate("Dashboard"));
        Assert.Equal("3 Probleme beheben", text.Format("Fix {0} issues", 3));
        Assert.Equal(
            "www.microsoft.com wurde erfolgreich aufgelöst.",
            text.Translate("www.microsoft.com resolved successfully."));
        Assert.Equal("de-DE", text.Culture.Name);
    }

    [Fact]
    public void ReportLocalization_LocalizesCompletePresentationWithoutMutatingSource()
    {
        var text = new LocalizationService();
        text.SetLanguage("de");
        var service = new ReportLocalizationService(text);
        var source = CreateEnglishReport();

        var localized = service.Localize(source);

        Assert.Equal("DNS-Namensauflösung", localized.Checks[0].Title);
        Assert.Equal("www.microsoft.com wurde erfolgreich aufgelöst.", localized.Checks[0].Summary);
        Assert.Equal("Testhost", localized.Checks[0].Evidence.Keys.Single());
        Assert.Equal("Ihre Internetverbindung sieht einwandfrei aus", localized.Diagnosis.Headline);
        Assert.Equal("DNS resolution", source.Checks[0].Title);
        Assert.Equal(source.Id, localized.Id);
    }

    [Fact]
    public async Task GermanPresentation_ProducesGermanExport()
    {
        var text = new LocalizationService();
        text.SetLanguage("de");
        var report = new ReportLocalizationService(text).Localize(CreateEnglishReport());
        var path = Path.Combine(Path.GetTempPath(), $"NetCheck-{Guid.NewGuid():N}.html");

        try
        {
            await new ReportExporter(text).ExportAsync(report, path, includeComputerName: false);
            var html = await File.ReadAllTextAsync(path);

            Assert.Contains("<html lang=\"de\">", html);
            Assert.Contains("Ihre Internetverbindung sieht einwandfrei aus", html);
            Assert.Contains("Abgeschlossen", html);
            Assert.DoesNotContain("Your internet connection looks healthy", html);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void StringDictionaries_HaveMatchingKeysAndCoverEveryDynamicResource()
    {
        var root = FindRepositoryRoot();
        var appDirectory = Path.Combine(root, "src", "NetCheck.App");
        var english = ReadKeys(Path.Combine(appDirectory, "Resources", "Strings.en.xaml"));
        var german = ReadKeys(Path.Combine(appDirectory, "Resources", "Strings.de.xaml"));

        Assert.Equal(english.Order(), german.Order());

        var referenced = Directory
            .EnumerateFiles(appDirectory, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => !Path.GetFileName(path).StartsWith("Strings.", StringComparison.Ordinal))
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"\{DynamicResource\s+([^}\s]+)\}")
                .Select(match => match.Groups[1].Value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(referenced.Except(english, StringComparer.Ordinal));
    }

    [Fact]
    public void ShellAndSettings_ContainRequestedBrandingAndSafetyChanges()
    {
        var root = FindRepositoryRoot();
        var appDirectory = Path.Combine(root, "src", "NetCheck.App");
        var shell = File.ReadAllText(Path.Combine(appDirectory, "MainWindow.xaml"));
        var settings = File.ReadAllText(Path.Combine(appDirectory, "Views", "SettingsView.xaml"));

        Assert.Contains("created by pcalsys", ReadResourceValue(appDirectory, "Strings.en.xaml", "CreatorCredit"));
        Assert.DoesNotContain("NETWORK CLARITY", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Private &amp; local", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("You stay in control", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("<TextBlock Text=\"NetCheck\"", shell, StringComparison.Ordinal);
        Assert.Contains("<TextBlock Text=\"English\" Foreground=\"White\"", shell, StringComparison.Ordinal);
        Assert.Contains("<TextBlock Text=\"Deutsch\" Foreground=\"White\"", shell, StringComparison.Ordinal);
        Assert.Contains("{DynamicResource ExpertWarningTitle}", settings, StringComparison.Ordinal);
        Assert.Contains("Nur für erfahrene Benutzer", ReadResourceValue(appDirectory, "Strings.de.xaml", "ExpertWarningTitle"));
    }

    [Fact]
    public void SpeedTestView_UsesOneWayBindingForReadOnlyProgress()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(
            root,
            "src",
            "NetCheck.App",
            "Views",
            "SpeedTestView.xaml"));

        Assert.Contains(
            "Value=\"{Binding ProgressPercentage, Mode=OneWay}\"",
            view,
            StringComparison.Ordinal);
    }

    private static DiagnosticReport CreateEnglishReport() => new()
    {
        Network = new NetworkSnapshot(),
        Checks =
        [
            new DiagnosticCheckResult
            {
                CheckId = DiagnosticCheckIds.Dns,
                Title = "DNS resolution",
                Category = DiagnosticCategory.NameResolution,
                Status = CheckStatus.Passed,
                Severity = FindingSeverity.Information,
                Summary = "www.microsoft.com resolved successfully.",
                Detail = "The configured DNS resolver returned one or more addresses.",
                Evidence = new Dictionary<string, string> { ["Test host"] = "www.microsoft.com" }
            }
        ],
        Diagnosis = new Diagnosis
        {
            Outcome = DiagnosticOutcome.Healthy,
            Headline = "Your internet connection looks healthy",
            Summary = "The adapter, local network, DNS, internet access, and connection quality checks completed successfully."
        }
    };

    private static HashSet<string> ReadKeys(string path)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return XDocument.Load(path)
            .Descendants()
            .Select(element => (string?)element.Attribute(x + "Key"))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string ReadResourceValue(string appDirectory, string fileName, string key)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return XDocument.Load(Path.Combine(appDirectory, "Resources", fileName))
            .Descendants()
            .Single(element => string.Equals((string?)element.Attribute(x + "Key"), key, StringComparison.Ordinal))
            .Value;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NetCheck.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the NetCheck repository root.");
    }
}

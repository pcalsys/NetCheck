using System.Globalization;
using NetCheck.App.Localization;
using NetCheck.Core.Models;

namespace NetCheck.App.ViewModels;

public sealed class HistoryItemViewModel
{
    private readonly LocalizationService _text;

    private HistoryItemViewModel(
        Guid id,
        DateTimeOffset occurredAtUtc,
        LocalizationService text,
        DiagnosticReport? report,
        ActivityHistoryEntry? activity)
    {
        Id = id;
        OccurredAtUtc = occurredAtUtc;
        _text = text;
        Report = report;
        Activity = activity;
        ChangedSettings = activity?.SettingChanges
            .Select(change => new SettingChangeViewModel(
                GetSettingLabel(change.SettingName),
                FormatSettingValue(change.SettingName, change.PreviousValue),
                FormatSettingValue(change.SettingName, change.NewValue)))
            .ToArray() ?? Array.Empty<SettingChangeViewModel>();
    }

    public Guid Id { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public DiagnosticReport? Report { get; }

    public ActivityHistoryEntry? Activity { get; }

    public SpeedTestResult? SpeedTestResult => Activity?.SpeedTestResult;

    public IReadOnlyList<SettingChangeViewModel> ChangedSettings { get; }

    public bool IsDiagnostic => Report is not null;

    public bool IsSpeedTest => Activity?.Kind == ActivityHistoryKind.SpeedTest;

    public bool IsConfigurationChange => Activity?.Kind is
        ActivityHistoryKind.SettingsChanged or ActivityHistoryKind.LanguageChanged;

    public bool CanExport => IsDiagnostic;

    public DiagnosticOutcome Outcome => Report?.Diagnosis.Outcome ?? DiagnosticOutcome.Healthy;

    public string BadgeLabel => Activity?.Kind switch
    {
        ActivityHistoryKind.SpeedTest => _text.Translate("SPEED TEST"),
        ActivityHistoryKind.SettingsChanged => _text.Translate("SETTINGS"),
        ActivityHistoryKind.LanguageChanged => _text.Translate("LANGUAGE"),
        _ => _text.Translate("DIAGNOSIS")
    };

    public string Title => Activity?.Kind switch
    {
        ActivityHistoryKind.SpeedTest => _text.Translate("Speed test completed"),
        ActivityHistoryKind.SettingsChanged => _text.Translate("Settings changed"),
        ActivityHistoryKind.LanguageChanged => _text.Translate("Language changed"),
        _ => Report?.Diagnosis.Headline ?? string.Empty
    };

    public string Summary => Activity?.Kind switch
    {
        ActivityHistoryKind.SpeedTest when SpeedTestResult is not null => _text.Format(
            "{0} download · {1} upload · {2} latency",
            FormatSpeed(SpeedTestResult.DownloadMegabitsPerSecond),
            FormatSpeed(SpeedTestResult.UploadMegabitsPerSecond),
            FormatLatency(SpeedTestResult.LatencyMilliseconds)),
        ActivityHistoryKind.SettingsChanged when ChangedSettings.Count == 1 =>
            _text.Translate("1 setting changed"),
        ActivityHistoryKind.SettingsChanged => _text.Format("{0} settings changed", ChangedSettings.Count),
        ActivityHistoryKind.LanguageChanged when ChangedSettings.Count > 0 => _text.Format(
            "{0} changed to {1}",
            ChangedSettings[0].PreviousValue,
            ChangedSettings[0].NewValue),
        _ => Report?.Diagnosis.Summary ?? string.Empty
    };

    public string AverageDownloadText => FormatSpeed(SpeedTestResult?.DownloadMegabitsPerSecond);

    public string PeakDownloadText => FormatSpeed(SpeedTestResult?.PeakDownloadMegabitsPerSecond);

    public string AverageUploadText => FormatSpeed(SpeedTestResult?.UploadMegabitsPerSecond);

    public string PeakUploadText => FormatSpeed(SpeedTestResult?.PeakUploadMegabitsPerSecond);

    public string LatencyText => SpeedTestResult is null
        ? "—"
        : FormatLatency(SpeedTestResult.LatencyMilliseconds);

    public string DurationText => SpeedTestResult is null
        ? "—"
        : string.Format(_text.Culture, "{0:N1} s", SpeedTestResult.Duration.TotalSeconds);

    public string DataUsedText => SpeedTestResult is null
        ? "—"
        : string.Format(
            _text.Culture,
            "{0:N1} MB",
            (SpeedTestResult.DownloadBytes + SpeedTestResult.UploadBytes) / 1_000_000d);

    public static HistoryItemViewModel FromReport(
        DiagnosticReport report,
        LocalizationService text)
    {
        ArgumentNullException.ThrowIfNull(report);
        return new HistoryItemViewModel(report.Id, report.CompletedAtUtc, text, report, null);
    }

    public static HistoryItemViewModel FromActivity(
        ActivityHistoryEntry activity,
        LocalizationService text)
    {
        ArgumentNullException.ThrowIfNull(activity);
        return new HistoryItemViewModel(activity.Id, activity.OccurredAtUtc, text, null, activity);
    }

    private string GetSettingLabel(string settingName) => settingName switch
    {
        nameof(DiagnosticOptions.DnsTestHost) => _text.Translate("DNS test hostname"),
        nameof(DiagnosticOptions.InternetPingTargets) => _text.Translate("Internet ping targets"),
        nameof(DiagnosticOptions.ConnectivityCheckUri) => _text.Translate("Web connectivity URL"),
        nameof(DiagnosticOptions.PingTimeoutMilliseconds) => _text.Translate("Ping timeout"),
        nameof(DiagnosticOptions.StabilitySampleCount) => _text.Translate("Stability samples"),
        nameof(DiagnosticOptions.PacketLossWarningPercent) => _text.Translate("Packet-loss warning"),
        nameof(DiagnosticOptions.LatencyWarningMilliseconds) => _text.Translate("Latency warning"),
        nameof(DiagnosticOptions.AutoRunOnLaunch) => _text.Translate("Run diagnostic on launch"),
        nameof(DiagnosticOptions.SaveDiagnosticHistory) => _text.Translate("Save diagnostic history"),
        nameof(DiagnosticOptions.IncludeComputerNameInExports) => _text.Translate("Computer name in exports"),
        nameof(DiagnosticOptions.MenuLanguage) => _text.Translate("Menu language"),
        _ => settingName
    };

    private string FormatSettingValue(string settingName, string value)
    {
        if (settingName is nameof(DiagnosticOptions.AutoRunOnLaunch)
            or nameof(DiagnosticOptions.SaveDiagnosticHistory)
            or nameof(DiagnosticOptions.IncludeComputerNameInExports))
        {
            return _text.Translate(string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                ? "Enabled"
                : "Disabled");
        }

        if (settingName == nameof(DiagnosticOptions.MenuLanguage))
        {
            return _text.Translate(string.Equals(value, "de", StringComparison.OrdinalIgnoreCase)
                ? "German"
                : "English");
        }

        if (settingName is nameof(DiagnosticOptions.PingTimeoutMilliseconds)
            or nameof(DiagnosticOptions.LatencyWarningMilliseconds))
        {
            return $"{FormatNumber(value)} ms";
        }

        if (settingName == nameof(DiagnosticOptions.PacketLossWarningPercent))
        {
            return $"{FormatNumber(value)} %";
        }

        return value;
    }

    private string FormatNumber(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number.ToString("0.##", _text.Culture)
            : value;

    private string FormatSpeed(double? value) => value is null
        ? "—"
        : string.Format(_text.Culture, "{0:N1} Mbit/s", value.Value);

    private string FormatLatency(double value) =>
        string.Format(_text.Culture, "{0:N0} ms", value);
}

public sealed record SettingChangeViewModel(
    string SettingLabel,
    string PreviousValue,
    string NewValue);

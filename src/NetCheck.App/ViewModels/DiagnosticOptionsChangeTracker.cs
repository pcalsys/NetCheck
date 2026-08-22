using System.Globalization;
using NetCheck.Core.Models;

namespace NetCheck.App.ViewModels;

internal static class DiagnosticOptionsChangeTracker
{
    public static IReadOnlyList<SettingChange> Compare(
        DiagnosticOptions previous,
        DiagnosticOptions current,
        bool includeMenuLanguage = false)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var changes = new List<SettingChange>();
        Add(changes, nameof(DiagnosticOptions.DnsTestHost), previous.DnsTestHost, current.DnsTestHost);
        Add(
            changes,
            nameof(DiagnosticOptions.InternetPingTargets),
            string.Join(", ", previous.InternetPingTargets),
            string.Join(", ", current.InternetPingTargets));
        Add(
            changes,
            nameof(DiagnosticOptions.ConnectivityCheckUri),
            previous.ConnectivityCheckUri.AbsoluteUri,
            current.ConnectivityCheckUri.AbsoluteUri);
        Add(changes, nameof(DiagnosticOptions.PingTimeoutMilliseconds), previous.PingTimeoutMilliseconds, current.PingTimeoutMilliseconds);
        Add(changes, nameof(DiagnosticOptions.StabilitySampleCount), previous.StabilitySampleCount, current.StabilitySampleCount);
        Add(changes, nameof(DiagnosticOptions.PacketLossWarningPercent), previous.PacketLossWarningPercent, current.PacketLossWarningPercent);
        Add(changes, nameof(DiagnosticOptions.LatencyWarningMilliseconds), previous.LatencyWarningMilliseconds, current.LatencyWarningMilliseconds);
        Add(changes, nameof(DiagnosticOptions.AutoRunOnLaunch), previous.AutoRunOnLaunch, current.AutoRunOnLaunch);
        Add(changes, nameof(DiagnosticOptions.SaveDiagnosticHistory), previous.SaveDiagnosticHistory, current.SaveDiagnosticHistory);
        Add(changes, nameof(DiagnosticOptions.IncludeComputerNameInExports), previous.IncludeComputerNameInExports, current.IncludeComputerNameInExports);
        if (includeMenuLanguage)
        {
            Add(changes, nameof(DiagnosticOptions.MenuLanguage), previous.MenuLanguage, current.MenuLanguage);
        }

        return changes;
    }

    private static void Add(
        ICollection<SettingChange> changes,
        string settingName,
        string previous,
        string current)
    {
        if (!string.Equals(previous, current, StringComparison.Ordinal))
        {
            changes.Add(new SettingChange(settingName, previous, current));
        }
    }

    private static void Add<T>(
        ICollection<SettingChange> changes,
        string settingName,
        T previous,
        T current)
        where T : IFormattable
    {
        var previousText = previous.ToString(null, CultureInfo.InvariantCulture);
        var currentText = current.ToString(null, CultureInfo.InvariantCulture);
        Add(changes, settingName, previousText, currentText);
    }

    private static void Add(
        ICollection<SettingChange> changes,
        string settingName,
        bool previous,
        bool current) => Add(
            changes,
            settingName,
            previous ? "true" : "false",
            current ? "true" : "false");
}

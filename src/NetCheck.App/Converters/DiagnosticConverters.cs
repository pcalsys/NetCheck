using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using NetCheck.App.Localization;
using NetCheck.Core.Models;

namespace NetCheck.App.Converters;

public sealed class OutcomeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var color = value switch
        {
            DiagnosticOutcome.Healthy or CheckStatus.Passed => Color.FromRgb(20, 139, 96),
            DiagnosticOutcome.Attention or CheckStatus.Warning => Color.FromRgb(181, 111, 13),
            DiagnosticOutcome.Problem or CheckStatus.Failed => Color.FromRgb(204, 55, 69),
            DiagnosticOutcome.Cancelled or CheckStatus.Skipped => Color.FromRgb(101, 113, 138),
            CheckStatus.Running => Color.FromRgb(53, 106, 230),
            _ => Color.FromRgb(53, 106, 230)
        };
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class OutcomeToTintBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var color = value switch
        {
            DiagnosticOutcome.Healthy => Color.FromRgb(234, 249, 243),
            DiagnosticOutcome.Attention => Color.FromRgb(255, 247, 230),
            DiagnosticOutcome.Problem => Color.FromRgb(255, 239, 241),
            DiagnosticOutcome.Cancelled => Color.FromRgb(242, 244, 248),
            _ => Color.FromRgb(237, 243, 255)
        };
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StatusToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        CheckStatus.Passed => "✓",
        CheckStatus.Warning => "!",
        CheckStatus.Failed => "×",
        CheckStatus.Skipped => "—",
        CheckStatus.Running => "•",
        _ => "·"
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StatusToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var source = value switch
        {
            CheckStatus.Passed => "PASSED",
            CheckStatus.Warning => "ATTENTION",
            CheckStatus.Failed => "FAILED",
            CheckStatus.Skipped => "SKIPPED",
            CheckStatus.Running => "RUNNING",
            _ => "PENDING"
        };
        return LocalizationService.Current?.Translate(source) ?? source;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class CompletedCountConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var count = value is int integer ? integer : 0;
        return LocalizationService.Current?.Format("{0} complete", count) ?? $"{count} complete";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class DurationMillisecondsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var duration = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
        return LocalizationService.Current?.Format("Completed in {0:0} ms", duration)
            ?? $"Completed in {duration:0} ms";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class DurationSecondsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var duration = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
        return LocalizationService.Current?.Format("{0:0.0} seconds", duration)
            ?? $"{duration:0.0} seconds";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class DateTimeTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = LocalizationService.Current;
        return value switch
        {
            DateTimeOffset date => date.ToLocalTime().ToString("g", text?.Culture ?? culture),
            DateTime date => date.ToLocalTime().ToString("g", text?.Culture ?? culture),
            _ => string.Empty
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class LocalizedTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var source = value?.ToString() ?? string.Empty;
        return LocalizationService.Current?.Translate(source) ?? source;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

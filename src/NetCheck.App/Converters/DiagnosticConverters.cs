using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
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
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        CheckStatus.Passed => "PASSED",
        CheckStatus.Warning => "ATTENTION",
        CheckStatus.Failed => "FAILED",
        CheckStatus.Skipped => "SKIPPED",
        CheckStatus.Running => "RUNNING",
        _ => "PENDING"
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}


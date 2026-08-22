using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;

namespace NetCheck.App.Controls;

public sealed class LineChart : FrameworkElement
{
    public LineChart()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values),
        typeof(IEnumerable),
        typeof(LineChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnValuesChanged));

    public static readonly DependencyProperty SecondaryValuesProperty = DependencyProperty.Register(
        nameof(SecondaryValues),
        typeof(IEnumerable),
        typeof(LineChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnValuesChanged));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke),
        typeof(Brush),
        typeof(LineChart),
        new FrameworkPropertyMetadata(Brushes.RoyalBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SecondaryStrokeProperty = DependencyProperty.Register(
        nameof(SecondaryStroke),
        typeof(Brush),
        typeof(LineChart),
        new FrameworkPropertyMetadata(Brushes.Teal, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(
        nameof(GridBrush),
        typeof(Brush),
        typeof(LineChart),
        new FrameworkPropertyMetadata(
            new SolidColorBrush(Color.FromRgb(226, 231, 240)),
            FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? Values
    {
        get => (IEnumerable?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IEnumerable? SecondaryValues
    {
        get => (IEnumerable?)GetValue(SecondaryValuesProperty);
        set => SetValue(SecondaryValuesProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Brush SecondaryStroke
    {
        get => (Brush)GetValue(SecondaryStrokeProperty);
        set => SetValue(SecondaryStrokeProperty, value);
    }

    public Brush GridBrush
    {
        get => (Brush)GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(
        double.IsInfinity(availableSize.Width) ? 240 : availableSize.Width,
        double.IsInfinity(availableSize.Height) ? 120 : availableSize.Height);

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (ActualWidth <= 1 || ActualHeight <= 1)
        {
            return;
        }

        var gridPen = new Pen(GridBrush, 1);
        for (var line = 1; line < 4; line++)
        {
            var y = Math.Round((ActualHeight * line) / 4) + 0.5;
            drawingContext.DrawLine(gridPen, new Point(0, y), new Point(ActualWidth, y));
        }

        var primary = ToValues(Values);
        var secondary = ToValues(SecondaryValues);
        var scaleValues = primary.Concat(secondary).Where(value => value is not null).Select(value => value!.Value).ToArray();
        var maximum = scaleValues.Length == 0 ? 1 : Math.Max(1, scaleValues.Max() * 1.1);
        DrawSeries(drawingContext, primary, maximum, Stroke, 2.2);
        DrawSeries(drawingContext, secondary, maximum, SecondaryStroke, 1.8);
    }

    private static IReadOnlyList<double?> ToValues(IEnumerable? source) => source is null
        ? Array.Empty<double?>()
        : source.Cast<object?>().Select(value => value switch
        {
            null => (double?)null,
            double number when double.IsFinite(number) => number,
            float number when float.IsFinite(number) => number,
            _ => null
        }).ToArray();

    private void DrawSeries(
        DrawingContext drawingContext,
        IReadOnlyList<double?> values,
        double maximum,
        Brush brush,
        double thickness)
    {
        if (values.Count == 0)
        {
            return;
        }

        var pen = new Pen(brush, thickness)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var segmentOpen = false;
            for (var index = 0; index < values.Count; index++)
            {
                if (values[index] is not { } value)
                {
                    segmentOpen = false;
                    continue;
                }

                var x = values.Count == 1 ? ActualWidth / 2 : (ActualWidth * index) / (values.Count - 1);
                var y = ActualHeight - Math.Clamp(value / maximum, 0, 1) * (ActualHeight - 5) - 2.5;
                var point = new Point(x, y);
                if (!segmentOpen)
                {
                    context.BeginFigure(point, isFilled: false, isClosed: false);
                    segmentOpen = true;
                }
                else
                {
                    context.LineTo(point, isStroked: true, isSmoothJoin: true);
                }
            }
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, pen, geometry);
    }

    private static void OnValuesChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var chart = (LineChart)dependencyObject;
        chart.Detach(args.OldValue);
        if (chart.IsLoaded)
        {
            chart.Attach(args.NewValue);
        }

        chart.InvalidateVisual();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Attach(Values);
        Attach(SecondaryValues);
        InvalidateVisual();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Detach(Values);
        Detach(SecondaryValues);
    }

    private void Attach(object? source)
    {
        if (source is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged -= OnCollectionChanged;
            collection.CollectionChanged += OnCollectionChanged;
        }
    }

    private void Detach(object? source)
    {
        if (source is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged -= OnCollectionChanged;
        }
    }
}

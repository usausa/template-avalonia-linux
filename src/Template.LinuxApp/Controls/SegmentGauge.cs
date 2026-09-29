namespace Template.LinuxApp.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

public sealed class SegmentGauge : Control
{
    private const double CellWidth = 6;

    private const double CellGap = 2;

    private const double LabelHeight = 17;

    private const double BarHeight = 18;

    private const double UnfilledOpacity = 0.22;

    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<SegmentGauge, string?>(nameof(Label));

    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<SegmentGauge, double>(nameof(Value), Double.NaN);

    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<SegmentGauge, string?>(nameof(Text));

    public static readonly StyledProperty<double> MinimumProperty = AvaloniaProperty.Register<SegmentGauge, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<SegmentGauge, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<double> ThresholdProperty = AvaloniaProperty.Register<SegmentGauge, double>(nameof(Threshold), Double.NaN);

    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<SegmentGauge, IBrush?>(nameof(Fill), Brushes.Gold);

    public static readonly StyledProperty<IBrush?> ThresholdFillProperty = AvaloniaProperty.Register<SegmentGauge, IBrush?>(nameof(ThresholdFill), Brushes.Red);

    public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<SegmentGauge, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<double> TextWidthProperty = AvaloniaProperty.Register<SegmentGauge, double>(nameof(TextWidth), 80);

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Threshold
    {
        get => GetValue(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public IBrush? ThresholdFill
    {
        get => GetValue(ThresholdFillProperty);
        set => SetValue(ThresholdFillProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double TextWidth
    {
        get => GetValue(TextWidthProperty);
        set => SetValue(TextWidthProperty, value);
    }

    static SegmentGauge()
    {
        AffectsRender<SegmentGauge>(LabelProperty, ValueProperty, TextProperty, MinimumProperty, MaximumProperty, ThresholdProperty, FillProperty, ThresholdFillProperty, ForegroundProperty, TextWidthProperty);
        AffectsMeasure<SegmentGauge>(LabelProperty);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(0, (String.IsNullOrEmpty(Label) ? 0 : LabelHeight) + BarHeight);

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        if (width <= 0)
        {
            return;
        }

        var top = 0d;
        if (!String.IsNullOrEmpty(Label))
        {
            var label = ChartHelper.CreateText(Label, 12, Foreground ?? ChartHelper.DefaultTextBrush);
            label.MaxTextWidth = width;
            label.MaxLineCount = 1;
            label.Trimming = TextTrimming.CharacterEllipsis;
            context.DrawText(label, new Point(0, 0));
            top = LabelHeight;
        }

        var value = Value;
        var hasValue = Double.IsFinite(value);
        var range = Maximum > Minimum ? Maximum - Minimum : 1d;
        var valueBrush = Pick(value);
        var barWidth = Math.Max(0, width - TextWidth - 8);
        var count = Math.Max(1, (int)((barWidth + CellGap) / (CellWidth + CellGap)));
        var cellWidth = (barWidth - (CellGap * (count - 1))) / count;
        var lit = hasValue ? (int)Math.Round(Math.Clamp((value - Minimum) / range, 0d, 1d) * count) : 0;
        for (var i = 0; i < count; i++)
        {
            var brush = Pick(Minimum + (range * (i + 0.5) / count));
            var left = Math.Round(i * (cellWidth + CellGap));
            var right = Math.Round((i * (cellWidth + CellGap)) + cellWidth);
            var rect = new Rect(left, top, Math.Max(1, right - left), BarHeight);
            if (i < lit)
            {
                context.DrawRectangle(brush, null, rect);
            }
            else
            {
                using (context.PushOpacity(UnfilledOpacity))
                {
                    context.DrawRectangle(brush, null, rect);
                }
            }
        }

        var text = ChartHelper.CreateText(String.IsNullOrEmpty(Text) ? "—" : Text, 16, hasValue ? valueBrush ?? Brushes.Gray : Foreground ?? ChartHelper.DefaultTextBrush, ChartHelper.BoldTypeface);
        context.DrawText(text, new Point(width - text.Width, top + ((BarHeight - text.Height) / 2)));
    }

    private IBrush? Pick(double value) =>
        !Double.IsNaN(Threshold) && Double.IsFinite(value) && (value >= Threshold) ? ThresholdFill : Fill;
}

namespace Template.LinuxApp.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

public sealed class RingGauge : Control
{
    private const double StartAngle = 135;

    private const double SweepAngle = 270;

    private const double TrackOpacity = 0.22;

    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<RingGauge, double>(nameof(Value), Double.NaN);

    public static readonly StyledProperty<double> MinimumProperty = AvaloniaProperty.Register<RingGauge, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<RingGauge, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<RingGauge, string?>(nameof(Text));

    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<RingGauge, string?>(nameof(Label));

    public static readonly StyledProperty<double> ThicknessProperty = AvaloniaProperty.Register<RingGauge, double>(nameof(Thickness), 16);

    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<RingGauge, IBrush?>(nameof(Fill), Brushes.Gold);

    public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<RingGauge, IBrush?>(nameof(Foreground));

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
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

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    static RingGauge()
    {
        AffectsRender<RingGauge>(ValueProperty, MinimumProperty, MaximumProperty, TextProperty, LabelProperty, ThicknessProperty, FillProperty, ForegroundProperty);
    }

    public override void Render(DrawingContext context)
    {
        var thickness = Thickness;
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= thickness * 2)
        {
            return;
        }

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = (size - thickness) / 2;
        var fill = Fill ?? Brushes.Gray;
        var pen = new Pen(fill, thickness);
        using (context.PushOpacity(TrackOpacity))
        {
            context.DrawGeometry(null, pen, CreateArc(center, radius, SweepAngle));
        }

        var value = Value;
        var hasValue = Double.IsFinite(value);
        if (hasValue)
        {
            var range = Maximum > Minimum ? Maximum - Minimum : 1d;
            var sweep = Math.Clamp((value - Minimum) / range, 0d, 1d) * SweepAngle;
            if (sweep > 0)
            {
                context.DrawGeometry(null, pen, CreateArc(center, radius, sweep));
            }
        }

        var foreground = Foreground ?? ChartHelper.DefaultTextBrush;
        var text = ChartHelper.CreateText(String.IsNullOrEmpty(Text) ? "—" : Text, Math.Max(16, radius * 0.4), hasValue ? fill : foreground, ChartHelper.BoldTypeface);
        context.DrawText(text, new Point(center.X - (text.Width / 2), center.Y - (text.Height / 2)));

        if (!String.IsNullOrEmpty(Label))
        {
            var label = ChartHelper.CreateText(Label, Math.Max(12, radius * 0.16), foreground);
            label.MaxTextWidth = radius * 1.4;
            label.MaxLineCount = 1;
            label.Trimming = TextTrimming.CharacterEllipsis;
            context.DrawText(label, new Point(center.X - (label.Width / 2), center.Y + (radius * 0.72) - (label.Height / 2)));
        }
    }

    private static StreamGeometry CreateArc(Point center, double radius, double sweep)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(PointAt(center, radius, StartAngle), false);
        ctx.ArcTo(PointAt(center, radius, StartAngle + sweep), new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise);
        ctx.EndFigure(false);
        return geometry;
    }

    private static Point PointAt(Point center, double radius, double angle)
    {
        var radian = angle * Math.PI / 180;
        return new Point(center.X + (radius * Math.Cos(radian)), center.Y + (radius * Math.Sin(radian)));
    }
}

namespace Template.LinuxApp.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

public sealed class StickScope : Control
{
    private const int TrailCapacity = 120;

    private const double TrailOpacity = 0.5;

    private const double ZoneOpacity = 0.18;

    private const double DotRatio = 0.035;

    private const double MinDotRadius = 4;

    private const double LabelRatio = 0.1;

    private const double MinLabelSize = 11;

    private const double LabelMargin = 3;

    private static readonly TimeSpan TrailDuration = TimeSpan.FromSeconds(1);

    public static readonly StyledProperty<Point> PositionProperty = AvaloniaProperty.Register<StickScope, Point>(nameof(Position));

    public static readonly StyledProperty<double> RangeProperty = AvaloniaProperty.Register<StickScope, double>(nameof(Range), 0.2);

    public static readonly StyledProperty<double> DeadzoneProperty = AvaloniaProperty.Register<StickScope, double>(nameof(Deadzone), 0.08);

    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<StickScope, IBrush?>(nameof(Fill), Brushes.LimeGreen);

    public static readonly StyledProperty<IBrush?> OutsideFillProperty = AvaloniaProperty.Register<StickScope, IBrush?>(nameof(OutsideFill), Brushes.Orange);

    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<StickScope, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<StickScope, IBrush?>(nameof(Foreground));

    private readonly List<(Point Position, TimeSpan? Time)> trail = [];

    private TopLevel? topLevel;

    private TimeSpan frameTime;

    private bool frameRequested;

    public Point Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public double Range
    {
        get => GetValue(RangeProperty);
        set => SetValue(RangeProperty, value);
    }

    public double Deadzone
    {
        get => GetValue(DeadzoneProperty);
        set => SetValue(DeadzoneProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public IBrush? OutsideFill
    {
        get => GetValue(OutsideFillProperty);
        set => SetValue(OutsideFillProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    static StickScope()
    {
        AffectsRender<StickScope>(PositionProperty, RangeProperty, DeadzoneProperty, FillProperty, OutsideFillProperty, StrokeProperty, ForegroundProperty);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        topLevel = TopLevel.GetTopLevel(this);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        topLevel = null;
        trail.Clear();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if ((change.Property == PositionProperty) && (topLevel is not null))
        {
            if (trail.Count == TrailCapacity)
            {
                trail.RemoveAt(0);
            }

            trail.Add((change.GetOldValue<Point>(), null));
            RequestFrame();
        }
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        var dot = Math.Max(MinDotRadius, size * DotRatio);
        var radius = (size / 2) - dot - 1;
        if (radius <= 0)
        {
            return;
        }

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var range = Range > 0 ? Range : 1d;
        var scale = radius / range;
        var deadzone = Math.Clamp(Deadzone, 0d, range);
        var zone = deadzone * scale;
        var inside = Fill ?? Brushes.LimeGreen;
        var outside = OutsideFill ?? Brushes.Orange;

        var grid = new Pen(Stroke ?? ChartHelper.DefaultGridBrush);
        context.DrawEllipse(null, grid, center, radius, radius);
        context.DrawLine(grid, new Point(center.X - radius, center.Y), new Point(center.X + radius, center.Y));
        context.DrawLine(grid, new Point(center.X, center.Y - radius), new Point(center.X, center.Y + radius));
        using (context.PushOpacity(ZoneOpacity))
        {
            context.DrawEllipse(inside, null, center, zone, zone);
        }

        context.DrawEllipse(null, new Pen(inside), center, zone, zone);

        var foreground = Foreground ?? ChartHelper.DefaultTextBrush;
        var labelSize = Math.Max(MinLabelSize, radius * LabelRatio);
        var zoneLabel = ChartHelper.CreateText(FormatPercent(deadzone), labelSize, foreground);
        context.DrawText(zoneLabel, new Point(center.X + zone + LabelMargin, center.Y + LabelMargin));
        var rangeLabel = ChartHelper.CreateText(FormatPercent(range), labelSize, foreground);
        context.DrawText(rangeLabel, new Point(center.X + LabelMargin, center.Y - radius + LabelMargin));

        foreach (var (position, time) in trail)
        {
            var age = time is { } value ? (frameTime - value) / TrailDuration : 0d;
            var length = Length(position);
            if ((age >= 1) || !(length <= range))
            {
                continue;
            }

            using (context.PushOpacity((1 - age) * TrailOpacity))
            {
                context.DrawEllipse(length > deadzone ? outside : inside, null, Map(center, position, scale), dot / 2, dot / 2);
            }
        }

        var current = Position;
        var magnitude = Length(current);
        if (!Double.IsFinite(magnitude))
        {
            return;
        }

        var brush = magnitude > deadzone ? outside : inside;
        var shown = magnitude > range ? new Point(current.X * range / magnitude, current.Y * range / magnitude) : current;
        var point = Map(center, shown, scale);
        context.DrawLine(new Pen(brush, 1.5), center, point);
        if (magnitude > range)
        {
            context.DrawEllipse(null, new Pen(brush, 2), point, dot, dot);
        }
        else
        {
            context.DrawEllipse(brush, null, point, dot, dot);
        }
    }

    private void RequestFrame()
    {
        if (frameRequested || (topLevel is null))
        {
            return;
        }

        frameRequested = true;
        topLevel.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan time)
    {
        frameRequested = false;
        if (topLevel is null)
        {
            return;
        }

        frameTime = time;
        var expired = 0;
        for (var i = 0; i < trail.Count; i++)
        {
            var (position, stamp) = trail[i];
            if (stamp is null)
            {
                trail[i] = (position, time);
            }
            else if (time - stamp.Value >= TrailDuration)
            {
                expired = i + 1;
            }
        }

        trail.RemoveRange(0, expired);
        InvalidateVisual();
        if (trail.Count > 0)
        {
            RequestFrame();
        }
    }

    private static double Length(Point point) => Math.Sqrt((point.X * point.X) + (point.Y * point.Y));

    private static Point Map(Point center, Point position, double scale) => new(center.X + (position.X * scale), center.Y + (position.Y * scale));

    private static string FormatPercent(double value) => String.Create(CultureInfo.InvariantCulture, $"{value * 100:0.#}%");
}

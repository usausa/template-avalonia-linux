namespace Template.LinuxApp.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

public sealed class AnalogClock : Control
{
    public static readonly StyledProperty<TimeSpan> TimeProperty = AvaloniaProperty.Register<AnalogClock, TimeSpan>(nameof(Time));

    public static readonly StyledProperty<IBrush?> DialBrushProperty = AvaloniaProperty.Register<AnalogClock, IBrush?>(nameof(DialBrush), Brushes.Gray);

    public static readonly StyledProperty<IBrush?> HandBrushProperty = AvaloniaProperty.Register<AnalogClock, IBrush?>(nameof(HandBrush), Brushes.Black);

    public static readonly StyledProperty<IBrush?> SecondHandBrushProperty = AvaloniaProperty.Register<AnalogClock, IBrush?>(nameof(SecondHandBrush), Brushes.Red);

    public TimeSpan Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    public IBrush? DialBrush
    {
        get => GetValue(DialBrushProperty);
        set => SetValue(DialBrushProperty, value);
    }

    public IBrush? HandBrush
    {
        get => GetValue(HandBrushProperty);
        set => SetValue(HandBrushProperty, value);
    }

    public IBrush? SecondHandBrush
    {
        get => GetValue(SecondHandBrushProperty);
        set => SetValue(SecondHandBrushProperty, value);
    }

    static AnalogClock()
    {
        AffectsRender<AnalogClock>(TimeProperty, DialBrushProperty, HandBrushProperty, SecondHandBrushProperty);
    }

    public override void Render(DrawingContext context)
    {
        var radius = (Math.Min(Bounds.Width, Bounds.Height) / 2) - 2;
        if (radius <= 0)
        {
            return;
        }

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        if (DialBrush is { } dialBrush)
        {
            context.DrawEllipse(null, new Pen(dialBrush, 2), center, radius, radius);
            var hourPen = new Pen(dialBrush, 3, lineCap: PenLineCap.Round);
            var minutePen = new Pen(dialBrush);
            for (var i = 0; i < 60; i++)
            {
                var isHour = i % 5 == 0;
                context.DrawLine(isHour ? hourPen : minutePen, GetPoint(center, radius - (isHour ? 14 : 8), i * 6), GetPoint(center, radius - 4, i * 6));
            }
        }

        var time = Time;
        var seconds = (double)time.Seconds;
        var minutes = time.Minutes + (seconds / 60);
        var hours = (time.Hours % 12) + (minutes / 60);
        DrawHand(context, HandBrush, center, radius * 0.5, hours * 30, 6);
        DrawHand(context, HandBrush, center, radius * 0.75, minutes * 6, 4);
        DrawHand(context, SecondHandBrush, center, radius * 0.85, seconds * 6, 2);
        if (SecondHandBrush is { } secondHandBrush)
        {
            context.DrawEllipse(secondHandBrush, null, center, 4, 4);
        }
    }

    private static void DrawHand(DrawingContext context, IBrush? brush, Point center, double length, double angle, double thickness)
    {
        if (brush is null)
        {
            return;
        }

        context.DrawLine(new Pen(brush, thickness, lineCap: PenLineCap.Round), center, GetPoint(center, length, angle));
    }

    private static Point GetPoint(Point center, double radius, double angle)
    {
        var radian = (angle - 90) * Math.PI / 180;
        return new Point(center.X + (radius * Math.Cos(radian)), center.Y + (radius * Math.Sin(radian)));
    }
}

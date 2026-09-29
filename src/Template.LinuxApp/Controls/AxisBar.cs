namespace Template.LinuxApp.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

public sealed class AxisBar : Control
{
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<AxisBar, double>(nameof(Value));

    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<AxisBar, IBrush?>(nameof(Fill), Brushes.DodgerBlue);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty = AvaloniaProperty.Register<AxisBar, IBrush?>(nameof(TrackBrush), Brushes.LightGray);

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    static AxisBar()
    {
        AffectsRender<AxisBar>(ValueProperty, FillProperty, TrackBrushProperty);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if ((width <= 0) || (height <= 0))
        {
            return;
        }

        context.DrawRectangle(TrackBrush, null, new Rect(0, 0, width, height));
        var center = Math.Round(width / 2);
        var value = Double.IsFinite(Value) ? Math.Clamp(Value, -1d, 1d) : 0d;
        var end = center + (value * width / 2);
        if (Math.Abs(end - center) >= 1)
        {
            context.DrawRectangle(Fill, null, new Rect(Math.Min(center, end), 0, Math.Abs(end - center), height));
        }

        context.DrawRectangle(Fill, null, new Rect(center - 0.5, 0, 1, height));
    }
}

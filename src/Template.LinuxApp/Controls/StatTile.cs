namespace Template.LinuxApp.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

public sealed class StatTile : Control
{
    private const double Radius = 2;

    private static readonly IBrush AreaBrush = new ImmutableSolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));

    private static readonly IPen LinePen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF)), 1.5);

    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<StatTile, string?>(nameof(Title));

    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<StatTile, string?>(nameof(Value));

    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty = AvaloniaProperty.Register<StatTile, IReadOnlyList<double>?>(nameof(Values));

    public static readonly StyledProperty<int> CapacityProperty = AvaloniaProperty.Register<StatTile, int>(nameof(Capacity), 120);

    public static readonly StyledProperty<double> MinimumProperty = AvaloniaProperty.Register<StatTile, double>(nameof(Minimum), Double.NaN);

    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<StatTile, double>(nameof(Maximum), Double.NaN);

    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<StatTile, IBrush?>(nameof(Fill), Brushes.SteelBlue);

    public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<StatTile, IBrush?>(nameof(Foreground), Brushes.White);

    private IBrush? background;

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IReadOnlyList<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public int Capacity
    {
        get => GetValue(CapacityProperty);
        set => SetValue(CapacityProperty, value);
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

    static StatTile()
    {
        AffectsRender<StatTile>(TitleProperty, ValueProperty, ValuesProperty, CapacityProperty, MinimumProperty, MaximumProperty, FillProperty, ForegroundProperty);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if ((bounds.Width <= 0) || (bounds.Height <= 0))
        {
            return;
        }

        background ??= CreateBackground(Fill);
        context.DrawRectangle(background, null, bounds, Radius, Radius);

        if (Values is { Count: > 1 } values)
        {
            var area = new Rect(0, bounds.Height * 0.4, bounds.Width, bounds.Height * 0.6);
            var (minimum, maximum) = CalcRange(values);
            foreach (var segment in ChartHelper.BuildSegments(values, area, Capacity, minimum, maximum))
            {
                if (segment.Count < 2)
                {
                    continue;
                }

                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(new Point(segment[0].X, area.Bottom));
                    foreach (var point in segment)
                    {
                        ctx.LineTo(point);
                    }

                    ctx.LineTo(new Point(segment[^1].X, area.Bottom));
                    ctx.EndFigure(true);
                }

                context.DrawGeometry(AreaBrush, null, geometry);
                for (var i = 1; i < segment.Count; i++)
                {
                    context.DrawLine(LinePen, segment[i - 1], segment[i]);
                }
            }
        }

        var foreground = Foreground ?? Brushes.White;
        if (!String.IsNullOrEmpty(Title))
        {
            context.DrawText(ChartHelper.CreateText(Title, 13, foreground), new Point(8, 6));
        }

        var value = ChartHelper.CreateText(String.IsNullOrEmpty(Value) ? "—" : Value, Math.Clamp(bounds.Height * 0.32, 16, 34), foreground, ChartHelper.BoldTypeface);
        context.DrawText(value, new Point(bounds.Width - value.Width - 8, 3));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FillProperty)
        {
            background = null;
        }
    }

    private static IBrush? CreateBackground(IBrush? fill)
    {
        if (fill is not ISolidColorBrush solid)
        {
            return fill;
        }

        var hsl = solid.Color.ToHsl();
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Shift(hsl, 8, -0.15), 0),
                new GradientStop(Shift(hsl, -8, -0.05), 1)
            }
        }.ToImmutable();
    }

    private static Color Shift(HslColor hsl, double hue, double lightness) =>
        HslColor.ToRgb((hsl.H + hue + 360) % 360, hsl.S, Math.Clamp(hsl.L + lightness, 0, 1), hsl.A);

    private (double Minimum, double Maximum) CalcRange(IEnumerable<double> values)
    {
        var finite = values.Where(Double.IsFinite).ToList();
        var minimum = Double.IsNaN(Minimum) ? (finite.Count > 0 ? finite.Min() : 0d) : Minimum;
        var maximum = Double.IsNaN(Maximum) ? (finite.Count > 0 ? finite.Max() : minimum + 1d) : Maximum;
        return maximum > minimum ? (minimum, maximum) : (minimum, minimum + 1d);
    }
}

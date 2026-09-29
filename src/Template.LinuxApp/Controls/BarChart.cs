namespace Template.LinuxApp.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

public sealed class BarChart : Control
{
    private const int GridDivisions = 2;

    private const double BarWidthFactor = 0.6;

    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty = AvaloniaProperty.Register<BarChart, IReadOnlyList<double>?>(nameof(Values));

    public static readonly StyledProperty<int> CapacityProperty = AvaloniaProperty.Register<BarChart, int>(nameof(Capacity), 120);

    public static readonly StyledProperty<DateTimeOffset?> TimeProperty = AvaloniaProperty.Register<BarChart, DateTimeOffset?>(nameof(Time));

    public static readonly StyledProperty<TimeSpan> IntervalProperty = AvaloniaProperty.Register<BarChart, TimeSpan>(nameof(Interval), TimeSpan.FromSeconds(1));

    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<BarChart, double>(nameof(Maximum), Double.NaN);

    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<BarChart, IBrush?>(nameof(Fill), Brushes.Orange);

    public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<BarChart, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> GridBrushProperty = AvaloniaProperty.Register<BarChart, IBrush?>(nameof(GridBrush));

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

    public DateTimeOffset? Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    public TimeSpan Interval
    {
        get => GetValue(IntervalProperty);
        set => SetValue(IntervalProperty, value);
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

    public IBrush? GridBrush
    {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    static BarChart()
    {
        AffectsRender<BarChart>(ValuesProperty, CapacityProperty, TimeProperty, IntervalProperty, MaximumProperty, FillProperty, ForegroundProperty, GridBrushProperty);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if ((width <= 0) || (height <= 0))
        {
            return;
        }

        var textBrush = Foreground ?? ChartHelper.DefaultTextBrush;
        var gridBrush = GridBrush ?? ChartHelper.DefaultGridBrush;
        var values = Values ?? [];
        var plot = new Rect(ChartHelper.AxisWidth, 6, Math.Max(0, width - ChartHelper.AxisWidth - 4), Math.Max(0, height - 6 - ChartHelper.TimeAxisHeight));
        var (_, maximum, divisions) = ChartHelper.CalcAxis(values, 0, Maximum, GridDivisions);
        ChartHelper.DrawAxis(context, plot, 0, maximum, divisions, textBrush, gridBrush);

        var slots = Math.Max(Capacity, values.Count);
        if (slots == 0)
        {
            return;
        }

        var step = plot.Width / slots;
        if ((Time is { } time) && (slots > 1))
        {
            ChartHelper.DrawTimeAxis(context, plot.Deflate(new Thickness(step / 2, 0)), time, Interval * (slots - 1), textBrush, gridBrush);
        }

        var barWidth = Math.Max(1, Math.Floor(step * BarWidthFactor));
        var offset = slots - values.Count;
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (!Double.IsFinite(value) || (value <= 0))
            {
                continue;
            }

            var barHeight = Math.Min(value / maximum, 1d) * plot.Height;
            var left = Math.Floor(plot.Left + ((offset + i) * step) + ((step - barWidth) / 2));
            context.DrawRectangle(Fill, null, new Rect(left, plot.Bottom - barHeight, barWidth, barHeight));
        }
    }
}

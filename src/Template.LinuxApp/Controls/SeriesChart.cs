namespace Template.LinuxApp.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

public sealed record ChartSeries(string Name, IReadOnlyList<double> Values, string Last);

public sealed class SeriesChart : Control
{
    private const int GridDivisions = 4;

    private const double RowHeight = 18;

    public static readonly StyledProperty<IReadOnlyList<ChartSeries>?> SeriesProperty = AvaloniaProperty.Register<SeriesChart, IReadOnlyList<ChartSeries>?>(nameof(Series));

    public static readonly StyledProperty<IReadOnlyList<Color>?> PaletteProperty = AvaloniaProperty.Register<SeriesChart, IReadOnlyList<Color>?>(nameof(Palette));

    public static readonly StyledProperty<int> CapacityProperty = AvaloniaProperty.Register<SeriesChart, int>(nameof(Capacity), 120);

    public static readonly StyledProperty<DateTimeOffset?> TimeProperty = AvaloniaProperty.Register<SeriesChart, DateTimeOffset?>(nameof(Time));

    public static readonly StyledProperty<TimeSpan> IntervalProperty = AvaloniaProperty.Register<SeriesChart, TimeSpan>(nameof(Interval), TimeSpan.FromSeconds(1));

    public static readonly StyledProperty<double> MinimumProperty = AvaloniaProperty.Register<SeriesChart, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<SeriesChart, double>(nameof(Maximum), Double.NaN);

    public static readonly StyledProperty<double> LegendWidthProperty = AvaloniaProperty.Register<SeriesChart, double>(nameof(LegendWidth), 180);

    public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<SeriesChart, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> GridBrushProperty = AvaloniaProperty.Register<SeriesChart, IBrush?>(nameof(GridBrush));

    public IReadOnlyList<ChartSeries>? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public IReadOnlyList<Color>? Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
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

    public double LegendWidth
    {
        get => GetValue(LegendWidthProperty);
        set => SetValue(LegendWidthProperty, value);
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

    static SeriesChart()
    {
        AffectsRender<SeriesChart>(SeriesProperty, PaletteProperty, CapacityProperty, TimeProperty, IntervalProperty, MinimumProperty, MaximumProperty, LegendWidthProperty, ForegroundProperty, GridBrushProperty);
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
        var series = Series ?? [];
        var brushes = CreateBrushes(series.Count, textBrush);
        var legendWidth = width > LegendWidth * 2 ? LegendWidth : 0;
        var plot = new Rect(ChartHelper.AxisWidth, 8, Math.Max(0, width - ChartHelper.AxisWidth - legendWidth - 8), Math.Max(0, height - 8 - ChartHelper.TimeAxisHeight));

        var (minimum, maximum, divisions) = ChartHelper.CalcAxis(series.SelectMany(static x => x.Values), Minimum, Maximum, GridDivisions);
        ChartHelper.DrawAxis(context, plot, minimum, maximum, divisions, textBrush, gridBrush);

        var slots = Math.Max(Capacity, series.Count > 0 ? series.Max(static x => x.Values.Count) : 0);
        if ((Time is { } time) && (slots > 1))
        {
            ChartHelper.DrawTimeAxis(context, plot, time, Interval * (slots - 1), textBrush, gridBrush);
        }

        using (context.PushClip(new Rect(plot.Left, 0, plot.Width, height)))
        {
            for (var i = 0; i < series.Count; i++)
            {
                var pen = new Pen(brushes[i], 1.5);
                foreach (var segment in ChartHelper.BuildSegments(series[i].Values, plot, slots, minimum, maximum))
                {
                    if (segment.Count < 2)
                    {
                        continue;
                    }

                    var geometry = new StreamGeometry();
                    using (var ctx = geometry.Open())
                    {
                        ctx.BeginFigure(segment[0], false);
                        for (var j = 1; j < segment.Count; j++)
                        {
                            ctx.LineTo(segment[j]);
                        }

                        ctx.EndFigure(false);
                    }

                    context.DrawGeometry(null, pen, geometry);
                }
            }
        }

        if ((legendWidth > 0) && (series.Count > 0))
        {
            DrawLegend(context, series, brushes, new Rect(width - legendWidth, 0, legendWidth, height), textBrush);
        }
    }

    private IBrush[] CreateBrushes(int count, IBrush fallback)
    {
        var palette = Palette;
        var brushes = new IBrush[count];
        for (var i = 0; i < count; i++)
        {
            brushes[i] = palette is { Count: > 0 } ? new ImmutableSolidColorBrush(palette[i % palette.Count]) : fallback;
        }

        return brushes;
    }

    private static void DrawLegend(DrawingContext context, IReadOnlyList<ChartSeries> series, IBrush[] brushes, Rect area, IBrush textBrush)
    {
        var header = ChartHelper.CreateText("Name", 11, textBrush);
        context.DrawText(header, new Point(area.Left + 18, area.Top + 2));
        var last = ChartHelper.CreateText("Last", 11, textBrush);
        context.DrawText(last, new Point(area.Right - last.Width - 4, area.Top + 2));

        var rows = Math.Max(0, (int)((area.Height - RowHeight) / RowHeight));
        for (var i = 0; i < Math.Min(series.Count, rows); i++)
        {
            var y = area.Top + RowHeight + (i * RowHeight);
            context.DrawRectangle(brushes[i], null, new Rect(area.Left + 2, y + 7, 12, 3));
            var value = ChartHelper.CreateText(series[i].Last, 12, textBrush);
            var valueLeft = area.Right - value.Width - 4;
            var name = ChartHelper.CreateText(series[i].Name, 12, textBrush);
            name.MaxTextWidth = Math.Max(0, valueLeft - area.Left - 24);
            name.MaxLineCount = 1;
            name.Trimming = TextTrimming.CharacterEllipsis;
            context.DrawText(name, new Point(area.Left + 18, y));
            context.DrawText(value, new Point(valueLeft, y));
        }
    }
}

namespace Template.LinuxApp.Controls;

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

internal static class ChartHelper
{
    public const double AxisWidth = 40;

    public const double TimeAxisHeight = 16;

    public static readonly IBrush DefaultTextBrush = new ImmutableSolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));

    public static readonly IBrush DefaultGridBrush = new ImmutableSolidColorBrush(Color.FromArgb(0x40, 0x9E, 0x9E, 0x9E));

    public static readonly Typeface BoldTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);

    private static readonly TimeSpan[] TimeSteps =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1)
    ];

    public static (double Minimum, double Maximum, int Divisions) CalcAxis(IEnumerable<double> values, double minimum, double maximum, int divisions)
    {
        var dataMin = Double.PositiveInfinity;
        var dataMax = Double.NegativeInfinity;
        foreach (var value in values)
        {
            if (!Double.IsFinite(value))
            {
                continue;
            }

            dataMin = Math.Min(dataMin, value);
            dataMax = Math.Max(dataMax, value);
        }

        var min = Double.IsNaN(minimum) ? (Double.IsFinite(dataMin) ? dataMin : 0d) : minimum;
        if (!Double.IsNaN(maximum))
        {
            return (min, maximum > min ? maximum : min + 1d, divisions);
        }

        var span = Double.IsFinite(dataMax) && (dataMax > min) ? dataMax - min : 1d;
        var step = NiceStep(span / divisions);
        var count = Math.Clamp((int)Math.Ceiling((span / step) - 1e-9), 1, divisions);
        return (min, min + (step * count), count);
    }

    private static double NiceStep(double value)
    {
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        var normalized = value / magnitude;
        var nice = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 2.5 ? 2.5 : normalized <= 5 ? 5 : 10;
        return nice * magnitude;
    }

    public static string FormatAxis(double value) =>
        Math.Abs(value) >= 1e6
            ? (value / 1e6).ToString("0.##M", CultureInfo.InvariantCulture)
            : Math.Abs(value) >= 1e3
                ? (value / 1e3).ToString("0.##k", CultureInfo.InvariantCulture)
                : value.ToString("0.##", CultureInfo.InvariantCulture);

    public static FormattedText CreateText(string text, double size, IBrush brush, Typeface? typeface = null) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface ?? Typeface.Default, size, brush);

    public static void DrawAxis(DrawingContext context, Rect plot, double minimum, double maximum, int divisions, IBrush textBrush, IBrush gridBrush)
    {
        var pen = new Pen(gridBrush);
        for (var i = 0; i <= divisions; i++)
        {
            var y = Math.Round(plot.Bottom - (plot.Height * i / divisions)) + 0.5;
            context.DrawLine(pen, new Point(plot.Left, y), new Point(plot.Right, y));
            var label = CreateText(FormatAxis(minimum + ((maximum - minimum) * i / divisions)), 11, textBrush);
            context.DrawText(label, new Point(plot.Left - label.Width - 4, y - (label.Height / 2)));
        }
    }

    public static void DrawTimeAxis(DrawingContext context, Rect plot, DateTimeOffset end, TimeSpan span, IBrush textBrush, IBrush gridBrush)
    {
        if ((plot.Width <= 0) || (span <= TimeSpan.Zero))
        {
            return;
        }

        var minimum = span * ((CreateText("00:00:00", 11, textBrush).Width + 16) / plot.Width);
        var step = TimeSteps.FirstOrDefault(x => x >= minimum, TimeSteps[^1]);
        var format = step >= TimeSpan.FromMinutes(1) ? "HH:mm" : "HH:mm:ss";
        var pen = new Pen(gridBrush);
        var start = end - span;
        for (var ticks = ((start.Ticks / step.Ticks) + 1) * step.Ticks; ticks <= end.Ticks; ticks += step.Ticks)
        {
            var x = Math.Round(plot.Left + (plot.Width * (ticks - start.Ticks) / span.Ticks)) + 0.5;
            context.DrawLine(pen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var label = CreateText(new DateTime(ticks, DateTimeKind.Unspecified).ToString(format, CultureInfo.InvariantCulture), 11, textBrush);
            var left = x - (label.Width / 2);
            if ((left >= plot.Left - 2) && (left + label.Width <= plot.Right + 4))
            {
                context.DrawText(label, new Point(left, plot.Bottom + 1));
            }
        }
    }

    public static IEnumerable<List<Point>> BuildSegments(IReadOnlyList<double> values, Rect plot, int capacity, double minimum, double maximum)
    {
        var count = values.Count;
        var slots = Math.Max(capacity, count);
        var step = slots > 1 ? plot.Width / (slots - 1) : 0;
        var offset = slots - count;
        var segment = new List<Point>();
        for (var i = 0; i < count; i++)
        {
            var value = values[i];
            if (!Double.IsFinite(value))
            {
                if (segment.Count > 0)
                {
                    yield return segment;
                    segment = [];
                }

                continue;
            }

            var ratio = Math.Clamp((value - minimum) / (maximum - minimum), 0d, 1d);
            segment.Add(new Point(plot.Left + ((offset + i) * step), plot.Bottom - (ratio * plot.Height)));
        }

        if (segment.Count > 0)
        {
            yield return segment;
        }
    }
}

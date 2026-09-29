namespace Template.LinuxApp.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

public sealed class AnimatedWave : Control
{
    private const int Samples = 160;

    private const int ColumnDivisions = 8;

    private const int RowDivisions = 4;

    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<AnimatedWave, IBrush?>(nameof(Stroke), Brushes.DodgerBlue);

    public static readonly StyledProperty<IBrush?> SecondaryStrokeProperty = AvaloniaProperty.Register<AnimatedWave, IBrush?>(nameof(SecondaryStroke), Brushes.Orange);

    public static readonly StyledProperty<IBrush?> GridBrushProperty = AvaloniaProperty.Register<AnimatedWave, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<double> CyclesProperty = AvaloniaProperty.Register<AnimatedWave, double>(nameof(Cycles), 2);

    public static readonly StyledProperty<double> FrequencyProperty = AvaloniaProperty.Register<AnimatedWave, double>(nameof(Frequency), 0.5);

    private TopLevel? topLevel;

    private TimeSpan? startTime;

    private double elapsed;

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush? SecondaryStroke
    {
        get => GetValue(SecondaryStrokeProperty);
        set => SetValue(SecondaryStrokeProperty, value);
    }

    public IBrush? GridBrush
    {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public double Cycles
    {
        get => GetValue(CyclesProperty);
        set => SetValue(CyclesProperty, value);
    }

    public double Frequency
    {
        get => GetValue(FrequencyProperty);
        set => SetValue(FrequencyProperty, value);
    }

    static AnimatedWave()
    {
        AffectsRender<AnimatedWave>(StrokeProperty, SecondaryStrokeProperty, GridBrushProperty, CyclesProperty, FrequencyProperty);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        topLevel = TopLevel.GetTopLevel(this);
        startTime = null;
        topLevel?.RequestAnimationFrame(OnFrame);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if ((width <= 0) || (height <= 0))
        {
            return;
        }

        if (GridBrush is { } gridBrush)
        {
            var gridPen = new Pen(gridBrush);
            for (var i = 0; i <= ColumnDivisions; i++)
            {
                var x = Math.Round(width * i / ColumnDivisions);
                context.DrawLine(gridPen, new Point(x, 0), new Point(x, height));
            }

            for (var i = 0; i <= RowDivisions; i++)
            {
                var y = Math.Round(height * i / RowDivisions);
                context.DrawLine(gridPen, new Point(0, y), new Point(width, y));
            }
        }

        var phase = 2 * Math.PI * Frequency * elapsed;
        DrawWave(context, SecondaryStroke, width, height, Cycles * 2, -phase * 1.5, 0.35);
        DrawWave(context, Stroke, width, height, Cycles, phase, 0.8);
    }

    private void OnFrame(TimeSpan time)
    {
        if (topLevel is null)
        {
            return;
        }

        startTime ??= time;
        elapsed = (time - startTime.Value).TotalSeconds;
        InvalidateVisual();
        topLevel.RequestAnimationFrame(OnFrame);
    }

    private static void DrawWave(DrawingContext context, IBrush? brush, double width, double height, double cycles, double phase, double amplitude)
    {
        if (brush is null)
        {
            return;
        }

        var middle = height / 2;
        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            for (var i = 0; i <= Samples; i++)
            {
                var ratio = (double)i / Samples;
                var point = new Point(ratio * width, middle - (Math.Sin((2 * Math.PI * cycles * ratio) - phase) * amplitude * middle));
                if (i == 0)
                {
                    stream.BeginFigure(point, false);
                }
                else
                {
                    stream.LineTo(point);
                }
            }

            stream.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(brush, 2, lineJoin: PenLineJoin.Round), geometry);
    }
}

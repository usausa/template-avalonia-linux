namespace Template.LinuxApp.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

public sealed class FrameRateCounter : Control
{
    private static readonly TimeSpan MeasureInterval = TimeSpan.FromSeconds(1);

    public static readonly StyledProperty<double> FontSizeProperty = AvaloniaProperty.Register<FrameRateCounter, double>(nameof(FontSize), 14);

    public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<FrameRateCounter, IBrush?>(nameof(Foreground), Brushes.Gray);

    private TopLevel? topLevel;

    private TimeSpan? measureStart;

    private int frames;

    private int framesPerSecond;

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    static FrameRateCounter()
    {
        AffectsMeasure<FrameRateCounter>(FontSizeProperty);
        AffectsRender<FrameRateCounter>(ForegroundProperty);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        topLevel = TopLevel.GetTopLevel(this);
        measureStart = null;
        frames = 0;
        topLevel?.RequestAnimationFrame(OnFrame);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var text = CreateText("000 fps");
        return new Size(text.Width, text.Height);
    }

    public override void Render(DrawingContext context)
    {
        context.DrawText(CreateText(String.Create(CultureInfo.InvariantCulture, $"{framesPerSecond} fps")), default);
    }

    private void OnFrame(TimeSpan time)
    {
        if (topLevel is null)
        {
            return;
        }

        frames++;
        measureStart ??= time;
        var elapsed = time - measureStart.Value;
        if (elapsed >= MeasureInterval)
        {
            framesPerSecond = (int)Math.Round(frames / elapsed.TotalSeconds);
            frames = 0;
            measureStart = time;
            InvalidateVisual();
        }

        topLevel.RequestAnimationFrame(OnFrame);
    }

    private FormattedText CreateText(string text) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, FontSize, Foreground);
}

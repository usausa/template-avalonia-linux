namespace Template.LinuxApp.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;

using SkiaSharp;

public sealed class SpeedGauge : Control
{
    public static readonly StyledProperty<int> SpeedProperty = AvaloniaProperty.Register<SpeedGauge, int>(nameof(Speed));

    public static readonly StyledProperty<int> MaxSpeedProperty = AvaloniaProperty.Register<SpeedGauge, int>(nameof(MaxSpeed), 255);

    public static readonly StyledProperty<int> GaugeWidthProperty = AvaloniaProperty.Register<SpeedGauge, int>(nameof(GaugeWidth), 32);

    public static readonly StyledProperty<Color> BackgroundColorProperty = AvaloniaProperty.Register<SpeedGauge, Color>(nameof(BackgroundColor), Colors.Black);

    public static readonly StyledProperty<Color> ColorProperty = AvaloniaProperty.Register<SpeedGauge, Color>(nameof(Color), Colors.LightGreen);

    public int Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public int MaxSpeed
    {
        get => GetValue(MaxSpeedProperty);
        set => SetValue(MaxSpeedProperty, value);
    }

    public int GaugeWidth
    {
        get => GetValue(GaugeWidthProperty);
        set => SetValue(GaugeWidthProperty, value);
    }

    public Color BackgroundColor
    {
        get => GetValue(BackgroundColorProperty);
        set => SetValue(BackgroundColorProperty, value);
    }

    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    static SpeedGauge()
    {
        AffectsRender<SpeedGauge>(SpeedProperty, MaxSpeedProperty, GaugeWidthProperty, BackgroundColorProperty, ColorProperty);
    }

    public override void Render(DrawingContext context)
    {
        var value = MaxSpeed > 0 ? Math.Clamp((float)Speed / MaxSpeed, 0f, 1f) : 0f;
        using var operation = new GaugeDrawOperation(new Rect(Bounds.Size), value, GaugeWidth, BackgroundColor.ToSKColor(), Color.ToSKColor());
        context.Custom(operation);
    }

    private sealed class GaugeDrawOperation : ICustomDrawOperation
    {
        private const float StartAngle = -210f;

        private const float SweepAngle = 240f;

        private readonly float value;

        private readonly float strokeWidth;

        private readonly SKColor backgroundColor;

        private readonly SKColor color;

        public Rect Bounds { get; }

        public GaugeDrawOperation(Rect bounds, float value, float strokeWidth, SKColor backgroundColor, SKColor color)
        {
            Bounds = bounds;
            this.value = value;
            this.strokeWidth = strokeWidth;
            this.backgroundColor = backgroundColor;
            this.color = color;
        }

        public void Dispose()
        {
        }

        public bool HitTest(Point p) => false;

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null)
            {
                return;
            }

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            var centerX = (float)(Bounds.Width / 2);
            var centerY = (float)(Bounds.Height / 2);
            var radius = (float)Math.Min(Bounds.Width, Bounds.Height) / 2;
            var margin = strokeWidth / 2;
            var rect = new SKRect(centerX - radius + margin, centerY - radius + margin, centerX + radius - margin, centerY + radius - margin);

            using var paint = new SKPaint();
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = strokeWidth;
            paint.IsAntialias = true;

            using var path = new SKPath();
            paint.Color = backgroundColor;
            path.AddArc(rect, StartAngle, SweepAngle);
            canvas.DrawPath(path, paint);

            path.Reset();
            paint.Color = color;
            path.AddArc(rect, StartAngle, SweepAngle * value);
            canvas.DrawPath(path, paint);
        }
    }
}

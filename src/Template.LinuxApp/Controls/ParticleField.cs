namespace Template.LinuxApp.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

public sealed class ParticleField : Control
{
    private const int LinkLevels = 4;

    private const double LinkOpacityStep = 0.15;

    private const double GoldenRatio = 0.6180339887498949;

    private const double PlasticRatio = 0.7548776662466927;

    private const double GoldenAngle = 2.399963229728653;

    private const double BaseSpeed = 24;

    private const double SpeedStep = 8;

    private const double MaxFrameSeconds = 0.05;

    public static readonly StyledProperty<int> CountProperty = AvaloniaProperty.Register<ParticleField, int>(nameof(Count), 120);

    public static readonly StyledProperty<double> LinkDistanceProperty = AvaloniaProperty.Register<ParticleField, double>(nameof(LinkDistance), 110);

    public static readonly StyledProperty<double> RadiusProperty = AvaloniaProperty.Register<ParticleField, double>(nameof(Radius), 3);

    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<ParticleField, IBrush?>(nameof(Fill), Brushes.DodgerBlue);

    public static readonly StyledProperty<IBrush?> LinkBrushProperty = AvaloniaProperty.Register<ParticleField, IBrush?>(nameof(LinkBrush), Brushes.DodgerBlue);

    private Point[] positions = [];

    private Vector[] velocities = [];

    private Size fieldSize;

    private IPen[]? linkPens;

    private TopLevel? topLevel;

    private TimeSpan? lastTime;

    public int Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public double LinkDistance
    {
        get => GetValue(LinkDistanceProperty);
        set => SetValue(LinkDistanceProperty, value);
    }

    public double Radius
    {
        get => GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public IBrush? LinkBrush
    {
        get => GetValue(LinkBrushProperty);
        set => SetValue(LinkBrushProperty, value);
    }

    static ParticleField()
    {
        AffectsRender<ParticleField>(CountProperty, LinkDistanceProperty, RadiusProperty, FillProperty, LinkBrushProperty);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        topLevel = TopLevel.GetTopLevel(this);
        lastTime = null;
        topLevel?.RequestAnimationFrame(OnFrame);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LinkBrushProperty)
        {
            linkPens = null;
        }
        else if (change.Property == CountProperty)
        {
            positions = [];
        }
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if ((size.Width <= 0) || (size.Height <= 0))
        {
            return;
        }

        EnsureParticles(size);

        var pens = GetPens();
        var distance = LinkDistance;
        var limit = distance * distance;
        for (var i = 0; i < positions.Length; i++)
        {
            for (var j = i + 1; j < positions.Length; j++)
            {
                var delta = positions[i] - positions[j];
                var squared = (delta.X * delta.X) + (delta.Y * delta.Y);
                if (squared < limit)
                {
                    var level = Math.Min(LinkLevels - 1, (int)((1 - (Math.Sqrt(squared) / distance)) * LinkLevels));
                    context.DrawLine(pens[level], positions[i], positions[j]);
                }
            }
        }

        var fill = Fill;
        var radius = Radius;
        foreach (var position in positions)
        {
            context.DrawEllipse(fill, null, position, radius, radius);
        }
    }

    private void OnFrame(TimeSpan time)
    {
        if (topLevel is null)
        {
            return;
        }

        var seconds = lastTime is { } last ? Math.Min((time - last).TotalSeconds, MaxFrameSeconds) : 0;
        lastTime = time;
        Move(seconds);
        InvalidateVisual();
        topLevel.RequestAnimationFrame(OnFrame);
    }

    private void EnsureParticles(Size size)
    {
        var count = Math.Max(0, Count);
        if (positions.Length == count)
        {
            if (size != fieldSize)
            {
                var scaleX = size.Width / fieldSize.Width;
                var scaleY = size.Height / fieldSize.Height;
                for (var i = 0; i < positions.Length; i++)
                {
                    positions[i] = new Point(positions[i].X * scaleX, positions[i].Y * scaleY);
                }

                fieldSize = size;
            }

            return;
        }

        fieldSize = size;
        positions = new Point[count];
        velocities = new Vector[count];
        for (var i = 0; i < count; i++)
        {
            positions[i] = new Point(((i * GoldenRatio) % 1) * size.Width, ((i * PlasticRatio) % 1) * size.Height);
            var angle = i * GoldenAngle;
            var speed = BaseSpeed + ((i % 7) * SpeedStep);
            velocities[i] = new Vector(Math.Cos(angle) * speed, Math.Sin(angle) * speed);
        }
    }

    private void Move(double seconds)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        for (var i = 0; i < positions.Length; i++)
        {
            var velocity = velocities[i];
            var x = positions[i].X + (velocity.X * seconds);
            var y = positions[i].Y + (velocity.Y * seconds);
            if ((x < 0) || (x > width))
            {
                velocity = new Vector(-velocity.X, velocity.Y);
                x = Math.Clamp(x, 0, width);
            }

            if ((y < 0) || (y > height))
            {
                velocity = new Vector(velocity.X, -velocity.Y);
                y = Math.Clamp(y, 0, height);
            }

            positions[i] = new Point(x, y);
            velocities[i] = velocity;
        }
    }

    private IPen[] GetPens()
    {
        if (linkPens is null)
        {
            var color = (LinkBrush as ISolidColorBrush)?.Color ?? Colors.DodgerBlue;
            linkPens = new IPen[LinkLevels];
            for (var i = 0; i < LinkLevels; i++)
            {
                linkPens[i] = new ImmutablePen(new ImmutableSolidColorBrush(color, (i + 1) * LinkOpacityStep));
            }
        }

        return linkPens;
    }
}

namespace Template.LinuxApp.Controls;

using System.Collections.Specialized;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using Template.LinuxApp.Components.Video;

public sealed class FaceBoxOverlay : Control
{
    public static readonly StyledProperty<IEnumerable<FaceBox>?> FaceBoxesProperty =
        AvaloniaProperty.Register<FaceBoxOverlay, IEnumerable<FaceBox>?>(nameof(FaceBoxes));

    public static readonly StyledProperty<Color> LowScoreColorProperty =
        AvaloniaProperty.Register<FaceBoxOverlay, Color>(nameof(LowScoreColor), Colors.Yellow);

    public static readonly StyledProperty<Color> HighScoreColorProperty =
        AvaloniaProperty.Register<FaceBoxOverlay, Color>(nameof(HighScoreColor), Colors.Red);

    public IEnumerable<FaceBox>? FaceBoxes
    {
        get => GetValue(FaceBoxesProperty);
        set => SetValue(FaceBoxesProperty, value);
    }

    public Color LowScoreColor
    {
        get => GetValue(LowScoreColorProperty);
        set => SetValue(LowScoreColorProperty, value);
    }

    public Color HighScoreColor
    {
        get => GetValue(HighScoreColorProperty);
        set => SetValue(HighScoreColorProperty, value);
    }

    static FaceBoxOverlay()
    {
        AffectsRender<FaceBoxOverlay>(FaceBoxesProperty, LowScoreColorProperty, HighScoreColorProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == FaceBoxesProperty)
        {
            if (change.OldValue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= OnFaceBoxesChanged;
            }

            if (change.NewValue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += OnFaceBoxesChanged;
            }
        }
    }

    private void OnFaceBoxesChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (FaceBoxes is null)
        {
            return;
        }

        var width = Bounds.Width;
        var height = Bounds.Height;
        foreach (var faceBox in FaceBoxes)
        {
            var left = faceBox.Left * width;
            var top = faceBox.Top * height;
            var right = faceBox.Right * width;
            var bottom = faceBox.Bottom * height;

            var brush = new SolidColorBrush(InterpolateColor(LowScoreColor, HighScoreColor, faceBox.Confidence));
            context.DrawRectangle(null, new Pen(brush, 2), new Rect(left, top, right - left, bottom - top));

            var text = new FormattedText(
                faceBox.Confidence.ToString("P1", CultureInfo.CurrentCulture),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                12,
                brush);
            context.DrawText(text, new Point(right - text.Width - 4, bottom - text.Height - 4));
        }
    }

    private static Color InterpolateColor(Color low, Color high, float ratio) =>
        Color.FromArgb(
            (byte)(low.A + ((high.A - low.A) * ratio)),
            (byte)(low.R + ((high.R - low.R) * ratio)),
            (byte)(low.G + ((high.G - low.G) * ratio)),
            (byte)(low.B + ((high.B - low.B) * ratio)));
}

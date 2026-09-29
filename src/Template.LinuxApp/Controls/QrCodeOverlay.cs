namespace Template.LinuxApp.Controls;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using Template.LinuxApp.Components.Video;

public sealed class QrCodeOverlay : Control
{
    private const int MaxLabelLength = 40;

    public static readonly StyledProperty<IReadOnlyList<QrCodeBox>?> CodesProperty = AvaloniaProperty.Register<QrCodeOverlay, IReadOnlyList<QrCodeBox>?>(nameof(Codes));

    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<QrCodeOverlay, IBrush?>(nameof(Stroke), Brushes.LimeGreen);

    public IReadOnlyList<QrCodeBox>? Codes
    {
        get => GetValue(CodesProperty);
        set => SetValue(CodesProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    static QrCodeOverlay()
    {
        AffectsRender<QrCodeOverlay>(CodesProperty, StrokeProperty);
    }

    public override void Render(DrawingContext context)
    {
        if (Codes is not { Count: > 0 } codes)
        {
            return;
        }

        var width = Bounds.Width;
        var height = Bounds.Height;
        var stroke = Stroke ?? Brushes.LimeGreen;
        var pen = new Pen(stroke, 2);
        foreach (var code in codes)
        {
            var rect = new Rect(code.Left * width, code.Top * height, (code.Right - code.Left) * width, (code.Bottom - code.Top) * height);
            context.DrawRectangle(null, pen, rect);

            var text = code.Text.Length > MaxLabelLength ? code.Text[..MaxLabelLength] + "…" : code.Text;
            var label = ChartHelper.CreateText(text, 12, Brushes.White);
            var top = rect.Top - label.Height - 2 >= 0 ? rect.Top - label.Height - 2 : rect.Bottom + 2;
            context.DrawRectangle(stroke, null, new Rect(rect.Left, top, label.Width + 6, label.Height + 2));
            context.DrawText(label, new Point(rect.Left + 3, top + 1));
        }
    }
}

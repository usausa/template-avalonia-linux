namespace Template.LinuxApp.Helpers;

using SkiaSharp;

public static class ReceiptHelper
{
    private const string StoreName = "TEMPLATE STORE";

    private const string ReceiptNumber = "0001";

    private const string QrText = "https://example.com/receipt/" + ReceiptNumber;

    private const int TextColumns = 32;

    private const int LabelWidth = 320;

    private const int LabelHeight = 240;

    private const int QrModule = 6;

    private const float LabelMargin = 18;

    private const float TextMargin = 10;

    private const float LineSpacing = 6;

    private static readonly (string Name, int Price)[] Items =
    [
        ("Coffee", 450),
        ("Sandwich", 380),
        ("Cookie", 200)
    ];

    public static string CreateText(DateTimeOffset now)
    {
        var rule = new string('-', TextColumns);
        var builder = new StringBuilder();
        AppendLine(builder, Center(StoreName));
        AppendLine(builder, Center(now.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture)));
        AppendLine(builder, rule);
        foreach (var (name, price) in Items)
        {
            AppendLine(builder, Justify(name, FormatPrice(price)));
        }

        AppendLine(builder, rule);
        AppendLine(builder, Justify("TOTAL", FormatPrice(Items.Sum(static x => x.Price))));
        AppendLine(builder, Center("Thank you"));
        return builder.ToString();
    }

    public static byte[] CreatePng(DateTimeOffset now)
    {
        using var bitmap = new SKBitmap(LabelWidth, LabelHeight);
        using (var canvas = new SKCanvas(bitmap))
        {
            Draw(canvas, now);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void Draw(SKCanvas canvas, DateTimeOffset now)
    {
        canvas.Clear(SKColors.White);

        using var qr = SkiaHelper.CreateQrBitmap(QrText, QrModule, false);
        canvas.DrawBitmap(qr, LabelMargin, (LabelHeight - qr.Height) / 2f);

        var left = LabelMargin + qr.Width + LabelMargin;
        var width = LabelWidth - left - TextMargin;
        using var paint = new SKPaint();
        paint.Color = SKColors.Black;
        paint.IsAntialias = true;
        using var regular = SKFontManager.Default.MatchCharacter("sans-serif", 'A');
        using var bold = regular is null ? null : SKFontManager.Default.MatchFamily(regular.FamilyName, SKFontStyle.Bold);
        var date = now.ToString("MM/dd", CultureInfo.InvariantCulture);
        var time = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        using var labelFont = CreateFont(regular, "No.", width, 24);
        using var numberFont = CreateFont(bold ?? regular, ReceiptNumber, width, 64);
        using var dateFont = CreateFont(regular, date, width, 32);
        using var timeFont = CreateFont(regular, time, width, 32);
        (string Text, SKFont Font)[] lines =
        [
            ("No.", labelFont),
            (ReceiptNumber, numberFont),
            (date, dateFont),
            (time, timeFont)
        ];

        var height = lines.Sum(static x => x.Font.Metrics.Descent - x.Font.Metrics.Ascent) + (LineSpacing * (lines.Length - 1));
        var center = left + (width / 2);
        var y = (LabelHeight - height) / 2;
        foreach (var (text, font) in lines)
        {
            y -= font.Metrics.Ascent;
            canvas.DrawText(text, center, y, SKTextAlign.Center, font, paint);
            y += font.Metrics.Descent + LineSpacing;
        }
    }

    private static SKFont CreateFont(SKTypeface? typeface, string text, float width, float size)
    {
        var font = new SKFont(typeface, size);
        var measured = font.MeasureText(text);
        if (measured > width)
        {
            font.Size = size * width / measured;
        }

        return font;
    }

    private static void AppendLine(StringBuilder builder, string line) => builder.Append(line).Append('\n');

    private static string Center(string text) => text.PadLeft((TextColumns + text.Length) / 2);

    private static string Justify(string left, string right) => left + right.PadLeft(TextColumns - left.Length);

    private static string FormatPrice(int price) => price.ToString("#,0", CultureInfo.InvariantCulture);
}

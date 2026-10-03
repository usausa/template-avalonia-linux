namespace Template.LinuxApp.Helpers;

using SkiaSharp;

using ZXing;

public static class ReceiptHelper
{
    private const string StoreName = "TEMPLATE STORE";

    private const string JanCode = "4901234567894";

    private const string QrText = "https://example.com/receipt/0001";

    private const int TextColumns = 32;

    private const int ImageWidth = 800;

    private const int ImageHeight = 1200;

    private const float Margin = 60;

    private const float BarcodeModule = 4;

    private const float BarcodeHeight = 120;

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
        using var bitmap = new SKBitmap(ImageWidth, ImageHeight);
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

        using var paint = new SKPaint();
        paint.Color = SKColors.Black;
        paint.IsAntialias = true;
        using var rulePaint = new SKPaint();
        rulePaint.Color = SKColors.Black;
        rulePaint.StrokeWidth = 2;
        rulePaint.PathEffect = SKPathEffect.CreateDash([12, 8], 0);
        using var regular = SKFontManager.Default.MatchCharacter("sans-serif", 'A');
        using var bold = regular is null ? null : SKFontManager.Default.MatchFamily(regular.FamilyName, SKFontStyle.Bold);
        using var titleFont = new SKFont(bold ?? regular, 56);
        using var totalFont = new SKFont(bold ?? regular, 44);
        using var textFont = new SKFont(regular, 32);

        var center = ImageWidth / 2f;
        var y = Margin + 56;
        canvas.DrawText(StoreName, center, y, SKTextAlign.Center, titleFont, paint);
        y += 52;
        canvas.DrawText(now.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture), center, y, SKTextAlign.Center, textFont, paint);
        y += 40;
        canvas.DrawLine(Margin, y, ImageWidth - Margin, y, rulePaint);
        y += 56;
        foreach (var (name, price) in Items)
        {
            canvas.DrawText(name, Margin, y, SKTextAlign.Left, textFont, paint);
            canvas.DrawText($"¥{FormatPrice(price)}", ImageWidth - Margin, y, SKTextAlign.Right, textFont, paint);
            y += 48;
        }

        canvas.DrawLine(Margin, y - 16, ImageWidth - Margin, y - 16, rulePaint);
        y += 48;
        canvas.DrawText("TOTAL", Margin, y, SKTextAlign.Left, totalFont, paint);
        canvas.DrawText($"¥{FormatPrice(Items.Sum(static x => x.Price))}", ImageWidth - Margin, y, SKTextAlign.Right, totalFont, paint);
        y += 56;
        DrawBarcode(canvas, y, textFont, paint);
        y += BarcodeHeight + 72;

        using var qr = SkiaHelper.CreateQrBitmap(QrText, 8);
        canvas.DrawBitmap(qr, (ImageWidth - qr.Width) / 2f, y);
        y += qr.Height + 48;
        canvas.DrawText("Thank you", center, y, SKTextAlign.Center, textFont, paint);
    }

    private static void DrawBarcode(SKCanvas canvas, float top, SKFont font, SKPaint paint)
    {
        var matrix = new MultiFormatWriter().encode(JanCode, BarcodeFormat.EAN_13, 0, 0, new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 0 });
        var left = (ImageWidth - (matrix.Width * BarcodeModule)) / 2f;
        for (var x = 0; x < matrix.Width; x++)
        {
            if (matrix[x, 0])
            {
                canvas.DrawRect(left + (x * BarcodeModule), top, BarcodeModule, BarcodeHeight, paint);
            }
        }

        canvas.DrawText(JanCode, ImageWidth / 2f, top + BarcodeHeight + 36, SKTextAlign.Center, font, paint);
    }

    private static void AppendLine(StringBuilder builder, string line) => builder.Append(line).Append('\n');

    private static string Center(string text) => text.PadLeft((TextColumns + text.Length) / 2);

    private static string Justify(string left, string right) => left + right.PadLeft(TextColumns - left.Length);

    private static string FormatPrice(int price) => price.ToString("#,0", CultureInfo.InvariantCulture);
}

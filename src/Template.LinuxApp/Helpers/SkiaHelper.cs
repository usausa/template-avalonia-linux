namespace Template.LinuxApp.Helpers;

using QRCoder;

using SkiaSharp;

public static class SkiaHelper
{
    public static SKBitmap CreateQrBitmap(string text, int pixelPerModule)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        using var code = new PngByteQRCode(data);
        return SKBitmap.Decode(code.GetGraphic(pixelPerModule));
    }
}

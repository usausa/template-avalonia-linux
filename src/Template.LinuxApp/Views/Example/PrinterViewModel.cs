namespace Template.LinuxApp.Views.Example;

using SkiaSharp;

using Template.LinuxApp.Components.Printer;
using Template.LinuxApp.Helpers;
using Template.LinuxApp.Services;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class PrinterViewModel : AppViewModelBase
{
    private const int ImageWidth = 800;

    private const int ImageHeight = 600;

    public ICommand PrintTextCommand { get; }

    public ICommand PrintImageCommand { get; }

    public PrinterViewModel(ILinePrinter linePrinter, IImagePrinter imagePrinter, IDialogService dialogService)
    {
        PrintTextCommand = MakeAsyncCommand(async () =>
        {
            if (!await linePrinter.PrintAsync("TEST1234567890\n"u8.ToArray()))
            {
                await dialogService.NotifyAsync("Printing failed.");
            }
        });
        PrintImageCommand = MakeAsyncCommand(async () =>
        {
            using var stream = CreateImage("TEST1234567890");
            if (!await imagePrinter.PrintAsync(stream))
            {
                await dialogService.NotifyAsync("Printing failed.");
            }
        });
    }

    private static MemoryStream CreateImage(string text)
    {
        using var bitmap = new SKBitmap(ImageWidth, ImageHeight);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint();
        paint.Color = SKColors.Black;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 2;
        paint.IsAntialias = true;

        canvas.Clear(SKColors.White);
        canvas.DrawRect(50, 50, ImageWidth - 100, ImageHeight - 100, paint);

        using var qr = SkiaHelper.CreateQrBitmap(text, 16);
        canvas.DrawBitmap(qr, (ImageWidth - qr.Width) / 2f, (ImageHeight - qr.Height) / 2f);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new MemoryStream(data.ToArray());
    }
}

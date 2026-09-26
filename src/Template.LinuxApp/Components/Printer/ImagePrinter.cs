namespace Template.LinuxApp.Components.Printer;

using LinuxDotNet.Cups;

using Template.LinuxApp.State;

public interface IImagePrinter
{
    ValueTask<bool> PrintAsync(Stream image);
}

public sealed class ImagePrinter : IImagePrinter
{
    private readonly ImagePrinterOption option;

    private readonly DeviceStatus status;

    public ImagePrinter(ImagePrinterOption option, DeviceState deviceState)
    {
        this.option = option;
        status = deviceState.Register("Image printer", !String.IsNullOrEmpty(option.ImagePrinterName));
    }

    public async ValueTask<bool> PrintAsync(Stream image)
    {
        if (!status.IsEnabled)
        {
            return false;
        }

        var options = new PrintOptions
        {
            Printer = option.ImagePrinterName,
            Copies = 1,
            MediaSize = "A4",
            ColorMode = true,
            Orientation = PrintOrientation.Portrait,
            Quality = PrintQuality.Normal
        };
        try
        {
            await Task.Run(() => CupsPrinter.PrintStreamAsync(image, options)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or DllNotFoundException)
        {
            status.ReportDisconnected();
            status.ReportError(ex.Message);
            return false;
        }

        status.ReportConnected();
        status.ReportEvent();
        return true;
    }
}

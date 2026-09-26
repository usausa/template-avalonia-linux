namespace Template.LinuxApp.Components.Printer;

using Template.LinuxApp.State;

public interface ILinePrinter
{
    ValueTask<bool> PrintAsync(ReadOnlyMemory<byte> data);
}

public sealed class LinePrinter : ILinePrinter
{
    private readonly LinePrinterOption option;

    private readonly DeviceStatus status;

    public LinePrinter(LinePrinterOption option, DeviceState deviceState)
    {
        this.option = option;
        status = deviceState.Register("Line printer", !String.IsNullOrEmpty(option.LinePrinterDevice));
    }

    public async ValueTask<bool> PrintAsync(ReadOnlyMemory<byte> data)
    {
        if (!status.IsEnabled)
        {
            return false;
        }

        try
        {
            await Task.Run(() =>
            {
                using var stream = new FileStream(option.LinePrinterDevice, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                stream.Write(data.Span);
                stream.Flush(true);
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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

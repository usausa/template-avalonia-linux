namespace Template.LinuxApp.Components.Printer;

using Template.LinuxApp.State;

public interface ILinePrinter
{
    DeviceStatus Status { get; }

    string Device { get; }

    void Start();

    ValueTask StopAsync();

    ValueTask<bool> PrintAsync(ReadOnlyMemory<byte> data);
}

public sealed class LinePrinter : ILinePrinter, IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);

    private readonly TimeProvider timeProvider;

    private readonly LinePrinterOption option;

    private CancellationTokenSource? cts;

    private Task? loopTask;

    public DeviceStatus Status { get; }

    public string Device => option.LinePrinterDevice;

    public LinePrinter(TimeProvider timeProvider, LinePrinterOption option, DeviceState deviceState)
    {
        this.timeProvider = timeProvider;
        this.option = option;
        Status = deviceState.Register("Line printer", !String.IsNullOrEmpty(option.LinePrinterDevice));
    }

    public void Dispose()
    {
        StopAsync().AsTask().GetAwaiter().GetResult();
    }

    public void Start()
    {
        if (!Status.IsEnabled || (loopTask is not null))
        {
            return;
        }

        cts = new CancellationTokenSource();
        var token = cts.Token;
        loopTask = Task.Run(() => LoopAsync(token), token);
        Status.ReportStarted();
    }

    public async ValueTask StopAsync()
    {
        if ((cts is null) || (loopTask is null))
        {
            return;
        }

        await cts.CancelAsync().ConfigureAwait(false);
        try
        {
            await loopTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        cts.Dispose();
        cts = null;
        loopTask = null;
        Status.ReportStopped();
    }

    public async ValueTask<bool> PrintAsync(ReadOnlyMemory<byte> data)
    {
        if (!Status.IsEnabled)
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
            Status.ReportDisconnected();
            Status.ReportError(ex.Message);
            return false;
        }

        Status.ReportConnected();
        Status.ReportEvent();
        return true;
    }

    private async Task LoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (File.Exists(option.LinePrinterDevice))
            {
                Status.ReportConnected();
            }
            else
            {
                Status.ReportDisconnected();
            }

            await Task.Delay(CheckInterval, timeProvider, token).ConfigureAwait(false);
        }
    }
}

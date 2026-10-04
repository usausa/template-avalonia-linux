namespace Template.LinuxApp.Components.Printer;

using LinuxDotNet.Cups;

using Template.LinuxApp.State;

public sealed record PrinterQueueInfo(PrinterState State, string MakeModel, string Message, string Reasons, bool IsAcceptingJobs);

public sealed class PrinterUpdatedEventArgs : EventArgs
{
    public PrinterQueueInfo Queue { get; }

    public IReadOnlyList<PrintJob> Jobs { get; }

    public PrinterUpdatedEventArgs(PrinterQueueInfo queue, IReadOnlyList<PrintJob> jobs)
    {
        Queue = queue;
        Jobs = jobs;
    }
}

public interface IImagePrinter
{
    event EventHandler<PrinterUpdatedEventArgs>? Updated;

    DeviceStatus Status { get; }

    string Device { get; }

    void Start();

    ValueTask StopAsync();

    ValueTask<int> PrintAsync(Stream image, string title);

    ValueTask<bool> CancelAsync(int jobId);
}

public sealed class ImagePrinter : IImagePrinter, IDisposable
{
    private const int MaxJobs = 20;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);

    private readonly TimeProvider timeProvider;

    private readonly ImagePrinterOption option;

    private CancellationTokenSource? cts;

    private Task? loopTask;

    public event EventHandler<PrinterUpdatedEventArgs>? Updated;

    public DeviceStatus Status { get; }

    public string Device => option.ImagePrinterName;

    public ImagePrinter(TimeProvider timeProvider, ImagePrinterOption option, DeviceState deviceState)
    {
        this.timeProvider = timeProvider;
        this.option = option;
        Status = deviceState.Register("Image printer", !String.IsNullOrEmpty(option.ImagePrinterName));
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

    public async ValueTask<int> PrintAsync(Stream image, string title)
    {
        if (!Status.IsEnabled)
        {
            return 0;
        }

        var options = new PrintOptions
        {
            Printer = option.ImagePrinterName,
            JobTitle = title,
            Copies = 1,
            MediaSize = String.IsNullOrEmpty(option.ImageMediaSize) ? null : option.ImageMediaSize,
            ColorMode = false,
            Orientation = PrintOrientation.Portrait,
            Quality = PrintQuality.Normal,
            CustomOptions =
            {
                ["print-scaling"] = "fit"
            }
        };
        int jobId;
        try
        {
            jobId = await Task.Run(() => CupsPrinter.PrintStreamAsync(image, options)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or DllNotFoundException)
        {
            Status.ReportDisconnected();
            Status.ReportError(ex.Message);
            return 0;
        }

        Status.ReportConnected();
        Status.ReportEvent();
        return jobId;
    }

    public async ValueTask<bool> CancelAsync(int jobId)
    {
        if (!Status.IsEnabled)
        {
            return false;
        }

        try
        {
            return await Task.Run(() => CupsPrinter.CancelJob(option.ImagePrinterName, jobId)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or DllNotFoundException)
        {
            Status.ReportError(ex.Message);
            return false;
        }
    }

    private async Task LoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var queue = ReadQueue();
                var jobs = CupsPrinter.GetJobs(option.ImagePrinterName)
                    .OrderByDescending(static x => x.JobId)
                    .Take(MaxJobs)
                    .ToList();
                if (queue.State == PrinterState.Unknown)
                {
                    Status.ReportDisconnected();
                }
                else
                {
                    Status.ReportConnected();
                }

                Updated?.Invoke(this, new PrinterUpdatedEventArgs(queue, jobs));
            }
            catch (InvalidOperationException ex)
            {
                Status.ReportDisconnected();
                Status.ReportError(ex.Message);
            }
            catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException)
            {
                Status.ReportError(ex.Message);
                return;
            }

            await Task.Delay(CheckInterval, timeProvider, token).ConfigureAwait(false);
        }
    }

    private PrinterQueueInfo ReadQueue()
    {
        var detail = CupsPrinter.GetPrinterDetail(option.ImagePrinterName);
        var attributes = CupsPrinter.GetPrinterAttributes(option.ImagePrinterName);
        var reasons = attributes.TryGetValue("printer-state-reasons", out var values)
            ? String.Join(", ", values.Where(static x => x != "none"))
            : string.Empty;
        return new PrinterQueueInfo(detail.State, detail.MakeModel, detail.StateMessage, reasons, detail.IsAcceptingJobs);
    }
}

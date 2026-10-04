namespace Template.LinuxApp.Views.Example;

using Avalonia.Media.Imaging;

using LinuxDotNet.Cups;

using Smart.Reactive;

using Template.LinuxApp.Components.Printer;
using Template.LinuxApp.Reports;
using Template.LinuxApp.State;

public enum PrintResult
{
    TextSent,
    TextFailed,
    ImageSent,
    ImageFailed,
    Canceled,
    CancelFailed
}

public sealed record PrintJobItem(int JobId, string Title, DateTime SubmitTime, PrintJobState State, bool CanCancel);

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class PrinterViewModel : AppViewModelBase
{
    private const string JobTitle = "Receipt sample";

    private readonly TimeProvider timeProvider;

    private readonly ILinePrinter linePrinter;

    private readonly IImagePrinter imagePrinter;

    private readonly ReceiptReportBuilder reportBuilder;

    private byte[] imageData = [];

    [ObservableProperty]
    public partial Bitmap? PreviewImage { get; set; }

    [ObservableProperty]
    public partial string PreviewText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial PrinterQueueInfo Queue { get; set; } = new(default, String.Empty, String.Empty, String.Empty, true);

    [ObservableProperty]
    public partial bool HasJobs { get; set; }

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset ResultTime { get; set; }

    [ObservableProperty]
    public partial PrintResult Result { get; set; }

    [ObservableProperty]
    public partial int? ResultJobId { get; set; }

    [ObservableProperty]
    public partial bool IsResultError { get; set; }

    public ObservableCollection<PrintJobItem> Jobs { get; } = [];

    public DeviceStatus LineStatus => linePrinter.Status;

    public string LineDevice => linePrinter.Device;

    public DeviceStatus ImageStatus => imagePrinter.Status;

    public string ImageDevice => imagePrinter.Device;

    public ICommand PrintTextCommand { get; }

    public ICommand PrintImageCommand { get; }

    public ICommand CancelJobCommand { get; }

    public PrinterViewModel(TimeProvider timeProvider, ILinePrinter linePrinter, IImagePrinter imagePrinter, ReceiptReportBuilder reportBuilder)
    {
        this.timeProvider = timeProvider;
        this.linePrinter = linePrinter;
        this.imagePrinter = imagePrinter;
        this.reportBuilder = reportBuilder;

        Disposables.Add(Observable
            .FromEventPattern<PrinterUpdatedEventArgs>(h => imagePrinter.Updated += h, h => imagePrinter.Updated -= h)
            .ObserveOnCurrentContext()
            .Subscribe(x => ApplyQueue(x.EventArgs)));

        PrintTextCommand = MakeAsyncCommand(PrintTextAsync);
        PrintImageCommand = MakeAsyncCommand(PrintImageAsync);
        CancelJobCommand = MakeAsyncCommand<int>(CancelJobAsync);

        UpdatePreview();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var image = PreviewImage;
            PreviewImage = null;
            image?.Dispose();
        }

        base.Dispose(disposing);
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        linePrinter.Start();
        imagePrinter.Start();
        return Task.CompletedTask;
    }

    public override async Task OnNavigatingFromAsync(INavigationContext context)
    {
        await linePrinter.StopAsync();
        await imagePrinter.StopAsync();
    }

    private void UpdatePreview()
    {
        (PreviewText, imageData) = reportBuilder.Build();

        var previous = PreviewImage;
        using (var stream = new MemoryStream(imageData))
        {
            PreviewImage = new Bitmap(stream);
        }

        previous?.Dispose();
    }

    private async Task PrintTextAsync()
    {
        UpdatePreview();
        var result = await linePrinter.PrintAsync(Encoding.ASCII.GetBytes(PreviewText + "\n\n\n"));
        ShowResult(result ? PrintResult.TextSent : PrintResult.TextFailed, null, !result);
    }

    private async Task PrintImageAsync()
    {
        UpdatePreview();
        using var stream = new MemoryStream(imageData);
        var jobId = await imagePrinter.PrintAsync(stream, JobTitle);
        ShowResult(jobId > 0 ? PrintResult.ImageSent : PrintResult.ImageFailed, jobId > 0 ? jobId : null, jobId <= 0);
    }

    private async Task CancelJobAsync(int jobId)
    {
        var result = await imagePrinter.CancelAsync(jobId);
        ShowResult(result ? PrintResult.Canceled : PrintResult.CancelFailed, jobId, !result);
    }

    private void ShowResult(PrintResult result, int? jobId, bool error)
    {
        ResultTime = timeProvider.GetLocalNow();
        Result = result;
        ResultJobId = jobId;
        IsResultError = error;
        HasResult = true;
    }

    private void ApplyQueue(PrinterUpdatedEventArgs args)
    {
        Queue = args.Queue;

        var items = args.Jobs
            .Select(static x => new PrintJobItem(
                x.JobId,
                x.Title,
                x.SubmitTime,
                x.State,
                x.State is PrintJobState.Pending or PrintJobState.Held or PrintJobState.Processing or PrintJobState.Stopped))
            .ToList();
        if (!items.SequenceEqual(Jobs))
        {
            Jobs.Clear();
            foreach (var item in items)
            {
                Jobs.Add(item);
            }
        }

        HasJobs = Jobs.Count > 0;
    }
}

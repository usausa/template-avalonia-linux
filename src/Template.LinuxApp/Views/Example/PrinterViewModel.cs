namespace Template.LinuxApp.Views.Example;

using Avalonia.Media.Imaging;

using LinuxDotNet.Cups;

using Smart.Reactive;

using Template.LinuxApp.Components.Printer;
using Template.LinuxApp.Helpers;
using Template.LinuxApp.State;

public sealed record PrintJobItem(int JobId, string Label, string Time, PrintJobState State, bool CanCancel);

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class PrinterViewModel : AppViewModelBase
{
    private const string JobTitle = "Receipt sample";

    private readonly TimeProvider timeProvider;

    private readonly ILinePrinter linePrinter;

    private readonly IImagePrinter imagePrinter;

    private byte[] imageData = [];

    [ObservableProperty]
    public partial Bitmap? PreviewImage { get; set; }

    [ObservableProperty]
    public partial string PreviewText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial PrinterState QueueState { get; set; }

    [ObservableProperty]
    public partial string QueueDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasJobs { get; set; }

    [ObservableProperty]
    public partial string? ResultText { get; set; }

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

    public PrinterViewModel(TimeProvider timeProvider, ILinePrinter linePrinter, IImagePrinter imagePrinter)
    {
        this.timeProvider = timeProvider;
        this.linePrinter = linePrinter;
        this.imagePrinter = imagePrinter;

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
        var now = timeProvider.GetLocalNow();
        PreviewText = ReceiptHelper.CreateText(now);
        imageData = ReceiptHelper.CreatePng(now);

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
        ShowResult(result ? "Sent to the line printer." : "Printing to the line printer failed.", !result);
    }

    private async Task PrintImageAsync()
    {
        UpdatePreview();
        using var stream = new MemoryStream(imageData);
        var jobId = await imagePrinter.PrintAsync(stream, JobTitle);
        ShowResult(jobId > 0 ? String.Create(CultureInfo.InvariantCulture, $"Sent to the image printer (job {jobId}).") : "Printing to the image printer failed.", jobId <= 0);
    }

    private async Task CancelJobAsync(int jobId)
    {
        var result = await imagePrinter.CancelAsync(jobId);
        ShowResult(result ? String.Create(CultureInfo.InvariantCulture, $"Canceled job {jobId}.") : String.Create(CultureInfo.InvariantCulture, $"Canceling job {jobId} failed."), !result);
    }

    private void ShowResult(string text, bool error)
    {
        ResultText = String.Create(CultureInfo.InvariantCulture, $"{timeProvider.GetLocalNow():HH:mm:ss}  {text}");
        IsResultError = error;
    }

    private void ApplyQueue(PrinterUpdatedEventArgs args)
    {
        QueueState = args.Queue.State;
        List<string> parts = [args.Queue.MakeModel, args.Queue.Reasons, args.Queue.Message];
        if (!args.Queue.IsAcceptingJobs)
        {
            parts.Add("Not accepting jobs");
        }

        QueueDetail = String.Join("  ", parts.Where(static x => !String.IsNullOrEmpty(x)));

        var items = args.Jobs
            .Select(static x => new PrintJobItem(
                x.JobId,
                String.Create(CultureInfo.InvariantCulture, $"#{x.JobId}  {x.Title}"),
                x.SubmitTime.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture),
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

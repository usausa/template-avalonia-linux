namespace Template.LinuxApp.Views.Example;

using Avalonia.Threading;

using Smart.Reactive;

using Template.LinuxApp.Components.Barcode;
using Template.LinuxApp.State;

public sealed record BarcodeHistoryItem(string Code, string Source, bool IsQr, DateTimeOffset Time, int Count)
{
    public bool IsRepeated => Count > 1;
}

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class BarcodeViewModel : AppViewModelBase
{
    private const int MaxHistory = 100;

    private static readonly TimeSpan FlashDuration = TimeSpan.FromMilliseconds(400);

    private static readonly TimeSpan RepeatInterval = TimeSpan.FromSeconds(3);

    private readonly TimeProvider timeProvider;

    private readonly IBarcodeReader barcodeReader;

    private readonly IQrReader qrReader;

    private readonly DispatcherTimer flashTimer;

    private long lastScanTimestamp;

    [ObservableProperty]
    public partial BarcodeHistoryItem? Latest { get; set; }

    [ObservableProperty]
    public partial bool IsFlashing { get; set; }

    [ObservableProperty]
    public partial int ScanCount { get; set; }

    public ObservableCollection<BarcodeHistoryItem> History { get; } = [];

    public DeviceStatus BarcodeStatus => barcodeReader.Status;

    public string BarcodeDevice => barcodeReader.Device;

    public DeviceStatus QrStatus => qrReader.Status;

    public string QrDevice => qrReader.Device;

    public ICommand ResumeCommand { get; }

    public ICommand PauseCommand { get; }

    public ICommand ClearCommand { get; }

    public BarcodeViewModel(TimeProvider timeProvider, IBarcodeReader barcodeReader, IQrReader qrReader)
    {
        this.timeProvider = timeProvider;
        this.barcodeReader = barcodeReader;
        this.qrReader = qrReader;

        flashTimer = new DispatcherTimer { Interval = FlashDuration };
        flashTimer.Tick += (_, _) =>
        {
            flashTimer.Stop();
            IsFlashing = false;
        };

        Disposables.Add(Observable
            .FromEventPattern<BarcodeEventArgs>(h => barcodeReader.Scanned += h, h => barcodeReader.Scanned -= h)
            .Select(static x => (Source: "Barcode", IsQr: false, x.EventArgs.Code))
            .Merge(Observable
                .FromEventPattern<BarcodeEventArgs>(h => qrReader.Scanned += h, h => qrReader.Scanned -= h)
                .Select(static x => (Source: "QR", IsQr: true, x.EventArgs.Code)))
            .ObserveOnCurrentContext()
            .Subscribe(x => AddScan(x.Source, x.IsQr, x.Code)));

        ResumeCommand = MakeDelegateCommand(qrReader.ResumeReading);
        PauseCommand = MakeDelegateCommand(qrReader.PauseReading);
        ClearCommand = MakeDelegateCommand(ClearHistory, () => Latest is not null);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            flashTimer.Stop();
        }

        base.Dispose(disposing);
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        barcodeReader.Start();
        qrReader.Start();
        return Task.CompletedTask;
    }

    public override async Task OnNavigatingFromAsync(INavigationContext context)
    {
        await barcodeReader.StopAsync();
        await qrReader.StopAsync();
    }

    private void AddScan(string source, bool isQr, string code)
    {
        if (code.Length == 0)
        {
            return;
        }

        var timestamp = timeProvider.GetTimestamp();
        var time = timeProvider.GetLocalNow();
        if ((Latest is { } latest) &&
            (latest.IsQr == isQr) &&
            (latest.Code == code) &&
            (timeProvider.GetElapsedTime(lastScanTimestamp, timestamp) < RepeatInterval))
        {
            var repeated = latest with { Time = time, Count = latest.Count + 1 };
            History[0] = repeated;
            Latest = repeated;
        }
        else
        {
            var item = new BarcodeHistoryItem(code, source, isQr, time, 1);
            History.Insert(0, item);
            while (History.Count > MaxHistory)
            {
                History.RemoveAt(History.Count - 1);
            }

            Latest = item;
        }

        lastScanTimestamp = timestamp;
        ScanCount++;

        IsFlashing = true;
        flashTimer.Stop();
        flashTimer.Start();
    }

    private void ClearHistory()
    {
        History.Clear();
        Latest = null;
        ScanCount = 0;
    }
}

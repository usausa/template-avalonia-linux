namespace Template.LinuxApp.Views.Example;

using Avalonia.Threading;

using Smart.Reactive;

using Template.LinuxApp.Components.Nfc;
using Template.LinuxApp.Domain.Logic;
using Template.LinuxApp.State;

public sealed record SuicaHistoryItem(DateTime DateTime, bool IsSales, byte Terminal, byte Process, int Balance, int? Amount)
{
    public bool IsIncome => Amount > 0;
}

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class NfcViewModel : AppViewModelBase
{
    private static readonly TimeSpan FlashDuration = TimeSpan.FromMilliseconds(400);

    private static readonly TimeSpan RepeatInterval = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(1);

    private readonly TimeProvider timeProvider;

    private readonly ISuicaReader suicaReader;

    private readonly DispatcherTimer flashTimer;

    private readonly DispatcherTimer errorTimer;

    private long lastReadTimestamp;

    [ObservableProperty]
    public partial bool HasCard { get; set; }

    [ObservableProperty]
    public partial int Balance { get; set; }

    [ObservableProperty]
    public partial string Idm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTimeOffset? ReadTime { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial bool IsFlashing { get; set; }

    [ObservableProperty]
    public partial bool HasHistory { get; set; }

    [ObservableProperty]
    public partial string ReaderDevice { get; set; }

    public ObservableCollection<SuicaHistoryItem> History { get; } = [];

    public DeviceStatus ReaderStatus => suicaReader.Status;

    public ICommand ClearCommand { get; }

    public NfcViewModel(TimeProvider timeProvider, ISuicaReader suicaReader)
    {
        this.timeProvider = timeProvider;
        this.suicaReader = suicaReader;
        ReaderDevice = suicaReader.Device;

        flashTimer = new DispatcherTimer { Interval = FlashDuration };
        flashTimer.Tick += (_, _) =>
        {
            flashTimer.Stop();
            IsFlashing = false;
        };

        errorTimer = new DispatcherTimer { Interval = ErrorDelay };
        errorTimer.Tick += (_, _) =>
        {
            errorTimer.Stop();
            HasError = true;
        };

        Disposables.Add(Observable
            .FromEventPattern<SuicaReadEventArgs>(h => suicaReader.CardRead += h, h => suicaReader.CardRead -= h)
            .ObserveOnCurrentContext()
            .Subscribe(x => ApplyCard(x.EventArgs)));
        Disposables.Add(Observable
            .FromEventPattern(h => suicaReader.ReadFailed += h, h => suicaReader.ReadFailed -= h)
            .ObserveOnCurrentContext()
            .Subscribe(_ =>
            {
                if (!errorTimer.IsEnabled)
                {
                    errorTimer.Start();
                }
            }));
        Disposables.Add(Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(h => suicaReader.Status.PropertyChanged += h, h => suicaReader.Status.PropertyChanged -= h)
            .ObserveOnCurrentContext()
            .Subscribe(_ => ReaderDevice = suicaReader.Device));

        ClearCommand = MakeDelegateCommand(Clear, () => HasCard);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            flashTimer.Stop();
            errorTimer.Stop();
        }

        base.Dispose(disposing);
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        suicaReader.Start();
        return Task.CompletedTask;
    }

    public override async Task OnNavigatingFromAsync(INavigationContext context)
    {
        await suicaReader.StopAsync();
    }

    private void ApplyCard(SuicaReadEventArgs args)
    {
        errorTimer.Stop();
        var timestamp = timeProvider.GetTimestamp();
        if (HasCard && !HasError && (args.Idm == Idm) && (timeProvider.GetElapsedTime(lastReadTimestamp, timestamp) < RepeatInterval))
        {
            return;
        }

        lastReadTimestamp = timestamp;
        Idm = args.Idm;
        Balance = args.Balance;
        ReadTime = timeProvider.GetLocalNow();
        HasError = false;
        HasCard = true;

        History.Clear();
        for (var i = 0; i < args.History.Count; i++)
        {
            History.Add(CreateHistory(args.History[i], i + 1 < args.History.Count ? args.History[i + 1] : null));
        }

        HasHistory = History.Count > 0;

        IsFlashing = true;
        flashTimer.Stop();
        flashTimer.Start();
    }

    private void Clear()
    {
        errorTimer.Stop();
        History.Clear();
        HasHistory = false;
        HasCard = false;
        Balance = 0;
        Idm = string.Empty;
        ReadTime = null;
        HasError = false;
    }

    private static SuicaHistoryItem CreateHistory(SuicaHistoryRecord record, SuicaHistoryRecord? previous)
    {
        var amount = (previous is not null) && (((record.TransactionId - previous.TransactionId) & 0xFFFF) == 1) ? record.Balance - previous.Balance : (int?)null;
        return new SuicaHistoryItem(record.DateTime, SuicaLogic.IsProcessOfSales(record.Process), record.Terminal, record.Process, record.Balance, amount);
    }
}

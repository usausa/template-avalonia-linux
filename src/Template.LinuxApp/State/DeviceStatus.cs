namespace Template.LinuxApp.State;

using Avalonia.Threading;

public sealed partial class DeviceStatus : ObservableObject
{
    private static readonly TimeSpan EventInterval = TimeSpan.FromSeconds(1);

    private readonly ILogger log;

    private readonly TimeProvider timeProvider;

    private readonly IDispatcher dispatcher;

    private long lastEventTimestamp;

    private bool connectedBefore;

    public string Name { get; }

    public bool IsEnabled { get; }

    [ObservableProperty]
    public partial DeviceCondition Condition { get; private set; }

    [ObservableProperty]
    public partial bool IsRunning { get; private set; }

    [ObservableProperty]
    public partial bool IsConnected { get; private set; }

    [ObservableProperty]
    public partial DateTimeOffset? LastEventAt { get; private set; }

    [ObservableProperty]
    public partial int ReconnectCount { get; private set; }

    [ObservableProperty]
    public partial string? LastError { get; private set; }

    public DeviceStatus(ILogger log, TimeProvider timeProvider, IDispatcher dispatcher, string name, bool enabled)
    {
        this.log = log;
        this.timeProvider = timeProvider;
        this.dispatcher = dispatcher;
        Name = name;
        IsEnabled = enabled;
        UpdateCondition();
    }

    public void ReportStarted() => Post(() =>
    {
        IsRunning = true;
        UpdateCondition();
    });

    public void ReportStopped() => Post(() =>
    {
        IsRunning = false;
        IsConnected = false;
        connectedBefore = false;
        UpdateCondition();
    });

    public void ReportConnected() => Post(() =>
    {
        if (IsConnected)
        {
            return;
        }

        if (connectedBefore)
        {
            ReconnectCount++;
        }

        connectedBefore = true;
        IsConnected = true;
        LastError = null;
        UpdateCondition();
        log.InfoDeviceConnected(Name);
    });

    public void ReportDisconnected() => Post(() =>
    {
        if (!IsConnected)
        {
            return;
        }

        IsConnected = false;
        UpdateCondition();
        log.WarnDeviceDisconnected(Name);
    });

    public void ReportError(string message) => Post(() =>
    {
        if (LastError == message)
        {
            return;
        }

        LastError = message;
        log.WarnDeviceError(Name, message);
    });

    public void ReportEvent()
    {
        var timestamp = timeProvider.GetTimestamp();
        var last = Interlocked.Read(ref lastEventTimestamp);
        if ((last != 0) && (timeProvider.GetElapsedTime(last, timestamp) < EventInterval))
        {
            return;
        }

        Interlocked.Exchange(ref lastEventTimestamp, timestamp);
        var now = timeProvider.GetLocalNow();
        Post(() => LastEventAt = now);
    }

    private void UpdateCondition() =>
        Condition = (IsEnabled, IsConnected, IsRunning) switch
        {
            (false, _, _) => DeviceCondition.Disabled,
            (_, true, _) => DeviceCondition.Connected,
            (_, _, true) => DeviceCondition.Waiting,
            _ => DeviceCondition.Idle
        };

    private void Post(Action action)
    {
        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Post(action);
        }
    }
}

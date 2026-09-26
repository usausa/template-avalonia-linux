namespace Template.LinuxApp.State;

using Avalonia.Threading;

public sealed class DeviceState
{
    private readonly ILogger<DeviceState> log;

    private readonly TimeProvider timeProvider;

    private readonly IDispatcher dispatcher;

    public ObservableCollection<DeviceStatus> Devices { get; } = [];

    public DeviceState(ILogger<DeviceState> log, TimeProvider timeProvider, IDispatcher dispatcher)
    {
        this.log = log;
        this.timeProvider = timeProvider;
        this.dispatcher = dispatcher;
    }

    public DeviceStatus Register(string name, bool enabled)
    {
        var status = new DeviceStatus(log, timeProvider, dispatcher, name, enabled);
        if (dispatcher.CheckAccess())
        {
            Devices.Add(status);
        }
        else
        {
            dispatcher.Post(() => Devices.Add(status));
        }

        if (!enabled)
        {
            log.InfoDeviceDisabled(name);
        }

        return status;
    }
}

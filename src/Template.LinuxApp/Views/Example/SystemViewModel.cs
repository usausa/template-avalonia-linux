namespace Template.LinuxApp.Views.Example;

using Avalonia;
using Avalonia.Threading;

using LinuxDotNet.SystemInfo;

using Template.LinuxApp.Services;

public sealed record UsbItem(
    string Name,
    bool IsHub,
    string Port,
    ushort VendorId,
    ushort ProductId,
    double Speed,
    IReadOnlyList<UsbClass> Classes,
    IReadOnlyList<string> Drivers,
    IReadOnlyList<string> Files,
    Thickness Indent);

public sealed record UsbEventItem(DateTimeOffset Time, bool Connected, string Name, string Port);

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class SystemViewModel : AppViewModelBase
{
    private const int TopProcesses = 40;

    private const int StorageTicks = 5;

    private const int MaxUsbEvents = 6;

    private const double IndentWidth = 16;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    private readonly TimeProvider timeProvider;

    private readonly SystemService systemService;

    private readonly DispatcherTimer timer;

    private Dictionary<string, UsbDevice>? previousUsb;

    private int tick;

    private bool refreshing;

    public bool IsSupported => systemService.IsSupported;

    public ObservableCollection<UsbEventItem> UsbEvents { get; } = [];

    [ObservableProperty]
    public partial HostSnapshot? Host { get; set; }

    [ObservableProperty]
    public partial PowerSnapshot? Power { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<FileSystemEntry> FileSystems { get; set; } = [];

    [ObservableProperty]
    public partial int ProcessCount { get; set; }

    [ObservableProperty]
    public partial int ThreadCount { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ProcessEntry> Processes { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<UsbItem> UsbDevices { get; set; } = [];

    [ObservableProperty]
    public partial bool HasUsbEvents { get; set; }

    public SystemViewModel(TimeProvider timeProvider, SystemService systemService)
    {
        this.timeProvider = timeProvider;
        this.systemService = systemService;

        timer = new DispatcherTimer { Interval = RefreshInterval };
        timer.Tick += (_, _) => _ = RefreshAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Stop();
        }

        base.Dispose(disposing);
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        await RefreshAsync();
        timer.Start();
    }

    public override Task OnNavigatingFromAsync(INavigationContext context)
    {
        timer.Stop();
        return Task.CompletedTask;
    }

    private async Task RefreshAsync()
    {
        if (refreshing)
        {
            return;
        }

        refreshing = true;
        try
        {
            var count = tick++;
            var withStorage = (count % StorageTicks) == 0;
            var data = await Task.Run(() => new
            {
                Host = withStorage ? systemService.ReadHost() : null,
                Power = systemService.ReadPower(),
                FileSystems = withStorage ? systemService.ReadFileSystems() : null,
                Processes = systemService.ReadProcesses(TopProcesses),
                Usb = systemService.ReadUsbDevices()
            });

            if (data.Host is not null)
            {
                Host = data.Host;
            }

            if (data.Power is not null)
            {
                Power = data.Power;
            }

            if (data.FileSystems is not null)
            {
                FileSystems = data.FileSystems;
            }

            if (data.Processes is not null)
            {
                ProcessCount = data.Processes.ProcessCount;
                ThreadCount = data.Processes.ThreadCount;
                Processes = data.Processes.Top;
            }

            if (IsSupported)
            {
                ApplyUsb(data.Usb);
            }
        }
        finally
        {
            refreshing = false;
        }
    }

    private void ApplyUsb(IReadOnlyList<UsbDevice> devices)
    {
        var current = devices.ToDictionary(static x => String.Create(CultureInfo.InvariantCulture, $"{x.Name} {x.VendorId:x4}:{x.ProductId:x4}"), StringComparer.Ordinal);
        if (previousUsb is not null)
        {
            var time = timeProvider.GetLocalNow();
            foreach (var device in current.Where(x => !previousUsb.ContainsKey(x.Key)).Select(static x => x.Value))
            {
                AddUsbEvent(new UsbEventItem(time, true, GetName(device), device.Name));
            }

            foreach (var device in previousUsb.Where(x => !current.ContainsKey(x.Key)).Select(static x => x.Value))
            {
                AddUsbEvent(new UsbEventItem(time, false, GetName(device), device.Name));
            }
        }

        previousUsb = current;
        UsbDevices = [.. devices.Select(CreateUsb)];
    }

    private void AddUsbEvent(UsbEventItem item)
    {
        UsbEvents.Insert(0, item);
        while (UsbEvents.Count > MaxUsbEvents)
        {
            UsbEvents.RemoveAt(UsbEvents.Count - 1);
        }

        HasUsbEvents = true;
    }

    private static UsbItem CreateUsb(UsbDevice device) =>
        new(
            GetName(device),
            device.DeviceClass == UsbClass.Hub,
            device.Name,
            device.VendorId,
            device.ProductId,
            device.Speed,
            GetClasses(device),
            [.. device.Interfaces.Select(static x => x.Driver).Where(static x => !String.IsNullOrEmpty(x)).Distinct(StringComparer.Ordinal)],
            [.. device.Interfaces.SelectMany(static x => x.DeviceFiles).Distinct(StringComparer.Ordinal)],
            new Thickness(IndentWidth * device.Name.Count(static x => x == '.'), 0, 0, 0));

    private static string GetName(UsbDevice device) =>
        String.Join(" ", new[] { device.Manufacturer, device.Product }.Where(static x => !String.IsNullOrWhiteSpace(x)).Select(static x => x.Trim()));

    private static List<UsbClass> GetClasses(UsbDevice device)
    {
        if (device.DeviceClass is not (UsbClass.PerInterface or UsbClass.Miscellaneous))
        {
            return [device.DeviceClass];
        }

        var classes = device.Interfaces
            .Select(static x => x.InterfaceClass)
            .Where(static x => x != UsbClass.CdcData)
            .Distinct()
            .ToList();
        return classes.Count > 0 ? classes : [device.DeviceClass];
    }
}

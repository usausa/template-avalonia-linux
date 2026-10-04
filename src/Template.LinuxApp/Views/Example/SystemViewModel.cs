namespace Template.LinuxApp.Views.Example;

using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Threading;

using LinuxDotNet.SystemInfo;

using Template.LinuxApp.Services;

public sealed record InfoItem(string Name, string Value);

public sealed record FileSystemItem(string Label, double Usage, string Text, string Detail);

public sealed record ProcessItem(string ProcessId, string Name, string User, string Cpu, string Memory, string Threads, string State);

public sealed record UsbItem(string Name, string Speed, string Detail, Thickness Indent);

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class SystemViewModel : AppViewModelBase
{
    private const int TopProcesses = 40;

    private const int StorageTicks = 5;

    private const int MaxUsbEvents = 6;

    private const double IndentWidth = 16;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    private static readonly string[] ByteUnits = ["B", "KB", "MB", "GB", "TB", "PB"];

    private readonly ISystemService inspector;

    private readonly TimeProvider timeProvider;

    private readonly DispatcherTimer timer;

    private Dictionary<string, UsbDevice>? previousUsb;

    private int tick;

    private bool refreshing;

    public bool IsSupported => inspector.IsSupported;

    public ObservableCollection<string> UsbEvents { get; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<InfoItem> Hardware { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<InfoItem> Os { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<InfoItem> Power { get; set; } = [];

    [ObservableProperty]
    public partial bool HasBattery { get; set; }

    [ObservableProperty]
    public partial double BatteryLevel { get; set; } = Double.NaN;

    [ObservableProperty]
    public partial string BatteryText { get; set; } = "—";

    [ObservableProperty]
    public partial IReadOnlyList<FileSystemItem> FileSystems { get; set; } = [];

    [ObservableProperty]
    public partial string ProcessSummary { get; set; } = "—";

    [ObservableProperty]
    public partial IReadOnlyList<ProcessItem> Processes { get; set; } = [];

    [ObservableProperty]
    public partial string UsbSummary { get; set; } = "—";

    [ObservableProperty]
    public partial IReadOnlyList<UsbItem> UsbDevices { get; set; } = [];

    [ObservableProperty]
    public partial bool HasUsbEvents { get; set; }

    public SystemViewModel(TimeProvider timeProvider, ISystemService inspector)
    {
        this.timeProvider = timeProvider;
        this.inspector = inspector;

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
                Host = withStorage ? inspector.ReadHost() : null,
                Power = inspector.ReadPower(),
                FileSystems = withStorage ? inspector.ReadFileSystems() : null,
                Processes = inspector.ReadProcesses(TopProcesses),
                Usb = inspector.ReadUsbDevices()
            });

            if (data.Host is not null)
            {
                Hardware = FormatHardware(data.Host.Hardware);
                Os = FormatOs(data.Host);
            }

            if (data.Power is not null)
            {
                ApplyPower(data.Power);
            }

            if (data.FileSystems is not null)
            {
                FileSystems = [.. data.FileSystems.Select(FormatFileSystem)];
            }

            if (data.Processes is not null)
            {
                ProcessSummary = String.Create(CultureInfo.InvariantCulture, $"{data.Processes.ProcessCount:N0} processes, {data.Processes.ThreadCount:N0} threads");
                Processes = [.. data.Processes.Top.Select(FormatProcess)];
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

    private void ApplyPower(PowerSnapshot power)
    {
        HasBattery = power.HasBattery;
        BatteryLevel = power.HasBattery ? power.Capacity : Double.NaN;
        BatteryText = power.HasBattery ? String.Create(CultureInfo.InvariantCulture, $"{power.Capacity}%") : "—";
        Power =
        [
            new InfoItem("AC", power.HasMains ? (power.MainsOnline ? "Online" : "Offline") : "—"),
            new InfoItem("Battery", power.HasBattery ? power.Status : "Not present"),
            new InfoItem("Voltage", power.HasBattery ? String.Create(CultureInfo.InvariantCulture, $"{power.Voltage:F2} V") : "—"),
            new InfoItem("Current", power.HasBattery ? String.Create(CultureInfo.InvariantCulture, $"{power.Current:F2} A") : "—"),
            new InfoItem("Charge", power.HasBattery ? String.Create(CultureInfo.InvariantCulture, $"{power.Charge:N0} / {power.ChargeFull:N0} mAh") : "—")
        ];
    }

    private void ApplyUsb(IReadOnlyList<UsbDevice> devices)
    {
        var current = devices.ToDictionary(static x => String.Create(CultureInfo.InvariantCulture, $"{x.Name} {x.VendorId:x4}:{x.ProductId:x4}"), StringComparer.Ordinal);
        if (previousUsb is not null)
        {
            var time = timeProvider.GetLocalNow().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            foreach (var device in current.Where(x => !previousUsb.ContainsKey(x.Key)).Select(static x => x.Value))
            {
                AddUsbEvent($"{time} Connected {GetUsbName(device)} ({device.Name})");
            }

            foreach (var device in previousUsb.Where(x => !current.ContainsKey(x.Key)).Select(static x => x.Value))
            {
                AddUsbEvent($"{time} Disconnected {GetUsbName(device)} ({device.Name})");
            }
        }

        previousUsb = current;
        UsbSummary = String.Create(CultureInfo.InvariantCulture, $"{devices.Count} devices");
        UsbDevices = [.. devices.Select(FormatUsb)];
    }

    private void AddUsbEvent(string message)
    {
        UsbEvents.Insert(0, message);
        while (UsbEvents.Count > MaxUsbEvents)
        {
            UsbEvents.RemoveAt(UsbEvents.Count - 1);
        }

        HasUsbEvents = true;
    }

    private static List<InfoItem> FormatHardware(HardwareInfo hardware) =>
    [
        new("Model", Join(hardware.Vendor, hardware.ProductName)),
        new("Board", Join(hardware.BoardVendor, hardware.BoardName)),
        new("BIOS", Join(hardware.BiosVendor, hardware.BiosVersion, String.IsNullOrEmpty(hardware.BiosDate) ? String.Empty : $"({hardware.BiosDate})")),
        new("CPU", Join(hardware.CpuBrandString)),
        new("Cores", String.Create(CultureInfo.InvariantCulture, $"{hardware.CoresPerSocket * hardware.PhysicalCpu} cores, {hardware.LogicalCpu} threads")),
        new("Clock", hardware.CpuFrequencyMax > 0 ? String.Create(CultureInfo.InvariantCulture, $"{hardware.CpuFrequencyMax / 1e6:F0} MHz max") : "—"),
        new("Cache", $"L1d {FormatBytes(hardware.L1DCacheSize)}, L1i {FormatBytes(hardware.L1ICacheSize)}, L2 {FormatBytes(hardware.L2CacheSize)}, L3 {FormatBytes(hardware.L3CacheSize)}"),
        new("Memory", FormatBytes(hardware.MemoryTotal))
    ];

    private static List<InfoItem> FormatOs(HostSnapshot host) =>
    [
        new("Distribution", Join(host.Kernel.OsPrettyName)),
        new("Kernel", Join(host.Kernel.OsType, host.Kernel.OsRelease)),
        new("Architecture", RuntimeInformation.OSArchitecture.ToString()),
        new("Host", host.HostName),
        new("Address", host.Addresses.Count > 0 ? String.Join(", ", host.Addresses) : "—"),
        new("Wireless", host.Wireless.Count > 0 ? String.Join(", ", host.Wireless.Select(static x => String.Create(CultureInfo.InvariantCulture, $"{x.Interface} {x.SignalLevel:F0} dBm, quality {x.LinkQuality:F0}"))) : "—"),
        new("Boot", host.Kernel.BootTime > DateTimeOffset.MinValue ? host.Kernel.BootTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "—"),
        new("Uptime", FormatDuration(host.Uptime))
    ];

    private static FileSystemItem FormatFileSystem(FileSystemEntry entry) =>
        new(
            $"{entry.MountPoint}  {entry.FileSystem}  {entry.Device}{(entry.ReadOnly ? "  ro" : String.Empty)}",
            entry.Usage,
            Double.IsFinite(entry.Usage) ? String.Create(CultureInfo.InvariantCulture, $"{entry.Usage:F0}%") : "—",
            $"{FormatBytes(entry.Used)} used of {FormatBytes(entry.Total)}, {FormatBytes(entry.Available)} free");

    private static ProcessItem FormatProcess(ProcessEntry entry) =>
        new(
            entry.ProcessId.ToString(CultureInfo.InvariantCulture),
            entry.Name,
            entry.User,
            Double.IsFinite(entry.CpuUsage) ? String.Create(CultureInfo.InvariantCulture, $"{entry.CpuUsage:F1}%") : "—",
            FormatBytes(entry.Memory),
            entry.Threads.ToString(CultureInfo.InvariantCulture),
            FormatState(entry.State));

    private static UsbItem FormatUsb(UsbDevice device)
    {
        var details = new List<string> { device.Name, String.Create(CultureInfo.InvariantCulture, $"{device.VendorId:x4}:{device.ProductId:x4}"), FormatDeviceClass(device) };
        details.AddRange(device.Interfaces.Select(static x => x.Driver).Distinct(StringComparer.Ordinal));
        details.AddRange(device.Interfaces.SelectMany(static x => x.DeviceFiles).Distinct(StringComparer.Ordinal));
        return new UsbItem(
            GetUsbName(device),
            FormatSpeed(device.Speed),
            String.Join("  ", details.Where(static x => !String.IsNullOrEmpty(x))),
            new Thickness(IndentWidth * device.Name.Count(static x => x == '.'), 0, 0, 0));
    }

    private static string GetUsbName(UsbDevice device)
    {
        var name = Join(device.Manufacturer, device.Product);
        return name.Length > 0 ? name : device.DeviceClass == UsbClass.Hub ? "USB hub" : $"{FormatDeviceClass(device)} device";
    }

    private static string FormatDeviceClass(UsbDevice device)
    {
        if (device.DeviceClass is not (UsbClass.PerInterface or UsbClass.Miscellaneous))
        {
            return FormatClass(device.DeviceClass);
        }

        var names = device.Interfaces
            .Select(static x => x.InterfaceClass)
            .Where(static x => x != UsbClass.CdcData)
            .Distinct()
            .Select(FormatClass)
            .ToList();
        return names.Count > 0 ? String.Join(", ", names) : FormatClass(device.DeviceClass);
    }

    private static string FormatClass(UsbClass value) =>
        value switch
        {
            UsbClass.PerInterface => "Device",
            UsbClass.Hid => "HID",
            UsbClass.CdcData => "CDC data",
            UsbClass.MassStorage => "Mass storage",
            UsbClass.SmartCard => "Smart card",
            UsbClass.ContentSecurity => "Content security",
            UsbClass.PersonalHealthcare => "Healthcare",
            UsbClass.AudioVideo => "Audio/Video",
            UsbClass.TypeCBridge => "Type-C bridge",
            UsbClass.WirelessController => "Wireless",
            UsbClass.ApplicationSpecific => "Application",
            UsbClass.VendorSpecific => "Vendor specific",
            _ => value.ToString()
        };

    private static string Join(params string?[] values) =>
        String.Join(" ", values.Where(static x => !String.IsNullOrWhiteSpace(x)).Select(static x => x!.Trim()));

    private static string FormatState(ProcessState state) =>
        state switch
        {
            ProcessState.Running => "R",
            ProcessState.Sleeping => "S",
            ProcessState.DiskSleep => "D",
            ProcessState.Zombie => "Z",
            ProcessState.Stopped => "T",
            ProcessState.TracingStop => "t",
            ProcessState.Idle => "I",
            _ => "?"
        };

    private static string FormatSpeed(double speed) =>
        !Double.IsFinite(speed)
            ? String.Empty
            : speed >= 1000
                ? String.Create(CultureInfo.InvariantCulture, $"{speed / 1000:0.#} Gb/s")
                : String.Create(CultureInfo.InvariantCulture, $"{speed:0.#} Mb/s");

    private static string FormatBytes(ulong value)
    {
        var size = (double)value;
        var unit = 0;
        while ((size >= 1024) && (unit < ByteUnits.Length - 1))
        {
            size /= 1024;
            unit++;
        }

        return unit == 0
            ? String.Create(CultureInfo.InvariantCulture, $"{value} B")
            : String.Create(CultureInfo.InvariantCulture, $"{size:0.0} {ByteUnits[unit]}");
    }

    private static string FormatDuration(TimeSpan value) =>
        value.TotalDays >= 1
            ? String.Create(CultureInfo.InvariantCulture, $"{(int)value.TotalDays}d {value.Hours:D2}h {value.Minutes:D2}m")
            : value.TotalHours >= 1
                ? String.Create(CultureInfo.InvariantCulture, $"{value.Hours}h {value.Minutes:D2}m")
                : String.Create(CultureInfo.InvariantCulture, $"{value.Minutes}m {value.Seconds:D2}s");
}

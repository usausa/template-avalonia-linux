namespace Template.LinuxApp.Views.Example;

using System.Diagnostics;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Threading;

using LinuxDotNet.Disk;
using LinuxDotNet.SystemInfo;

using Microsoft.Extensions.Hosting;

using Template.LinuxApp.Components.Platform;
using Template.LinuxApp.Settings;

public sealed record InfoItem(string Name, string Value);

public sealed record FileSystemItem(string Label, double Usage, string Text, string Detail);

public sealed record DiskItem(
    string Title,
    string Detail,
    string Smart,
    bool HasSmart,
    double Life,
    string LifeText,
    double Temperature,
    string TemperatureText,
    IReadOnlyList<InfoItem> Attributes);

public sealed record ProcessItem(string ProcessId, string Name, string User, string Cpu, string Memory, string Threads, string State);

public sealed record UsbItem(string Name, string Speed, string Detail, Thickness Indent);

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class SystemViewModel : AppViewModelBase
{
    private const int TopProcesses = 40;

    private const int StorageTicks = 5;

    private const int DiskTicks = 30;

    private const int MaxUsbEvents = 6;

    private const double IndentWidth = 16;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    private static readonly string[] ByteUnits = ["B", "KB", "MB", "GB", "TB", "PB"];

    private readonly ISystemInspector inspector;

    private readonly IHostEnvironment environment;

    private readonly Setting setting;

    private readonly KioskSetting kioskSetting;

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
    public partial IReadOnlyList<InfoItem> Application { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<FileSystemItem> FileSystems { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<DiskItem> Disks { get; set; } = [];

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

    public SystemViewModel(TimeProvider timeProvider, IHostEnvironment environment, Setting setting, KioskSetting kioskSetting, ISystemInspector inspector)
    {
        this.timeProvider = timeProvider;
        this.environment = environment;
        this.setting = setting;
        this.kioskSetting = kioskSetting;
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
            var withDisks = (count % DiskTicks) == 0;
            var data = await Task.Run(() => new
            {
                Host = withStorage ? inspector.ReadHost() : null,
                Power = inspector.ReadPower(),
                FileSystems = withStorage ? inspector.ReadFileSystems() : null,
                Disks = withDisks ? inspector.ReadDisks() : null,
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

            if (data.Disks is not null)
            {
                Disks = [.. data.Disks.Select(FormatDisk)];
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

            Application = FormatApplication();
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
        var current = devices.ToDictionary(static x => $"{x.Port} {x.VendorId}:{x.ProductId}", StringComparer.Ordinal);
        if (previousUsb is not null)
        {
            var time = timeProvider.GetLocalNow().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            foreach (var device in current.Where(x => !previousUsb.ContainsKey(x.Key)).Select(static x => x.Value))
            {
                AddUsbEvent($"{time} Connected {GetUsbName(device)} ({device.Port})");
            }

            foreach (var device in previousUsb.Where(x => !current.ContainsKey(x.Key)).Select(static x => x.Value))
            {
                AddUsbEvent($"{time} Disconnected {GetUsbName(device)} ({device.Port})");
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

    private List<InfoItem> FormatApplication()
    {
        using var process = Process.GetCurrentProcess();
        var started = process.StartTime;
        return
        [
            new InfoItem("Version", typeof(App).Assembly.GetName().Version?.ToString() ?? "—"),
            new InfoItem("Runtime", $"{RuntimeInformation.FrameworkDescription} {RuntimeInformation.RuntimeIdentifier}"),
            new InfoItem("Environment", $"{environment.EnvironmentName} (setting {setting.Value})"),
            new InfoItem("Kiosk", kioskSetting.Enable ? "Enabled" : "Disabled"),
            new InfoItem("Process", String.Create(CultureInfo.InvariantCulture, $"PID {Environment.ProcessId}, {process.Threads.Count} threads")),
            new InfoItem("Started", String.Create(CultureInfo.InvariantCulture, $"{started:yyyy-MM-dd HH:mm:ss} ({FormatDuration(timeProvider.GetLocalNow().LocalDateTime - started)})")),
            new InfoItem("Memory", $"Working set {FormatBytes((ulong)process.WorkingSet64)}, GC heap {FormatBytes((ulong)GC.GetTotalMemory(false))}"),
            new InfoItem("GC", String.Create(CultureInfo.InvariantCulture, $"Gen0 {GC.CollectionCount(0)}, Gen1 {GC.CollectionCount(1)}, Gen2 {GC.CollectionCount(2)}"))
        ];
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

    private static DiskItem FormatDisk(DiskEntry disk)
    {
        var attributes = new List<InfoItem>();
        AddAttribute(attributes, "Power on", disk.PowerOnHours, static x => String.Create(CultureInfo.InvariantCulture, $"{x:N0} h"));
        AddAttribute(attributes, "Power cycles", disk.PowerCycles, static x => x.ToString("N0", CultureInfo.InvariantCulture));
        AddAttribute(attributes, "Read", disk.DataRead, static x => FormatBytes((ulong)x));
        AddAttribute(attributes, "Written", disk.DataWritten, static x => FormatBytes((ulong)x));
        AddAttribute(attributes, "Unsafe shutdowns", disk.UnsafeShutdowns, static x => x.ToString("N0", CultureInfo.InvariantCulture));
        AddAttribute(attributes, "Media errors", disk.MediaErrors, static x => x.ToString("N0", CultureInfo.InvariantCulture));

        return new DiskItem(
            Join(Path.GetFileName(disk.Device), disk.Model),
            Join(FormatDiskType(disk.Type), FormatBytes(disk.Size), String.IsNullOrEmpty(disk.Firmware) ? String.Empty : $"FW {disk.Firmware}", String.IsNullOrEmpty(disk.SerialNumber) ? String.Empty : $"S/N {disk.SerialNumber}", disk.Removable ? "removable" : String.Empty),
            disk.Smart switch
            {
                SmartState.Available => "SMART",
                SmartState.RequiresRoot => "SMART requires root (CAP_SYS_ADMIN)",
                _ => "SMART not supported"
            },
            disk.Smart == SmartState.Available,
            disk.Life,
            Double.IsFinite(disk.Life) ? String.Create(CultureInfo.InvariantCulture, $"{disk.Life:F0}%") : "—",
            disk.Temperature,
            Double.IsFinite(disk.Temperature) ? String.Create(CultureInfo.InvariantCulture, $"{disk.Temperature:F0} °C") : "—",
            attributes);
    }

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
        var details = new List<string> { device.Port, $"{device.VendorId}:{device.ProductId}", device.Class };
        details.AddRange(device.Drivers);
        details.AddRange(device.DeviceFiles);
        return new UsbItem(
            GetUsbName(device),
            FormatSpeed(device.Speed),
            String.Join("  ", details.Where(static x => !String.IsNullOrEmpty(x))),
            new Thickness(IndentWidth * device.Port.Count(static x => x == '.'), 0, 0, 0));
    }

    private static string GetUsbName(UsbDevice device)
    {
        var name = Join(device.Manufacturer, device.Product);
        return name.Length > 0 ? name : device.Class == "Hub" ? "USB hub" : $"{device.Class} device";
    }

    private static void AddAttribute(List<InfoItem> list, string name, double value, Func<double, string> format)
    {
        if (Double.IsFinite(value))
        {
            list.Add(new InfoItem(name, format(value)));
        }
    }

    private static string Join(params string?[] values) =>
        String.Join(" ", values.Where(static x => !String.IsNullOrWhiteSpace(x)).Select(static x => x!.Trim()));

    private static string FormatDiskType(DiskType type) =>
        type switch
        {
            DiskType.Nvme => "NVMe",
            DiskType.Scsi => "SATA/SCSI",
            DiskType.Ide => "IDE",
            DiskType.Mmc => "MMC",
            DiskType.Virtual => "Virtual",
            _ => String.Empty
        };

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

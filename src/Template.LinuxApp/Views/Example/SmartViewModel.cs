namespace Template.LinuxApp.Views.Example;

using Avalonia.Threading;

using LinuxDotNet.Disk;

using Template.LinuxApp.Domain.Logic;
using Template.LinuxApp.Services;

public sealed record SmartGaugeItem(string Label, double Value, double Maximum, string Text, SmartHealth Health);

public sealed record SmartStatItem(string Title, string Value, SmartHealth Health);

public sealed record SmartAttributeItem(string Id, string Name, string Type, string Current, string Worst, string Raw, string RawHex, SmartHealth Health);

public sealed record SmartValueItem(string Name, string Value, string Detail, SmartHealth Health);

public sealed record SmartDiskItem(
    string Device,
    string Name,
    string Model,
    string Detail,
    string Temperature,
    SmartHealth Health,
    bool HasSmart,
    bool IsNvme,
    string Message,
    string MessageDetail,
    IReadOnlyList<SmartIssue> Issues,
    IReadOnlyList<SmartGaugeItem> Gauges,
    IReadOnlyList<SmartStatItem> Stats,
    IReadOnlyList<SmartAttributeItem> Attributes,
    IReadOnlyList<SmartValueItem> Values)
{
    public bool IsSelected { get; init; }

    public string HealthText => Health.ToString();

    public bool IsAta => HasSmart && !IsNvme;

    public bool HasIssues => Issues.Count > 0;

    public string ListTitle => IsNvme ? "NVMe health log" : "SMART attributes";

    public string ListSummary =>
        !HasSmart
            ? String.Empty
            : IsNvme
                ? String.Create(CultureInfo.InvariantCulture, $"{Values.Count} values")
                : String.Create(CultureInfo.InvariantCulture, $"{Attributes.Count} attributes");
}

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class SmartViewModel : AppViewModelBase
{
    private const int MaxStats = 8;

    private const double TemperatureMaximum = 80;

    private const string PermissionDetail = "Run as root, or give the executable CAP_SYS_RAWIO (SATA) and CAP_SYS_ADMIN (NVMe) and add the user to the disk group. See docs/kiosk.md.";

    private const string UnsupportedDetail = "Memory cards, virtual disks and some USB adapters do not report SMART.";

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);

    private static readonly string[] SizeUnits = ["B", "KB", "MB", "GB", "TB", "PB"];

    private readonly TimeProvider timeProvider;

    private readonly IDiskService inspector;

    private readonly DispatcherTimer timer;

    private bool refreshing;

    public bool IsSupported => inspector.IsSupported;

    [ObservableProperty]
    public partial IReadOnlyList<SmartDiskItem> Disks { get; set; } = [];

    [ObservableProperty]
    public partial SmartDiskItem? Selected { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string UpdatedText { get; set; } = String.Empty;

    public ICommand SelectCommand { get; }

    public SmartViewModel(TimeProvider timeProvider, IDiskService inspector)
    {
        this.timeProvider = timeProvider;
        this.inspector = inspector;

        SelectCommand = MakeDelegateCommand<SmartDiskItem>(x => Apply(Disks, x.Device));

        timer = new DispatcherTimer { Interval = RefreshInterval };
        timer.Tick += (_, _) => _ = RefreshAsync(false);
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
        await RefreshAsync(true);
        timer.Start();
    }

    public override Task OnNavigatingFromAsync(INavigationContext context)
    {
        timer.Stop();
        return Task.CompletedTask;
    }

    private async Task RefreshAsync(bool rescan)
    {
        if (refreshing || !IsSupported)
        {
            return;
        }

        refreshing = true;
        try
        {
            var disks = await Task.Run(() => inspector.ReadDisks(rescan));
            Apply([.. disks.Select(FormatDisk)], Selected?.Device);
            IsEmpty = Disks.Count == 0;
            UpdatedText = String.Create(CultureInfo.InvariantCulture, $"Updated {timeProvider.GetLocalNow():HH:mm:ss}");
        }
        finally
        {
            refreshing = false;
        }
    }

    private void Apply(IReadOnlyList<SmartDiskItem> disks, string? device)
    {
        var selected = disks.FirstOrDefault(x => x.Device == device) ?? (disks.Count > 0 ? disks[0] : null);
        Disks = [.. disks.Select(x => x with { IsSelected = x.Device == selected?.Device })];
        Selected = Disks.FirstOrDefault(static x => x.IsSelected);
    }

    private static SmartDiskItem FormatDisk(DiskSnapshot disk)
    {
        var name = Path.GetFileName(disk.Device);
        var item = new SmartDiskItem(
            disk.Device,
            name,
            String.IsNullOrWhiteSpace(disk.Model) ? name : disk.Model.Trim(),
            Join(
                disk.Device,
                FormatDiskType(disk.Type),
                FormatSize(disk.Size),
                String.IsNullOrEmpty(disk.Firmware) ? String.Empty : $"FW {disk.Firmware}",
                String.IsNullOrEmpty(disk.SerialNumber) ? String.Empty : $"S/N {disk.SerialNumber}",
                disk.Removable ? "Removable" : String.Empty),
            "—",
            SmartHealth.Unknown,
            false,
            false,
            String.Empty,
            String.Empty,
            [],
            [],
            [],
            [],
            []);

        return disk.Smart switch
        {
            SmartState.Available when disk.Nvme is { } nvme => FormatNvme(item, nvme),
            SmartState.Available => FormatAta(item, disk.Attributes),
            SmartState.RequiresPermission => item with { Message = "SMART needs permission", MessageDetail = PermissionDetail },
            _ => item with { Message = "SMART is not supported", MessageDetail = UnsupportedDetail }
        };
    }

    private static SmartDiskItem FormatNvme(SmartDiskItem item, NvmeHealth nvme)
    {
        var issues = SmartLogic.EvaluateNvme(nvme.CriticalWarning, nvme.AvailableSpare, nvme.AvailableSpareThreshold, nvme.PercentageUsed, nvme.MediaErrors, nvme.ErrorInfoLogEntries);
        var life = Math.Max(0, 100 - nvme.PercentageUsed);
        var read = nvme.DataUnitRead * SmartLogic.NvmeDataUnit;
        var written = nvme.DataUnitWritten * SmartLogic.NvmeDataUnit;
        var spareHealth = SmartLogic.GetSpareHealth(nvme.AvailableSpare, nvme.AvailableSpareThreshold);
        var mediaHealth = nvme.MediaErrors > 0 ? SmartHealth.Critical : SmartHealth.Good;
        var errorHealth = nvme.ErrorInfoLogEntries > 0 ? SmartHealth.Warning : SmartHealth.Good;

        var values = new List<SmartValueItem>
        {
            new("Critical warning", Count(nvme.CriticalWarning), SmartLogic.FormatCriticalWarning(nvme.CriticalWarning), nvme.CriticalWarning != 0 ? SmartHealth.Critical : SmartHealth.Good),
            new("Composite temperature", Celsius(nvme.Temperature), String.Empty, SmartLogic.GetTemperatureHealth(nvme.Temperature)),
            new("Available spare", Percent(nvme.AvailableSpare), String.Empty, spareHealth),
            new("Available spare threshold", Percent(nvme.AvailableSpareThreshold), String.Empty, SmartHealth.Unknown),
            new("Percentage used", Percent(nvme.PercentageUsed), String.Empty, SmartLogic.GetLifeHealth(life)),
            new("Data units read", Count(nvme.DataUnitRead), FormatSize(read), SmartHealth.Unknown),
            new("Data units written", Count(nvme.DataUnitWritten), FormatSize(written), SmartHealth.Unknown),
            new("Host read commands", Count(nvme.HostReadCommands), String.Empty, SmartHealth.Unknown),
            new("Host write commands", Count(nvme.HostWriteCommands), String.Empty, SmartHealth.Unknown),
            new("Controller busy time", Minutes(nvme.ControllerBusyTime), String.Empty, SmartHealth.Unknown),
            new("Power cycles", Count(nvme.PowerCycles), String.Empty, SmartHealth.Unknown),
            new("Power on hours", Hours(nvme.PowerOnHours), Days(nvme.PowerOnHours), SmartHealth.Unknown),
            new("Unsafe shutdowns", Count(nvme.UnsafeShutdowns), String.Empty, SmartHealth.Unknown),
            new("Media and data integrity errors", Count(nvme.MediaErrors), String.Empty, mediaHealth),
            new("Error information log entries", Count(nvme.ErrorInfoLogEntries), String.Empty, errorHealth),
            new("Warning composite temperature time", Minutes(nvme.WarningTemperatureTime), String.Empty, SmartHealth.Unknown),
            new("Critical composite temperature time", Minutes(nvme.CriticalTemperatureTime), String.Empty, SmartHealth.Unknown)
        };
        values.AddRange(nvme.TemperatureSensors
            .Select(static (x, i) => (Value: x, Number: i + 1))
            .Where(static x => x.Value != Int16.MinValue)
            .Select(static x => new SmartValueItem(String.Create(CultureInfo.InvariantCulture, $"Temperature sensor {x.Number}"), Celsius(x.Value), String.Empty, SmartHealth.Unknown)));

        var health = SmartLogic.GetHealth(issues);
        return item with
        {
            Temperature = Celsius(nvme.Temperature),
            Health = health,
            HasSmart = true,
            IsNvme = true,
            Issues = issues,
            Gauges =
            [
                LifeGauge(life),
                TemperatureGauge(nvme.Temperature),
                new SmartGaugeItem("Spare", nvme.AvailableSpare, 100, Percent(nvme.AvailableSpare), spareHealth)
            ],
            Stats =
            [
                new SmartStatItem("Power on", Hours(nvme.PowerOnHours), SmartHealth.Unknown),
                new SmartStatItem("Power cycles", Count(nvme.PowerCycles), SmartHealth.Unknown),
                new SmartStatItem("Data written", FormatSize(written), SmartHealth.Unknown),
                new SmartStatItem("Data read", FormatSize(read), SmartHealth.Unknown),
                new SmartStatItem("Unsafe shutdowns", Count(nvme.UnsafeShutdowns), SmartHealth.Unknown),
                new SmartStatItem("Busy time", Hours(nvme.ControllerBusyTime / 60d), SmartHealth.Unknown),
                new SmartStatItem("Media errors", Count(nvme.MediaErrors), mediaHealth),
                new SmartStatItem("Error log entries", Count(nvme.ErrorInfoLogEntries), errorHealth)
            ],
            Values = values
        };
    }

    private static SmartDiskItem FormatAta(SmartDiskItem item, IReadOnlyList<SmartAttribute> attributes)
    {
        var issues = SmartLogic.EvaluateAta(attributes);
        var temperature = SmartLogic.GetTemperature(attributes);

        var stats = new List<SmartStatItem>();
        AddStat(stats, "Power on", SmartLogic.GetPowerOnHours(attributes), Hours);
        AddStat(stats, "Power cycles", SmartLogic.GetRaw(attributes, SmartId.PowerCycleCount), Count);
        AddStat(stats, "Data written", SmartLogic.GetDataWritten(attributes), FormatSize);
        AddStat(stats, "Data read", SmartLogic.GetDataRead(attributes), FormatSize);
        AddStat(stats, "Unsafe shutdowns", SmartLogic.GetUnsafeShutdowns(attributes), Count);
        AddCounter(stats, attributes, "Reallocated sectors", SmartId.ReallocatedSectorCount);
        AddCounter(stats, attributes, "Pending sectors", SmartId.CurrentPendingSectorCount);
        AddCounter(stats, attributes, "Uncorrectable sectors", SmartId.UncorrectableSectorCount);
        AddCounter(stats, attributes, "Uncorrectable errors", SmartId.ReportedUncorrectableErrors);
        AddCounter(stats, attributes, "CRC errors", SmartId.UltraDmaCrcErrorCount);

        var health = SmartLogic.GetHealth(issues);
        return item with
        {
            Temperature = Double.IsFinite(temperature) ? Celsius(temperature) : "—",
            Health = health,
            HasSmart = true,
            Issues = issues,
            Gauges = [LifeGauge(SmartLogic.GetLife(attributes)), TemperatureGauge(temperature)],
            Stats = [.. stats.Take(MaxStats)],
            Attributes = [.. attributes.Select(FormatAttribute)]
        };
    }

    private static SmartAttributeItem FormatAttribute(SmartAttribute attribute) =>
        new(
            attribute.Id.ToString("X2", CultureInfo.InvariantCulture),
            SmartLogic.GetAttributeName(attribute.Id),
            SmartLogic.IsPreFailure(attribute) ? "Pre-fail" : "Old age",
            attribute.CurrentValue.ToString(CultureInfo.InvariantCulture),
            attribute.WorstValue.ToString(CultureInfo.InvariantCulture),
            SmartLogic.FormatRawValue(attribute),
            attribute.RawValue.ToString("X12", CultureInfo.InvariantCulture),
            SmartLogic.GetAttributeHealth(attribute));

    private static SmartGaugeItem LifeGauge(double life) =>
        new("Life", life, 100, Double.IsFinite(life) ? Percent(life) : "—", SmartLogic.GetLifeHealth(life));

    private static SmartGaugeItem TemperatureGauge(double temperature) =>
        new("Temperature", temperature, TemperatureMaximum, Double.IsFinite(temperature) ? Celsius(temperature) : "—", SmartLogic.GetTemperatureHealth(temperature));

    private static void AddStat(List<SmartStatItem> stats, string title, double value, Func<double, string> format)
    {
        if (Double.IsFinite(value))
        {
            stats.Add(new SmartStatItem(title, format(value), SmartHealth.Unknown));
        }
    }

    private static void AddCounter(List<SmartStatItem> stats, IReadOnlyList<SmartAttribute> attributes, string title, SmartId id)
    {
        if (SmartLogic.Find(attributes, id) is { } attribute)
        {
            stats.Add(new SmartStatItem(title, Count(attribute.RawValue), SmartLogic.GetAttributeHealth(attribute)));
        }
    }

    private static string Join(params string?[] values) =>
        String.Join("  ", values.Where(static x => !String.IsNullOrWhiteSpace(x)).Select(static x => x!.Trim()));

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

    private static string FormatSize(double value)
    {
        var size = value;
        var unit = 0;
        while ((size >= 1000) && (unit < SizeUnits.Length - 1))
        {
            size /= 1000;
            unit++;
        }

        return unit == 0
            ? String.Create(CultureInfo.InvariantCulture, $"{size:F0} B")
            : String.Create(CultureInfo.InvariantCulture, $"{size:0.0} {SizeUnits[unit]}");
    }

    private static string Count(double value) =>
        value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Hours(double value) =>
        String.Create(CultureInfo.InvariantCulture, $"{value:N0} h");

    private static string Days(double hours) =>
        String.Create(CultureInfo.InvariantCulture, $"{hours / 24:N0} days");

    private static string Minutes(double value) =>
        String.Create(CultureInfo.InvariantCulture, $"{value:N0} min");

    private static string Percent(double value) =>
        String.Create(CultureInfo.InvariantCulture, $"{value:F0}%");

    private static string Celsius(double value) =>
        String.Create(CultureInfo.InvariantCulture, $"{value:F0} °C");
}

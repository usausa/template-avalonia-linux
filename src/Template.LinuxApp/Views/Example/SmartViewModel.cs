namespace Template.LinuxApp.Views.Example;

using Avalonia.Threading;

using LinuxDotNet.Disk;

using Template.LinuxApp.Domain.Logic;
using Template.LinuxApp.Services;

public sealed record SmartStatItem(string Title, double Value, SmartValueUnit Unit, SmartHealth Health);

public sealed record SmartAttributeItem(byte Id, bool IsPreFailure, byte Current, byte Worst, byte Threshold, ulong Raw, SmartHealth Health);

public sealed record SmartValueItem(string Name, double Value, SmartValueUnit Unit, double Detail, SmartValueUnit DetailUnit, SmartHealth Health);

public sealed record SmartDiskItem
{
    public required string Device { get; init; }

    public required string Name { get; init; }

    public required string Model { get; init; }

    public required DiskType Type { get; init; }

    public required ulong Size { get; init; }

    public required string Firmware { get; init; }

    public required string SerialNumber { get; init; }

    public required bool Removable { get; init; }

    public required SmartState Smart { get; init; }

    public SmartAssessment Assessment { get; init; }

    public bool IsNvme { get; init; }

    public SmartHealth Health { get; init; }

    public IReadOnlyList<SmartIssue> Issues { get; init; } = [];

    public double? Temperature { get; init; }

    public SmartHealth TemperatureHealth { get; init; }

    public double? Life { get; init; }

    public SmartHealth LifeHealth { get; init; }

    public double? Spare { get; init; }

    public SmartHealth SpareHealth { get; init; }

    public IReadOnlyList<SmartStatItem> Stats { get; init; } = [];

    public IReadOnlyList<SmartAttributeItem> Attributes { get; init; } = [];

    public IReadOnlyList<SmartValueItem> Values { get; init; } = [];

    public bool IsSelected { get; init; }

    public bool HasSmart => Smart == SmartState.Available;

    public bool IsAta => HasSmart && !IsNvme;

    public bool HasIssues => Issues.Count > 0;

    public bool HasAssessment => Assessment != SmartAssessment.Unknown;
}

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class SmartViewModel : AppViewModelBase
{
    private const int MaxStats = 8;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);

    private readonly TimeProvider timeProvider;

    private readonly DiskService diskService;

    private readonly DispatcherTimer timer;

    private bool refreshing;

    public bool IsSupported => diskService.IsSupported;

    [ObservableProperty]
    public partial IReadOnlyList<SmartDiskItem> Disks { get; set; } = [];

    [ObservableProperty]
    public partial SmartDiskItem? Selected { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial DateTimeOffset? Updated { get; set; }

    public ICommand SelectCommand { get; }

    public SmartViewModel(TimeProvider timeProvider, DiskService diskService)
    {
        this.timeProvider = timeProvider;
        this.diskService = diskService;

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
            var disks = await Task.Run(() => diskService.ReadDisks(rescan));
            Apply([.. disks.Select(CreateDisk)], Selected?.Device);
            IsEmpty = Disks.Count == 0;
            Updated = timeProvider.GetLocalNow();
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

    private static SmartDiskItem CreateDisk(DiskSnapshot disk)
    {
        var name = Path.GetFileName(disk.Device);
        var item = new SmartDiskItem
        {
            Device = disk.Device,
            Name = name,
            Model = String.IsNullOrWhiteSpace(disk.Model) ? name : disk.Model.Trim(),
            Type = disk.Type,
            Size = disk.Size,
            Firmware = disk.Firmware.Trim(),
            SerialNumber = disk.SerialNumber.Trim(),
            Removable = disk.Removable,
            Smart = disk.Smart
        };

        if (disk.Smart != SmartState.Available)
        {
            return item;
        }

        return disk.Nvme is { } nvme ? CreateNvme(item, nvme) : CreateAta(item, disk.Attributes, disk.Assessment);
    }

    private static SmartDiskItem CreateNvme(SmartDiskItem item, NvmeHealth nvme)
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
            new("Critical warning", nvme.CriticalWarning, SmartValueUnit.Count, nvme.CriticalWarning, SmartValueUnit.CriticalWarning, nvme.CriticalWarning != 0 ? SmartHealth.Critical : SmartHealth.Good),
            new("Composite temperature", nvme.Temperature, SmartValueUnit.Celsius, Double.NaN, SmartValueUnit.None, SmartLogic.GetTemperatureHealth(nvme.Temperature)),
            new("Available spare", nvme.AvailableSpare, SmartValueUnit.Percent, Double.NaN, SmartValueUnit.None, spareHealth),
            new("Available spare threshold", nvme.AvailableSpareThreshold, SmartValueUnit.Percent, Double.NaN, SmartValueUnit.None, SmartHealth.Unknown),
            new("Percentage used", nvme.PercentageUsed, SmartValueUnit.Percent, Double.NaN, SmartValueUnit.None, SmartLogic.GetLifeHealth(life)),
            new("Data units read", nvme.DataUnitRead, SmartValueUnit.Count, read, SmartValueUnit.Bytes, SmartHealth.Unknown),
            new("Data units written", nvme.DataUnitWritten, SmartValueUnit.Count, written, SmartValueUnit.Bytes, SmartHealth.Unknown),
            new("Host read commands", nvme.HostReadCommands, SmartValueUnit.Count, Double.NaN, SmartValueUnit.None, SmartHealth.Unknown),
            new("Host write commands", nvme.HostWriteCommands, SmartValueUnit.Count, Double.NaN, SmartValueUnit.None, SmartHealth.Unknown),
            new("Controller busy time", nvme.ControllerBusyTime, SmartValueUnit.Minutes, Double.NaN, SmartValueUnit.None, SmartHealth.Unknown),
            new("Power cycles", nvme.PowerCycles, SmartValueUnit.Count, Double.NaN, SmartValueUnit.None, SmartHealth.Unknown),
            new("Power on hours", nvme.PowerOnHours, SmartValueUnit.Hours, nvme.PowerOnHours / 24d, SmartValueUnit.Days, SmartHealth.Unknown),
            new("Unsafe shutdowns", nvme.UnsafeShutdowns, SmartValueUnit.Count, Double.NaN, SmartValueUnit.None, SmartHealth.Unknown),
            new("Media and data integrity errors", nvme.MediaErrors, SmartValueUnit.Count, Double.NaN, SmartValueUnit.None, mediaHealth),
            new("Error information log entries", nvme.ErrorInfoLogEntries, SmartValueUnit.Count, Double.NaN, SmartValueUnit.None, errorHealth),
            new("Warning composite temperature time", nvme.WarningTemperatureTime, SmartValueUnit.Minutes, Double.NaN, SmartValueUnit.None, SmartHealth.Unknown),
            new("Critical composite temperature time", nvme.CriticalTemperatureTime, SmartValueUnit.Minutes, Double.NaN, SmartValueUnit.None, SmartHealth.Unknown)
        };
        values.AddRange(nvme.TemperatureSensors
            .Select(static (x, i) => (Value: x, Number: i + 1))
            .Where(static x => x.Value != Int16.MinValue)
            .Select(static x => new SmartValueItem(String.Create(CultureInfo.InvariantCulture, $"Temperature sensor {x.Number}"), x.Value, SmartValueUnit.Celsius, Double.NaN, SmartValueUnit.None, SmartHealth.Unknown)));

        return item with
        {
            IsNvme = true,
            Health = SmartLogic.GetHealth(issues),
            Issues = issues,
            Temperature = nvme.Temperature,
            TemperatureHealth = SmartLogic.GetTemperatureHealth(nvme.Temperature),
            Life = life,
            LifeHealth = SmartLogic.GetLifeHealth(life),
            Spare = nvme.AvailableSpare,
            SpareHealth = spareHealth,
            Stats =
            [
                new SmartStatItem("Power on", nvme.PowerOnHours, SmartValueUnit.Hours, SmartHealth.Unknown),
                new SmartStatItem("Power cycles", nvme.PowerCycles, SmartValueUnit.Count, SmartHealth.Unknown),
                new SmartStatItem("Data written", written, SmartValueUnit.Bytes, SmartHealth.Unknown),
                new SmartStatItem("Data read", read, SmartValueUnit.Bytes, SmartHealth.Unknown),
                new SmartStatItem("Unsafe shutdowns", nvme.UnsafeShutdowns, SmartValueUnit.Count, SmartHealth.Unknown),
                new SmartStatItem("Busy time", nvme.ControllerBusyTime / 60d, SmartValueUnit.Hours, SmartHealth.Unknown),
                new SmartStatItem("Media errors", nvme.MediaErrors, SmartValueUnit.Count, mediaHealth),
                new SmartStatItem("Error log entries", nvme.ErrorInfoLogEntries, SmartValueUnit.Count, errorHealth)
            ],
            Values = values
        };
    }

    private static SmartDiskItem CreateAta(SmartDiskItem item, IReadOnlyList<SmartAttribute> attributes, SmartAssessment assessment)
    {
        var issues = SmartLogic.EvaluateAta(attributes, assessment);
        var temperature = SmartLogic.GetTemperature(attributes);
        var life = SmartLogic.GetLife(attributes);

        var stats = new List<SmartStatItem>();
        AddStat(stats, "Power on", SmartLogic.GetPowerOnHours(attributes), SmartValueUnit.Hours);
        AddStat(stats, "Power cycles", SmartLogic.GetRaw(attributes, SmartId.PowerCycleCount), SmartValueUnit.Count);
        AddStat(stats, "Data written", SmartLogic.GetDataWritten(attributes), SmartValueUnit.Bytes);
        AddStat(stats, "Data read", SmartLogic.GetDataRead(attributes), SmartValueUnit.Bytes);
        AddStat(stats, "Unsafe shutdowns", SmartLogic.GetUnsafeShutdowns(attributes), SmartValueUnit.Count);
        AddCounter(stats, attributes, "Reallocated sectors", SmartId.ReallocatedSectorCount);
        AddCounter(stats, attributes, "Pending sectors", SmartId.CurrentPendingSectorCount);
        AddCounter(stats, attributes, "Uncorrectable sectors", SmartId.UncorrectableSectorCount);
        AddCounter(stats, attributes, "Uncorrectable errors", SmartId.ReportedUncorrectableErrors);
        AddCounter(stats, attributes, "CRC errors", SmartId.UltraDmaCrcErrorCount);

        return item with
        {
            Assessment = assessment,
            Health = SmartLogic.GetHealth(issues),
            Issues = issues,
            Temperature = Double.IsFinite(temperature) ? temperature : null,
            TemperatureHealth = SmartLogic.GetTemperatureHealth(temperature),
            Life = Double.IsFinite(life) ? life : null,
            LifeHealth = SmartLogic.GetLifeHealth(life),
            Stats = [.. stats.Take(MaxStats)],
            Attributes = [.. attributes.Select(static x => new SmartAttributeItem(x.Id, SmartLogic.IsPreFailure(x), x.CurrentValue, x.WorstValue, x.Threshold, x.RawValue, SmartLogic.GetAttributeHealth(x)))]
        };
    }

    private static void AddStat(List<SmartStatItem> stats, string title, double value, SmartValueUnit unit)
    {
        if (Double.IsFinite(value))
        {
            stats.Add(new SmartStatItem(title, value, unit, SmartHealth.Unknown));
        }
    }

    private static void AddCounter(List<SmartStatItem> stats, IReadOnlyList<SmartAttribute> attributes, string title, SmartId id)
    {
        if (SmartLogic.Find(attributes, id) is { } attribute)
        {
            stats.Add(new SmartStatItem(title, attribute.RawValue, SmartValueUnit.Count, SmartLogic.GetAttributeHealth(attribute)));
        }
    }
}

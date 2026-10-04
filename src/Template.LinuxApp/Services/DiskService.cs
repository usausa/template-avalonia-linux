namespace Template.LinuxApp.Services;

using LinuxDotNet.Disk;

public enum SmartState
{
    Available,
    Unsupported,
    RequiresPermission
}

public sealed record NvmeHealth(
    byte CriticalWarning,
    short Temperature,
    byte AvailableSpare,
    byte AvailableSpareThreshold,
    byte PercentageUsed,
    ulong DataUnitRead,
    ulong DataUnitWritten,
    ulong HostReadCommands,
    ulong HostWriteCommands,
    ulong ControllerBusyTime,
    ulong PowerCycles,
    ulong PowerOnHours,
    ulong UnsafeShutdowns,
    ulong MediaErrors,
    ulong ErrorInfoLogEntries,
    uint WarningTemperatureTime,
    uint CriticalTemperatureTime,
    IReadOnlyList<short> TemperatureSensors);

public sealed record DiskSnapshot(
    string Device,
    string Model,
    string SerialNumber,
    string Firmware,
    DiskType Type,
    ulong Size,
    bool Removable,
    SmartState Smart,
    NvmeHealth? Nvme,
    IReadOnlyList<SmartAttribute> Attributes);

public sealed class DiskService : IDisposable
{
    private readonly Lock sync = new();

    private readonly ILogger<DiskService> log;

    private IReadOnlyList<IDiskInfo>? disks;

    private bool failed;

    public bool IsSupported { get; } = OperatingSystem.IsLinux();

    public DiskService(ILogger<DiskService> log)
    {
        this.log = log;
    }

    public void Dispose()
    {
        lock (sync)
        {
            Release();
        }
    }

    public IReadOnlyList<DiskSnapshot> ReadDisks(bool rescan)
    {
        if (!IsSupported)
        {
            return [];
        }

        lock (sync)
        {
            try
            {
                if (rescan)
                {
                    Release();
                }

                disks ??= DiskInfo.GetInformation();
                var list = disks.Select(ToSnapshot).ToList();
                failed = false;
                return list;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (!failed)
                {
                    failed = true;
                    log.WarnSystemReadFailed("disks", ex);
                }

                return [];
            }
        }
    }

    private void Release()
    {
        if (disks is null)
        {
            return;
        }

        foreach (var disk in disks)
        {
            disk.Dispose();
        }

        disks = null;
    }

    private static DiskSnapshot ToSnapshot(IDiskInfo disk)
    {
        if ((disk.SmartType == SmartType.Nvme) && (disk.Smart is ISmartNvme nvme) && nvme.Update())
        {
            var health = new NvmeHealth(
                nvme.CriticalWarning,
                nvme.Temperature,
                nvme.AvailableSpare,
                nvme.AvailableSpareThreshold,
                nvme.PercentageUsed,
                nvme.DataUnitRead,
                nvme.DataUnitWritten,
                nvme.HostReadCommands,
                nvme.HostWriteCommands,
                nvme.ControllerBusyTime,
                nvme.PowerCycles,
                nvme.PowerOnHours,
                nvme.UnsafeShutdowns,
                nvme.MediaErrors,
                nvme.ErrorInfoLogEntries,
                nvme.WarningCompositeTemperatureTime,
                nvme.CriticalCompositeTemperatureTime,
                [.. nvme.TemperatureSensors]);
            return Create(disk, SmartState.Available, health, []);
        }

        if ((disk.SmartType == SmartType.Generic) && (disk.Smart is ISmartGeneric generic) && generic.Update())
        {
            var attributes = new List<SmartAttribute>();
            foreach (var id in generic.GetSupportedIds())
            {
                if (generic.GetAttribute(id) is { } attribute)
                {
                    attributes.Add(attribute);
                }
            }

            return Create(disk, SmartState.Available, null, attributes);
        }

        var state = (disk.DiskType is DiskType.Nvme or DiskType.Scsi or DiskType.Ide) && !Environment.IsPrivilegedProcess
            ? SmartState.RequiresPermission
            : SmartState.Unsupported;
        return Create(disk, state, null, []);
    }

    private static DiskSnapshot Create(IDiskInfo disk, SmartState state, NvmeHealth? nvme, IReadOnlyList<SmartAttribute> attributes) =>
        new(disk.DeviceName, disk.Model, disk.SerialNumber, disk.FirmwareRevision, disk.DiskType, disk.Size, disk.Removable, state, nvme, attributes);
}

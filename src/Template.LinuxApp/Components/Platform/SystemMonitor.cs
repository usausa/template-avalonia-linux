namespace Template.LinuxApp.Components.Platform;

using LinuxDotNet.SystemInfo;

public sealed record SystemSnapshot(
    double CpuUsage,
    ulong MemoryTotal,
    ulong MemoryAvailable,
    double LoadAverage1,
    double LoadAverage5,
    double LoadAverage15,
    double? Temperature,
    TimeSpan Uptime);

public interface ISystemMonitor
{
    bool IsSupported { get; }

    SystemSnapshot? Read();
}

public sealed class SystemMonitor : ISystemMonitor
{
    private const ulong KiloByte = 1024;

    private readonly Lock sync = new();

    private SystemStat? systemStat;

    private MemoryStat? memoryStat;

    private LoadAverage? loadAverage;

    private Uptime? uptime;

    private List<HardwareSensor> temperatures = [];

    private ulong lastTotal;

    private ulong lastIdle;

    public bool IsSupported => OperatingSystem.IsLinux();

    public SystemSnapshot? Read()
    {
        if (!IsSupported)
        {
            return null;
        }

        lock (sync)
        {
            if ((systemStat is null) || (memoryStat is null) || (loadAverage is null) || (uptime is null))
            {
                systemStat = PlatformProvider.GetSystemStat();
                memoryStat = PlatformProvider.GetMemoryStat();
                loadAverage = PlatformProvider.GetLoadAverage();
                uptime = PlatformProvider.GetUptime();
                temperatures = FindTemperatures();
            }
            else
            {
                systemStat.Update();
                memoryStat.Update();
                loadAverage.Update();
                uptime.Update();
                foreach (var sensor in temperatures)
                {
                    sensor.Update();
                }
            }

            var cpu = systemStat.CpuTotal;
            var idle = cpu.Idle + cpu.IoWait;
            var total = cpu.User + cpu.Nice + cpu.System + cpu.Idle + cpu.IoWait + cpu.Irq + cpu.SoftIrq + cpu.Steal;
            var usage = (lastTotal > 0) && (total > lastTotal) ? 100d * (1d - ((double)(idle - lastIdle) / (total - lastTotal))) : 0d;
            lastTotal = total;
            lastIdle = idle;

            return new SystemSnapshot(
                usage,
                memoryStat.MemoryTotal * KiloByte,
                memoryStat.MemoryAvailable * KiloByte,
                loadAverage.Average1,
                loadAverage.Average5,
                loadAverage.Average15,
                temperatures.Count > 0 ? temperatures.Max(static x => x.Value) / 1000d : null,
                uptime.Elapsed);
        }
    }

    private static List<HardwareSensor> FindTemperatures()
    {
        try
        {
            return PlatformProvider.GetHardwareMonitors()
                .SelectMany(static x => x.Sensors)
                .Where(static x => x.Type == "temp")
                .ToList();
        }
        catch (IOException)
        {
            return [];
        }
    }
}

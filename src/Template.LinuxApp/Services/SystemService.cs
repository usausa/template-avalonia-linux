namespace Template.LinuxApp.Services;

using System.Net.NetworkInformation;
using System.Net.Sockets;

using LinuxDotNet.SystemInfo;

public sealed record HostSnapshot(
    HardwareInfo Hardware,
    KernelInfo Kernel,
    string HostName,
    IReadOnlyList<string> Addresses,
    IReadOnlyList<WirelessStatEntry> Wireless,
    TimeSpan Uptime);

public sealed record PowerSnapshot(
    bool HasMains,
    bool MainsOnline,
    bool HasBattery,
    int Capacity,
    string Status,
    double Voltage,
    double Current,
    double Charge,
    double ChargeFull);

public sealed record FileSystemEntry(
    string MountPoint,
    string FileSystem,
    string Device,
    bool ReadOnly,
    ulong Total,
    ulong Used,
    ulong Available,
    double Usage);

public sealed record ProcessEntry(
    int ProcessId,
    string Name,
    string User,
    double CpuUsage,
    ulong Memory,
    int Threads,
    ProcessState State);

public sealed record ProcessSnapshot(int ProcessCount, int ThreadCount, IReadOnlyList<ProcessEntry> Top);

public interface ISystemService
{
    bool IsSupported { get; }

    HostSnapshot? ReadHost();

    PowerSnapshot? ReadPower();

    IReadOnlyList<FileSystemEntry> ReadFileSystems();

    ProcessSnapshot? ReadProcesses(int count);

    IReadOnlyList<UsbDevice> ReadUsbDevices();
}

public sealed class SystemService : ISystemService
{
    private readonly Lock sync = new();

    private readonly ILogger<SystemService> log;

    private readonly TimeProvider timeProvider;

    private readonly HashSet<string> failedSections = [];

    private readonly Dictionary<int, (DateTimeOffset StartTime, double CpuSeconds)> previousProcesses = [];

    private Dictionary<uint, string>? userNames;

    private HardwareInfo? hardware;

    private KernelInfo? kernel;

    private Uptime? uptime;

    private MainsDevice? mains;

    private BatteryDevice? battery;

    private long? previousProcessTimestamp;

    public bool IsSupported => OperatingSystem.IsLinux();

    public SystemService(ILogger<SystemService> log, TimeProvider timeProvider)
    {
        this.log = log;
        this.timeProvider = timeProvider;
    }

    public HostSnapshot? ReadHost() =>
        Guard("host", () =>
        {
            hardware ??= PlatformProvider.GetHardware();
            kernel ??= PlatformProvider.GetKernel();
            if (uptime is null)
            {
                uptime = PlatformProvider.GetUptime();
            }
            else
            {
                uptime.Update();
            }

            return new HostSnapshot(hardware, kernel, Environment.MachineName, ReadAddresses(), PlatformProvider.GetWirelessStat().Interfaces, uptime.Elapsed);
        }, null);

    public PowerSnapshot? ReadPower() =>
        Guard("power", () =>
        {
            if (mains is null)
            {
                mains = PlatformProvider.GetMainsDevice();
            }
            else if (mains.Supported)
            {
                mains.Update();
            }

            if (battery is null)
            {
                battery = PlatformProvider.GetBatteryDevice();
            }
            else if (battery.Supported)
            {
                battery.Update();
            }

            return new PowerSnapshot(
                mains.Supported,
                mains.Supported && mains.Online,
                battery.Supported,
                battery.Capacity,
                battery.Status,
                battery.Voltage / 1e6,
                battery.Current / 1e6,
                battery.Charge / 1e3,
                battery.ChargeFull / 1e3);
        }, null);

    public IReadOnlyList<FileSystemEntry> ReadFileSystems() =>
        Guard<IReadOnlyList<FileSystemEntry>>("file systems", () =>
        {
            var list = new List<FileSystemEntry>();
            foreach (var mount in PlatformProvider.GetMounts()
                .Where(static x => x.DeviceName.StartsWith("/dev/", StringComparison.Ordinal) && !x.DeviceName.StartsWith("/dev/loop", StringComparison.Ordinal))
                .DistinctBy(static x => x.MountPoint))
            {
                var usage = PlatformProvider.GetFileSystemUsage(mount.MountPoint);
                var used = usage.TotalSize - usage.FreeSize;
                var capacity = used + usage.AvailableSize;
                list.Add(new FileSystemEntry(
                    mount.MountPoint,
                    mount.FileSystem,
                    mount.DeviceName,
                    mount.Option.IsReadonly(),
                    usage.TotalSize,
                    used,
                    usage.AvailableSize,
                    capacity > 0 ? 100d * used / capacity : Double.NaN));
            }

            return list;
        }, []);

    public ProcessSnapshot? ReadProcesses(int count) =>
        Guard("processes", () =>
        {
            var timestamp = timeProvider.GetTimestamp();
            var seconds = previousProcessTimestamp is { } last ? timeProvider.GetElapsedTime(last, timestamp).TotalSeconds : 0d;
            previousProcessTimestamp = timestamp;

            userNames ??= ReadUserNames();
            var processes = PlatformProvider.GetProcesses();
            var entries = new List<ProcessEntry>(processes.Count);
            var alive = new HashSet<int>();
            foreach (var process in processes)
            {
                alive.Add(process.ProcessId);
                var cpuSeconds = (process.UserTime + process.SystemTime).TotalSeconds;
                var usage = Double.NaN;
                if ((seconds > 0) && previousProcesses.TryGetValue(process.ProcessId, out var previous) && (previous.StartTime == process.StartTime))
                {
                    usage = Math.Max(0, 100d * (cpuSeconds - previous.CpuSeconds) / seconds);
                }

                previousProcesses[process.ProcessId] = (process.StartTime, cpuSeconds);
                entries.Add(new ProcessEntry(
                    process.ProcessId,
                    process.Name,
                    userNames.TryGetValue(process.UserId, out var user) ? user : process.UserId.ToString(CultureInfo.InvariantCulture),
                    usage,
                    process.ResidentMemorySize,
                    process.ThreadCount,
                    process.State));
            }

            foreach (var id in previousProcesses.Keys.Where(x => !alive.Contains(x)).ToList())
            {
                previousProcesses.Remove(id);
            }

            var top = entries
                .OrderByDescending(static x => Double.IsFinite(x.CpuUsage) ? x.CpuUsage : 0d)
                .ThenByDescending(static x => x.Memory)
                .Take(count)
                .ToList();
            return new ProcessSnapshot(entries.Count, entries.Sum(static x => x.Threads), top);
        }, null);

    public IReadOnlyList<UsbDevice> ReadUsbDevices() =>
        Guard("usb", static () => PlatformProvider.GetUsbDevices(), []);

    private T Guard<T>(string section, Func<T> func, T fallback)
    {
        if (!IsSupported)
        {
            return fallback;
        }

        lock (sync)
        {
            try
            {
                var result = func();
                failedSections.Remove(section);
                return result;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
            {
                if (failedSections.Add(section))
                {
                    log.WarnSystemReadFailed(section, ex);
                }

                return fallback;
            }
        }
    }

    private static List<string> ReadAddresses()
    {
        var list = new List<string>();
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
        {
            if ((network.OperationalStatus != OperationalStatus.Up) || (network.NetworkInterfaceType == NetworkInterfaceType.Loopback))
            {
                continue;
            }

            foreach (var address in network.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    list.Add($"{network.Name} {address.Address}");
                }
            }
        }

        return list;
    }

    private static Dictionary<uint, string> ReadUserNames()
    {
        var names = new Dictionary<uint, string>();
        foreach (var line in File.ReadLines("/etc/passwd"))
        {
            var fields = line.Split(':');
            if ((fields.Length > 2) && UInt32.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                names.TryAdd(id, fields[0]);
            }
        }

        return names;
    }
}

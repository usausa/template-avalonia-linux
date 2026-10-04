namespace Template.LinuxApp.Services;

using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

using LinuxDotNet.SystemInfo;

public sealed record NetworkAddress(string Interface, string Address);

public sealed record WirelessLink(string Interface, double SignalLevel, double LinkQuality);

public sealed record HostSnapshot(
    string Vendor,
    string ProductName,
    string BoardVendor,
    string BoardName,
    string BiosVendor,
    string BiosVersion,
    string BiosDate,
    string Cpu,
    int Cores,
    int Threads,
    double? ClockMhz,
    ulong L1DCache,
    ulong L1ICache,
    ulong L2Cache,
    ulong L3Cache,
    ulong Memory,
    string Distribution,
    string OsType,
    string OsRelease,
    Architecture Architecture,
    string HostName,
    IReadOnlyList<NetworkAddress> Addresses,
    IReadOnlyList<WirelessLink> Wireless,
    DateTimeOffset? BootTime,
    TimeSpan Uptime);

public sealed record PowerSnapshot(
    bool HasMains,
    bool? MainsOnline,
    bool HasBattery,
    int? Capacity,
    string? Status,
    double? Voltage,
    double? Current,
    double? Charge,
    double? ChargeFull);

public sealed record FileSystemEntry(
    string MountPoint,
    string FileSystem,
    string Device,
    bool ReadOnly,
    ulong Total,
    ulong Used,
    ulong Available,
    double? Usage);

public sealed record ProcessEntry(
    int ProcessId,
    string Name,
    string User,
    double? CpuUsage,
    ulong Memory,
    int Threads,
    ProcessState State);

public sealed record ProcessSnapshot(int ProcessCount, int ThreadCount, IReadOnlyList<ProcessEntry> Top);

public sealed class SystemService
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

    public bool IsSupported { get; } = OperatingSystem.IsLinux();

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

            return new HostSnapshot(
                hardware.Vendor.Trim(),
                hardware.ProductName.Trim(),
                hardware.BoardVendor.Trim(),
                hardware.BoardName.Trim(),
                hardware.BiosVendor.Trim(),
                hardware.BiosVersion.Trim(),
                hardware.BiosDate.Trim(),
                hardware.CpuBrandString.Trim(),
                hardware.CoresPerSocket * hardware.PhysicalCpu,
                hardware.LogicalCpu,
                hardware.CpuFrequencyMax > 0 ? hardware.CpuFrequencyMax / 1e6 : null,
                hardware.L1DCacheSize,
                hardware.L1ICacheSize,
                hardware.L2CacheSize,
                hardware.L3CacheSize,
                hardware.MemoryTotal,
                kernel.OsPrettyName?.Trim() ?? String.Empty,
                kernel.OsType.Trim(),
                kernel.OsRelease.Trim(),
                RuntimeInformation.OSArchitecture,
                Environment.MachineName,
                ReadAddresses(),
                [.. PlatformProvider.GetWirelessStat().Interfaces.Select(static x => new WirelessLink(x.Interface, x.SignalLevel, x.LinkQuality))],
                kernel.BootTime > DateTimeOffset.MinValue ? kernel.BootTime.ToLocalTime() : null,
                uptime.Elapsed);
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
                mains.Supported ? mains.Online : null,
                battery.Supported,
                battery.Supported ? battery.Capacity : null,
                battery.Supported ? battery.Status : null,
                battery.Supported ? battery.Voltage / 1e6 : null,
                battery.Supported ? battery.Current / 1e6 : null,
                battery.Supported ? battery.Charge / 1e3 : null,
                battery.Supported ? battery.ChargeFull / 1e3 : null);
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
                    capacity > 0 ? 100d * used / capacity : null));
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
                double? usage = null;
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
                .OrderByDescending(static x => x.CpuUsage ?? 0d)
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

    private static List<NetworkAddress> ReadAddresses()
    {
        var list = new List<NetworkAddress>();
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
                    list.Add(new NetworkAddress(network.Name, address.Address.ToString()));
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

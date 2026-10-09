namespace Template.LinuxApp.Services;

using System.Text.RegularExpressions;

using LinuxDotNet.SystemInfo;

public sealed record MetricValue(double Last, IReadOnlyList<double> History)
{
    public static MetricValue Empty { get; } = new(Double.NaN, []);

    public double? Current => Double.IsFinite(Last) ? Last : null;
}

public sealed record MetricSeries(string Name, double Last, IReadOnlyList<double> History);

public sealed class PerformanceSample
{
    public required DateTimeOffset Timestamp { get; init; }

    public required TimeSpan Uptime { get; init; }

    public required MetricValue UptimeDays { get; init; }

    public required MetricValue Load { get; init; }

    public required MetricValue Processes { get; init; }

    public required MetricValue Threads { get; init; }

    public required MetricValue FileHandles { get; init; }

    public required MetricValue Connections { get; init; }

    public required MetricValue Cpu { get; init; }

    public required MetricValue Memory { get; init; }

    public required MetricValue Swap { get; init; }

    public required MetricValue Battery { get; init; }

    public required MetricValue Power { get; init; }

    public required MetricValue CpuTemperature { get; init; }

    public required IReadOnlyList<MetricSeries> CoreUsage { get; init; }

    public required IReadOnlyList<MetricSeries> CoreClock { get; init; }

    public required IReadOnlyList<MetricSeries> Temperatures { get; init; }

    public required IReadOnlyList<MetricSeries> DiskBusy { get; init; }

    public required IReadOnlyList<MetricSeries> Network { get; init; }

    public required MetricValue RunQueue { get; init; }

    public required MetricValue Interrupts { get; init; }

    public required MetricValue ContextSwitches { get; init; }

    public required MetricValue Forks { get; init; }

    public required MetricValue PageFaults { get; init; }

    public required MetricValue SwapPages { get; init; }

    public required MetricValue DiskQueue { get; init; }

    public required MetricValue Commit { get; init; }
}

public sealed partial class PerformanceService : IDisposable
{
    private const int HistoryCapacity = 120;

    private const int MaxTemperatureSensors = 8;

    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

    private static readonly string[] VirtualInterfacePrefixes = ["docker", "veth", "br-", "virbr", "vmnet", "tun", "tap", "lxc", "cni", "flannel"];

    public event EventHandler<EventArgs<PerformanceSample>>? Sampled;

    private readonly Lock sync = new();

    private readonly ILogger<PerformanceService> log;

    private readonly TimeProvider timeProvider;

    private readonly MetricHistory uptimeDays = new(HistoryCapacity);

    private readonly MetricHistory load = new(HistoryCapacity);

    private readonly MetricHistory processes = new(HistoryCapacity);

    private readonly MetricHistory threads = new(HistoryCapacity);

    private readonly MetricHistory fileHandles = new(HistoryCapacity);

    private readonly MetricHistory connections = new(HistoryCapacity);

    private readonly MetricHistory cpu = new(HistoryCapacity);

    private readonly MetricHistory memory = new(HistoryCapacity);

    private readonly MetricHistory swap = new(HistoryCapacity);

    private readonly MetricHistory battery = new(HistoryCapacity);

    private readonly MetricHistory power = new(HistoryCapacity);

    private readonly MetricHistory cpuTemperature = new(HistoryCapacity);

    private readonly MetricHistory runQueue = new(HistoryCapacity);

    private readonly MetricHistory interrupts = new(HistoryCapacity);

    private readonly MetricHistory contextSwitches = new(HistoryCapacity);

    private readonly MetricHistory forks = new(HistoryCapacity);

    private readonly MetricHistory pageFaults = new(HistoryCapacity);

    private readonly MetricHistory swapPages = new(HistoryCapacity);

    private readonly MetricHistory diskQueue = new(HistoryCapacity);

    private readonly MetricHistory commit = new(HistoryCapacity);

    private readonly SeriesHistory coreUsage = new(HistoryCapacity);

    private readonly SeriesHistory coreClock = new(HistoryCapacity);

    private readonly SeriesHistory temperatures = new(HistoryCapacity);

    private readonly SeriesHistory diskBusy = new(HistoryCapacity);

    private readonly SeriesHistory network = new(HistoryCapacity);

    private readonly Dictionary<string, CpuTime> previousCores = [];

    private readonly Dictionary<string, ulong> previousIoTime = [];

    private readonly Dictionary<string, (ulong Rx, ulong Tx)> previousNetwork = [];

    private CancellationTokenSource? cts;

    private Task? loopTask;

    private Readers? readers;

    private long? previousTimestamp;

    private CpuTime previousCpu;

    private ulong? previousInterrupts;

    private ulong? previousContextSwitches;

    private ulong? previousForks;

    private ulong? previousPageFaults;

    private ulong? previousSwapPages;

    private ulong? previousEnergy;

    private bool sampleFailed;

    public bool IsSupported { get; } = OperatingSystem.IsLinux();

    public int Capacity { get; } = HistoryCapacity;

    public TimeSpan Interval { get; } = SampleInterval;

    public PerformanceSample? Latest { get; private set; }

    public PerformanceService(ILogger<PerformanceService> log, TimeProvider timeProvider)
    {
        this.log = log;
        this.timeProvider = timeProvider;
    }

    public void Dispose()
    {
        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        try
        {
            loopTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        cts.Dispose();
        cts = null;

        lock (sync)
        {
            readers?.Dispose();
            readers = null;
        }
    }

    public void Start()
    {
        lock (sync)
        {
            if ((cts is not null) || !IsSupported)
            {
                return;
            }

            cts = new CancellationTokenSource();
            var token = cts.Token;
            loopTask = Task.Run(() => LoopAsync(token), token);
        }
    }

    private async Task LoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(SampleInterval, timeProvider);
        do
        {
            PerformanceSample? sample;
            try
            {
                lock (sync)
                {
                    sample = Sample();
                    Latest = sample;
                    sampleFailed = false;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
            {
                if (!sampleFailed)
                {
                    log.WarnPerformanceSampleFailed(ex);
                    sampleFailed = true;
                }

                sample = null;
            }

            if (sample is not null)
            {
                Sampled?.Invoke(this, new EventArgs<PerformanceSample>(sample));
            }
        }
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false));
    }

    private PerformanceSample Sample()
    {
        var timestamp = timeProvider.GetTimestamp();
        var seconds = previousTimestamp is { } last ? timeProvider.GetElapsedTime(last, timestamp).TotalSeconds : 0d;
        previousTimestamp = timestamp;

        if (readers is null)
        {
            readers = new Readers();
        }
        else
        {
            readers.Update();
        }

        var r = readers;

        uptimeDays.Add(r.Uptime.Elapsed.TotalDays);
        load.Add(r.Load.Average1);
        processes.Add(r.Processes.ProcessCount);
        threads.Add(r.Processes.ThreadCount);
        fileHandles.Add(r.FileHandles.Allocated);
        connections.Add(r.Tcp4.Established + r.Tcp6.Established);

        cpu.Add(CalcUsage(r.Stat.CpuTotal, ref previousCpu));
        coreUsage.BeginSample();
        foreach (var core in r.Stat.CpuCores)
        {
            var previous = previousCores.GetValueOrDefault(core.Name);
            coreUsage.Add(core.Name, CalcUsage(core, ref previous));
            previousCores[core.Name] = previous;
        }
        coreUsage.EndSample();

        var mem = r.Memory;
        memory.Add(mem.MemoryTotal > 0 ? 100d * (mem.MemoryTotal - mem.MemoryAvailable) / mem.MemoryTotal : Double.NaN);
        swap.Add(mem.SwapTotal > 0 ? 100d * (mem.SwapTotal - mem.SwapFree) / mem.SwapTotal : Double.NaN);
        commit.Add(mem.CommitLimit > 0 ? 100d * mem.CommittedAddressSpace / mem.CommitLimit : Double.NaN);

        runQueue.Add(r.Stat.RunnableTasks);
        interrupts.Add(CalcRate(r.Stat.Interrupt, ref previousInterrupts, seconds));
        contextSwitches.Add(CalcRate(r.Stat.ContextSwitch, ref previousContextSwitches, seconds));
        forks.Add(CalcRate(r.Stat.Forks, ref previousForks, seconds));
        pageFaults.Add(CalcRate(r.VirtualMemory.PageFaults, ref previousPageFaults, seconds));
        swapPages.Add(CalcRate(r.VirtualMemory.SwapIn + r.VirtualMemory.SwapOut, ref previousSwapPages, seconds));

        SampleDisks(r.Disk, seconds);
        SampleNetwork(r.Network, seconds);

        battery.Add(r.Battery.Supported ? r.Battery.Capacity : Double.NaN);
        power.Add(CalcPower(r, seconds));
        cpuTemperature.Add(r.CpuTemperature is { } cpuSensor ? cpuSensor.Value / 1000d : Double.NaN);

        temperatures.BeginSample();
        foreach (var (name, sensor) in r.Temperatures)
        {
            temperatures.Add(name, sensor.Value / 1000d);
        }
        temperatures.EndSample();

        coreClock.BeginSample();
        foreach (var core in r.Cpu.Cores)
        {
            coreClock.Add(core.Name, core.Frequency > 0 ? core.Frequency / 1000d : Double.NaN);
        }
        coreClock.EndSample();

        return new PerformanceSample
        {
            Timestamp = timeProvider.GetLocalNow(),
            Uptime = r.Uptime.Elapsed,
            UptimeDays = ToValue(uptimeDays),
            Load = ToValue(load),
            Processes = ToValue(processes),
            Threads = ToValue(threads),
            FileHandles = ToValue(fileHandles),
            Connections = ToValue(connections),
            Cpu = ToValue(cpu),
            Memory = ToValue(memory),
            Swap = ToValue(swap),
            Battery = ToValue(battery),
            Power = ToValue(power),
            CpuTemperature = ToValue(cpuTemperature),
            CoreUsage = coreUsage.ToSeries(),
            CoreClock = coreClock.ToSeries(),
            Temperatures = temperatures.ToSeries(),
            DiskBusy = diskBusy.ToSeries(),
            Network = network.ToSeries(),
            RunQueue = ToValue(runQueue),
            Interrupts = ToValue(interrupts),
            ContextSwitches = ToValue(contextSwitches),
            Forks = ToValue(forks),
            PageFaults = ToValue(pageFaults),
            SwapPages = ToValue(swapPages),
            DiskQueue = ToValue(diskQueue),
            Commit = ToValue(commit)
        };
    }

    private void SampleDisks(DiskStat disk, double seconds)
    {
        var queue = 0UL;
        diskBusy.BeginSample();
        foreach (var device in disk.Devices.Where(static x => !IsPartition(x.Name)))
        {
            queue += device.IosInProgress;
            var busy = Double.NaN;
            if ((seconds > 0) && previousIoTime.TryGetValue(device.Name, out var previous) && (device.IoTime >= previous))
            {
                busy = Math.Clamp((device.IoTime - previous) / (seconds * 10d), 0d, 100d);
            }

            previousIoTime[device.Name] = device.IoTime;
            diskBusy.Add(device.Name, busy);
        }
        diskBusy.EndSample();
        diskQueue.Add(queue);
    }

    private void SampleNetwork(NetworkStat stat, double seconds)
    {
        network.BeginSample();
        foreach (var entry in stat.Interfaces.Where(static x => IsPhysicalInterface(x.Interface)))
        {
            var rx = Double.NaN;
            var tx = Double.NaN;
            if ((seconds > 0) && previousNetwork.TryGetValue(entry.Interface, out var previous) && (entry.RxBytes >= previous.Rx) && (entry.TxBytes >= previous.Tx))
            {
                rx = (entry.RxBytes - previous.Rx) / seconds;
                tx = (entry.TxBytes - previous.Tx) / seconds;
            }

            previousNetwork[entry.Interface] = (entry.RxBytes, entry.TxBytes);
            network.Add($"{entry.Interface} rx", rx);
            network.Add($"{entry.Interface} tx", tx);
        }
        network.EndSample();
    }

    private double CalcPower(Readers r, double seconds)
    {
        var energy = r.Cpu.Powers.Count > 0 ? r.Cpu.Powers[0].Energy : 0;
        if (energy > 0)
        {
            var value = (seconds > 0) && (previousEnergy is { } previous) && (energy >= previous) ? (energy - previous) / seconds / 1e6 : Double.NaN;
            previousEnergy = energy;
            return value;
        }

        previousEnergy = null;
        return r.Battery.Supported ? Math.Abs((double)r.Battery.Voltage * r.Battery.Current) / 1e12 : Double.NaN;
    }

    private static double CalcUsage(CpuStat stat, ref CpuTime previous)
    {
        var busy = stat.User + stat.Nice + stat.System + stat.Irq + stat.SoftIrq + stat.Steal;
        var total = busy + stat.Idle + stat.IoWait;
        var usage = (previous.Total > 0) && (total > previous.Total) && (busy >= previous.Busy)
            ? 100d * (busy - previous.Busy) / (total - previous.Total)
            : Double.NaN;
        previous = new CpuTime(busy, total);
        return usage;
    }

    private static double CalcRate(ulong value, ref ulong? previous, double seconds)
    {
        var rate = (seconds > 0) && (previous is { } last) && (value >= last) ? (value - last) / seconds : Double.NaN;
        previous = value;
        return rate;
    }

    private static MetricValue ToValue(MetricHistory history) => new(history.Last, history.ToArray());

    private static bool IsPhysicalInterface(string name) =>
        !String.Equals(name, "lo", StringComparison.Ordinal) &&
        !VirtualInterfacePrefixes.Any(x => name.StartsWith(x, StringComparison.Ordinal));

    private static bool IsPartition(string name) => PartitionPattern().IsMatch(name);

    [GeneratedRegex(@"^(?:nvme\d+n\d+p\d+|mmcblk\d+p\d+|mmcblk\d+(?:boot\d+|rpmb)|(?:sd|hd|vd|xvd)[a-z]+\d+)$")]
    private static partial Regex PartitionPattern();

    private readonly record struct CpuTime(ulong Busy, ulong Total);

    private sealed class SeriesHistory
    {
        private readonly int capacity;

        private readonly List<(string Name, MetricHistory History)> entries = [];

        private readonly HashSet<string> sampled = [];

        public SeriesHistory(int capacity)
        {
            this.capacity = capacity;
        }

        public void BeginSample() => sampled.Clear();

        public void Add(string name, double value)
        {
            sampled.Add(name);
            foreach (var (entryName, history) in entries)
            {
                if (String.Equals(entryName, name, StringComparison.Ordinal))
                {
                    history.Add(value);
                    return;
                }
            }

            var created = new MetricHistory(capacity);
            created.Add(value);
            entries.Add((name, created));
        }

        public void EndSample()
        {
            foreach (var (name, history) in entries)
            {
                if (!sampled.Contains(name))
                {
                    history.Add(Double.NaN);
                }
            }
        }

        public IReadOnlyList<MetricSeries> ToSeries() => [.. entries.Select(static x => new MetricSeries(x.Name, x.History.Last, x.History.ToArray()))];
    }

    private sealed class Readers : IDisposable
    {
        private readonly IReadOnlyList<HardwareMonitor> monitors;

        public Uptime Uptime { get; } = PlatformProvider.GetUptime();

        public LoadAverage Load { get; } = PlatformProvider.GetLoadAverage();

        public SystemStat Stat { get; } = PlatformProvider.GetSystemStat();

        public MemoryStat Memory { get; } = PlatformProvider.GetMemoryStat();

        public VirtualMemoryStat VirtualMemory { get; } = PlatformProvider.GetVirtualMemoryStat();

        public DiskStat Disk { get; } = PlatformProvider.GetDiskStat();

        public NetworkStat Network { get; } = PlatformProvider.GetNetworkStat();

        public TcpStat Tcp4 { get; } = PlatformProvider.GetTcpStat();

        public TcpStat Tcp6 { get; } = PlatformProvider.GetTcp6Stat();

        public ProcessSummary Processes { get; } = PlatformProvider.GetProcessSummary();

        public FileHandleStat FileHandles { get; } = PlatformProvider.GetFileHandleStat();

        public BatteryDevice Battery { get; } = PlatformProvider.GetBatteryDevice();

        public CpuDevice Cpu { get; } = PlatformProvider.GetCpuDevice();

        public IReadOnlyList<(string Name, HardwareSensor Sensor)> Temperatures { get; }

        public HardwareSensor? CpuTemperature { get; }

        public Readers()
        {
            monitors = PlatformProvider.GetHardwareMonitors();
            var sensors = monitors
                .SelectMany(static monitor => monitor.Sensors
                    .Where(static x => x.Type == "temp")
                    .Select(x => (Monitor: monitor.Name, Sensor: x)))
                .ToList();
            Temperatures = [.. sensors
                .Take(MaxTemperatureSensors)
                .Select(static x => (String.IsNullOrEmpty(x.Sensor.Label) ? x.Monitor : $"{x.Monitor} {x.Sensor.Label}", x.Sensor))];
            CpuTemperature = FindCpuTemperature(sensors);
        }

        public void Dispose()
        {
            Uptime.Dispose();
            Load.Dispose();
            Stat.Dispose();
            Memory.Dispose();
            VirtualMemory.Dispose();
            Disk.Dispose();
            Network.Dispose();
            Tcp4.Dispose();
            Tcp6.Dispose();
            Processes.Dispose();
            FileHandles.Dispose();
            Battery.Dispose();
            Cpu.Dispose();
            foreach (var monitor in monitors)
            {
                monitor.Dispose();
            }
        }

        public void Update()
        {
            Uptime.Update();
            Load.Update();
            Stat.Update();
            Memory.Update();
            VirtualMemory.Update();
            Disk.Update();
            Network.Update();
            Tcp4.Update();
            Tcp6.Update();
            Processes.Update();
            FileHandles.Update();
            if (Battery.Supported)
            {
                Battery.Update();
            }

            Cpu.Update();
            foreach (var (_, sensor) in Temperatures)
            {
                sensor.Update();
            }

            if ((CpuTemperature is not null) && Temperatures.All(x => !ReferenceEquals(x.Sensor, CpuTemperature)))
            {
                CpuTemperature.Update();
            }
        }

        private static HardwareSensor? FindCpuTemperature(List<(string Monitor, HardwareSensor Sensor)> sensors)
        {
            static bool Match((string Monitor, HardwareSensor Sensor) x, string monitor, string? labelPrefix) =>
                String.Equals(x.Monitor, monitor, StringComparison.Ordinal) &&
                ((labelPrefix is null) || x.Sensor.Label.StartsWith(labelPrefix, StringComparison.Ordinal));

            return sensors.FirstOrDefault(static x => Match(x, "coretemp", "Package")).Sensor
                ?? sensors.FirstOrDefault(static x => Match(x, "k10temp", "Tctl")).Sensor
                ?? sensors.FirstOrDefault(static x => Match(x, "k10temp", null)).Sensor
                ?? sensors.FirstOrDefault(static x => Match(x, "cpu_thermal", null)).Sensor
                ?? sensors.FirstOrDefault(static x => Match(x, "acpitz", null)).Sensor;
        }
    }
}

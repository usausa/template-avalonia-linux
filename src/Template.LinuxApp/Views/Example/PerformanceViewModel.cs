namespace Template.LinuxApp.Views.Example;

using System.Reactive.Concurrency;

using Template.LinuxApp.Controls;
using Template.LinuxApp.Services;

public sealed partial class MetricItem : ObservableObject
{
    [ObservableProperty]
    public partial string Value { get; set; } = "—";

    [ObservableProperty]
    public partial IReadOnlyList<double>? Values { get; set; }

    public void Update(MetricValue metric, string value)
    {
        Value = value;
        Values = metric.History;
    }
}

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class PerformanceViewModel : AppViewModelBase
{
    private const double KiloByte = 1024;

    public bool IsSupported { get; }

    public int Capacity { get; }

    public TimeSpan Interval { get; }

    public MetricItem Uptime { get; } = new();

    public MetricItem Load { get; } = new();

    public MetricItem Processes { get; } = new();

    public MetricItem Threads { get; } = new();

    public MetricItem FileHandles { get; } = new();

    public MetricItem Connections { get; } = new();

    public MetricItem Cpu { get; } = new();

    public MetricItem Memory { get; } = new();

    public MetricItem Swap { get; } = new();

    public MetricItem Battery { get; } = new();

    public MetricItem Power { get; } = new();

    public MetricItem CpuTemperature { get; } = new();

    public MetricItem RunQueue { get; } = new();

    public MetricItem Interrupts { get; } = new();

    public MetricItem ContextSwitches { get; } = new();

    public MetricItem Forks { get; } = new();

    public MetricItem PageFaults { get; } = new();

    public MetricItem SwapPages { get; } = new();

    public MetricItem DiskQueue { get; } = new();

    public MetricItem Commit { get; } = new();

    [ObservableProperty]
    public partial DateTimeOffset? Time { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ChartSeries>? CoreUsage { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ChartSeries>? CoreClock { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ChartSeries>? Temperatures { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ChartSeries>? DiskBusy { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ChartSeries>? Network { get; set; }

    public PerformanceViewModel(IPerformanceService monitor)
    {
        IsSupported = monitor.IsSupported;
        Capacity = monitor.Capacity;
        Interval = monitor.Interval;

        var scheduler = new SynchronizationContextScheduler(SynchronizationContext.Current!);
        Disposables.Add(Observable
            .FromEvent<EventHandler<EventArgs<PerformanceSample>>, EventArgs<PerformanceSample>>(static h => (_, e) => h(e), h => monitor.Sampled += h, h => monitor.Sampled -= h)
            .ObserveOn(scheduler)
            .Subscribe(x => Apply(x.Data)));

        if (monitor.Latest is { } latest)
        {
            Apply(latest);
        }

        monitor.Start();
    }

    private void Apply(PerformanceSample sample)
    {
        Time = sample.Timestamp;
        Uptime.Update(sample.UptimeDays, FormatUptime(sample.Uptime));
        Load.Update(sample.Load, Format(sample.Load.Last, "0.00", String.Empty));
        Processes.Update(sample.Processes, FormatCount(sample.Processes.Last));
        Threads.Update(sample.Threads, FormatCount(sample.Threads.Last));
        FileHandles.Update(sample.FileHandles, FormatCount(sample.FileHandles.Last));
        Connections.Update(sample.Connections, FormatCount(sample.Connections.Last));
        Cpu.Update(sample.Cpu, FormatPercent(sample.Cpu.Last));
        Memory.Update(sample.Memory, FormatPercent(sample.Memory.Last));
        Swap.Update(sample.Swap, FormatPercent(sample.Swap.Last));
        Battery.Update(sample.Battery, Format(sample.Battery.Last, "0", "%"));
        Power.Update(sample.Power, Format(sample.Power.Last, "0.0", " W"));
        CpuTemperature.Update(sample.CpuTemperature, Format(sample.CpuTemperature.Last, "0", " °C"));

        RunQueue.Update(sample.RunQueue, FormatCount(sample.RunQueue.Last));
        Interrupts.Update(sample.Interrupts, FormatRate(sample.Interrupts.Last));
        ContextSwitches.Update(sample.ContextSwitches, FormatRate(sample.ContextSwitches.Last));
        Forks.Update(sample.Forks, FormatRate(sample.Forks.Last));
        PageFaults.Update(sample.PageFaults, FormatRate(sample.PageFaults.Last));
        SwapPages.Update(sample.SwapPages, FormatRate(sample.SwapPages.Last));
        DiskQueue.Update(sample.DiskQueue, FormatCount(sample.DiskQueue.Last));
        Commit.Update(sample.Commit, FormatPercent(sample.Commit.Last));

        CoreUsage = [.. sample.CoreUsage.Select(static x => new ChartSeries(x.Name, x.History, FormatPercent(x.Last)))];
        CoreClock = [.. sample.CoreClock.Select(static x => new ChartSeries(x.Name, x.History, Format(x.Last, "0", " MHz")))];
        Temperatures = [.. sample.Temperatures.Select(static x => new ChartSeries(x.Name, x.History, Format(x.Last, "0", " °C")))];
        DiskBusy = [.. sample.DiskBusy.Select(static x => new ChartSeries(x.Name, x.History, FormatPercent(x.Last)))];
        Network = [.. sample.Network.Select(static x => new ChartSeries(x.Name, [.. x.History.Select(static v => v / KiloByte)], FormatBytesRate(x.Last)))];
    }

    private static string Format(double value, string format, string unit) =>
        Double.IsFinite(value) ? value.ToString(format, CultureInfo.InvariantCulture) + unit : "—";

    private static string FormatPercent(double value) => Format(value, "0.0", "%");

    private static string FormatCount(double value) => Format(value, "N0", String.Empty);

    private static string FormatRate(double value) => Format(value, "N0", "/s");

    private static string FormatBytesRate(double value) =>
        !Double.IsFinite(value)
            ? "—"
            : value >= KiloByte * KiloByte
                ? Format(value / KiloByte / KiloByte, "0.0", " MB/s")
                : value >= KiloByte
                    ? Format(value / KiloByte, "0.0", " KB/s")
                    : Format(value, "0", " B/s");

    private static string FormatUptime(TimeSpan uptime) =>
        uptime.TotalDays >= 1
            ? String.Create(CultureInfo.InvariantCulture, $"{(int)uptime.TotalDays}d {uptime.Hours:D2}h")
            : uptime.TotalHours >= 1
                ? String.Create(CultureInfo.InvariantCulture, $"{uptime.Hours}h {uptime.Minutes:D2}m")
                : String.Create(CultureInfo.InvariantCulture, $"{uptime.Minutes}m {uptime.Seconds:D2}s");
}

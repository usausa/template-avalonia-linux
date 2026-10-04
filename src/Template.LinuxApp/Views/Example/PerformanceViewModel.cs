namespace Template.LinuxApp.Views.Example;

using System.Reactive.Concurrency;

using Template.LinuxApp.Controls;
using Template.LinuxApp.Services;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class PerformanceViewModel : AppViewModelBase
{
    private const double KiloByte = 1024;

    public bool IsSupported { get; }

    public int Capacity { get; }

    public TimeSpan Interval { get; }

    [ObservableProperty]
    public partial DateTimeOffset? Time { get; set; }

    [ObservableProperty]
    public partial TimeSpan? Uptime { get; set; }

    [ObservableProperty]
    public partial MetricValue UptimeDays { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue Load { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue Processes { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue Threads { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue FileHandles { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue Connections { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue Cpu { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue Memory { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue Swap { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue Battery { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue Power { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue CpuTemperature { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue RunQueue { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue Interrupts { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue ContextSwitches { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue Forks { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue PageFaults { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue SwapPages { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue DiskQueue { get; set; } = MetricValue.Empty;

    [ObservableProperty]
    public partial MetricValue Commit { get; set; } = MetricValue.Empty;

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

    public PerformanceViewModel(PerformanceService performanceService)
    {
        IsSupported = performanceService.IsSupported;
        Capacity = performanceService.Capacity;
        Interval = performanceService.Interval;

        var scheduler = new SynchronizationContextScheduler(SynchronizationContext.Current!);
        Disposables.Add(Observable
            .FromEvent<EventHandler<EventArgs<PerformanceSample>>, EventArgs<PerformanceSample>>(static h => (_, e) => h(e), h => performanceService.Sampled += h, h => performanceService.Sampled -= h)
            .ObserveOn(scheduler)
            .Subscribe(x => Apply(x.Data)));

        if (performanceService.Latest is { } latest)
        {
            Apply(latest);
        }

        performanceService.Start();
    }

    private void Apply(PerformanceSample sample)
    {
        Time = sample.Timestamp;
        Uptime = sample.Uptime;
        UptimeDays = sample.UptimeDays;
        Load = sample.Load;
        Processes = sample.Processes;
        Threads = sample.Threads;
        FileHandles = sample.FileHandles;
        Connections = sample.Connections;
        Cpu = sample.Cpu;
        Memory = sample.Memory;
        Swap = sample.Swap;
        Battery = sample.Battery;
        Power = sample.Power;
        CpuTemperature = sample.CpuTemperature;

        RunQueue = sample.RunQueue;
        Interrupts = sample.Interrupts;
        ContextSwitches = sample.ContextSwitches;
        Forks = sample.Forks;
        PageFaults = sample.PageFaults;
        SwapPages = sample.SwapPages;
        DiskQueue = sample.DiskQueue;
        Commit = sample.Commit;

        CoreUsage = [.. sample.CoreUsage.Select(static x => new ChartSeries(x.Name, x.History, x.Last))];
        CoreClock = [.. sample.CoreClock.Select(static x => new ChartSeries(x.Name, x.History, x.Last))];
        Temperatures = [.. sample.Temperatures.Select(static x => new ChartSeries(x.Name, x.History, x.Last))];
        DiskBusy = [.. sample.DiskBusy.Select(static x => new ChartSeries(x.Name, x.History, x.Last))];
        Network = [.. sample.Network.Select(static x => new ChartSeries(x.Name, [.. x.History.Select(static v => v / KiloByte)], x.Last))];
    }
}

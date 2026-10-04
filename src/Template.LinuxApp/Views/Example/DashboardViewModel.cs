namespace Template.LinuxApp.Views.Example;

using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Runtime.InteropServices;

using Microsoft.Extensions.Hosting;

using Template.LinuxApp.Components.Platform;
using Template.LinuxApp.Services;
using Template.LinuxApp.Settings;

public sealed record GaugeValue(double Value, string Text)
{
    public static GaugeValue Empty { get; } = new(Double.NaN, "—");
}

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class DashboardViewModel : AppViewModelBase
{
    private readonly IPerformanceMonitor monitor;

    public bool IsSupported => monitor.IsSupported;

    public IReadOnlyList<InfoItem> Application { get; }

    [ObservableProperty]
    public partial GaugeValue Cpu { get; set; } = GaugeValue.Empty;

    [ObservableProperty]
    public partial GaugeValue Memory { get; set; } = GaugeValue.Empty;

    [ObservableProperty]
    public partial GaugeValue Swap { get; set; } = GaugeValue.Empty;

    [ObservableProperty]
    public partial GaugeValue Temperature { get; set; } = GaugeValue.Empty;

    public ICommand ThemeCommand { get; }

    public ICommand ExitCommand { get; }

    public DashboardViewModel(IHostEnvironment environment, Setting setting, KioskSetting kioskSetting, IPerformanceMonitor monitor, ThemeService themeService, ExitService exitService)
    {
        this.monitor = monitor;
        Application = CreateApplication(environment, setting, kioskSetting);

        var scheduler = new SynchronizationContextScheduler(SynchronizationContext.Current!);
        Disposables.Add(Observable
            .FromEvent<EventHandler<EventArgs<PerformanceSample>>, EventArgs<PerformanceSample>>(static h => (_, e) => h(e), h => monitor.Sampled += h, h => monitor.Sampled -= h)
            .ObserveOn(scheduler)
            .Subscribe(x => Apply(x.Data)));

        ThemeCommand = MakeAsyncCommand<string>(x => themeService.ChangeAsync(x).AsTask());
        ExitCommand = MakeAsyncCommand(() => exitService.RequestExitAsync().AsTask());
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        if (monitor.Latest is { } latest)
        {
            Apply(latest);
        }

        monitor.Start();
        return Task.CompletedTask;
    }

    private static List<InfoItem> CreateApplication(IHostEnvironment environment, Setting setting, KioskSetting kioskSetting)
    {
        using var process = Process.GetCurrentProcess();
        return
        [
            new InfoItem("Version", typeof(App).Assembly.GetName().Version?.ToString() ?? "—"),
            new InfoItem("Runtime", $"{RuntimeInformation.FrameworkDescription} {RuntimeInformation.RuntimeIdentifier}"),
            new InfoItem("Environment", $"{environment.EnvironmentName} (setting {setting.Value})"),
            new InfoItem("Kiosk", kioskSetting.Enable ? "Enabled" : "Disabled"),
            new InfoItem("Process", String.Create(CultureInfo.InvariantCulture, $"PID {Environment.ProcessId}")),
            new InfoItem("Started", process.StartTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
        ];
    }

    private void Apply(PerformanceSample sample)
    {
        Cpu = Percent(sample.Cpu.Last);
        Memory = Percent(sample.Memory.Last);
        Swap = Percent(sample.Swap.Last);
        Temperature = Double.IsFinite(sample.CpuTemperature.Last)
            ? new GaugeValue(sample.CpuTemperature.Last, String.Create(CultureInfo.InvariantCulture, $"{sample.CpuTemperature.Last:F0} °C"))
            : GaugeValue.Empty;
    }

    private static GaugeValue Percent(double value) =>
        Double.IsFinite(value) ? new GaugeValue(value, String.Create(CultureInfo.InvariantCulture, $"{value:F0}%")) : GaugeValue.Empty;
}

namespace Template.LinuxApp.Views.Example;

using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Runtime.InteropServices;

using Microsoft.Extensions.Hosting;

using Template.LinuxApp.Services;
using Template.LinuxApp.Settings;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class DashboardViewModel : AppViewModelBase
{
    private readonly IPerformanceService performanceService;

    public bool IsSupported => performanceService.IsSupported;

    public string? Version { get; } = typeof(App).Assembly.GetName().Version?.ToString();

    public string Framework { get; } = RuntimeInformation.FrameworkDescription;

    public string RuntimeIdentifier { get; } = RuntimeInformation.RuntimeIdentifier;

    public string EnvironmentName { get; }

    public string SettingValue { get; }

    public bool IsKiosk { get; }

    public int ProcessId { get; } = Environment.ProcessId;

    public DateTime StartTime { get; }

    [ObservableProperty]
    public partial double? Cpu { get; set; }

    [ObservableProperty]
    public partial double? Memory { get; set; }

    [ObservableProperty]
    public partial double? Swap { get; set; }

    [ObservableProperty]
    public partial double? Temperature { get; set; }

    public ICommand ThemeCommand { get; }

    public ICommand ExitCommand { get; }

    public DashboardViewModel(IHostEnvironment environment, Setting setting, KioskSetting kioskSetting, IPerformanceService performanceService, ThemeService themeService, ExitService exitService)
    {
        this.performanceService = performanceService;
        EnvironmentName = environment.EnvironmentName;
        SettingValue = setting.Value;
        IsKiosk = kioskSetting.Enable;
        using (var process = Process.GetCurrentProcess())
        {
            StartTime = process.StartTime;
        }

        var scheduler = new SynchronizationContextScheduler(SynchronizationContext.Current!);
        Disposables.Add(Observable
            .FromEvent<EventHandler<EventArgs<PerformanceSample>>, EventArgs<PerformanceSample>>(static h => (_, e) => h(e), h => performanceService.Sampled += h, h => performanceService.Sampled -= h)
            .ObserveOn(scheduler)
            .Subscribe(x => Apply(x.Data)));

        ThemeCommand = MakeAsyncCommand<string>(x => themeService.ChangeAsync(x).AsTask());
        ExitCommand = MakeAsyncCommand(() => exitService.RequestExitAsync().AsTask());
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        if (performanceService.Latest is { } latest)
        {
            Apply(latest);
        }

        performanceService.Start();
        return Task.CompletedTask;
    }

    private void Apply(PerformanceSample sample)
    {
        Cpu = sample.Cpu.Current;
        Memory = sample.Memory.Current;
        Swap = sample.Swap.Current;
        Temperature = sample.CpuTemperature.Current;
    }
}

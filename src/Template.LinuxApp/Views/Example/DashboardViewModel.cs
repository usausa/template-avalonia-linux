namespace Template.LinuxApp.Views.Example;

using Avalonia.Threading;

using Template.LinuxApp.Components.Platform;
using Template.LinuxApp.Services;
using Template.LinuxApp.Settings;
using Template.LinuxApp.State;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class DashboardViewModel : AppViewModelBase
{
    private const double MegaByte = 1024 * 1024;

    private readonly ISystemMonitor systemMonitor;

    private readonly DispatcherTimer timer;

    public ObservableCollection<DeviceStatus> Devices { get; }

    public string SettingValue { get; }

    public bool IsSystemSupported => systemMonitor.IsSupported;

    [ObservableProperty]
    public partial string Cpu { get; set; } = "-";

    [ObservableProperty]
    public partial string Memory { get; set; } = "-";

    [ObservableProperty]
    public partial string LoadAverage { get; set; } = "-";

    [ObservableProperty]
    public partial string Temperature { get; set; } = "-";

    [ObservableProperty]
    public partial string Uptime { get; set; } = "-";

    public ICommand ThemeCommand { get; }

    public DashboardViewModel(Setting setting, DeviceState deviceState, ISystemMonitor systemMonitor, ThemeService themeService)
    {
        this.systemMonitor = systemMonitor;
        SettingValue = setting.Value;
        Devices = deviceState.Devices;

        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Refresh();

        ThemeCommand = MakeAsyncCommand<string>(x => themeService.ChangeAsync(x).AsTask());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Stop();
        }

        base.Dispose(disposing);
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        Refresh();
        timer.Start();
        return Task.CompletedTask;
    }

    public override Task OnNavigatingFromAsync(INavigationContext context)
    {
        timer.Stop();
        return Task.CompletedTask;
    }

    private void Refresh()
    {
        if (systemMonitor.Read() is not { } snapshot)
        {
            return;
        }

        var culture = CultureInfo.InvariantCulture;
        Cpu = String.Create(culture, $"{snapshot.CpuUsage:F1} %");
        Memory = String.Create(culture, $"{(snapshot.MemoryTotal - snapshot.MemoryAvailable) / MegaByte:F0} / {snapshot.MemoryTotal / MegaByte:F0} MB");
        LoadAverage = String.Create(culture, $"{snapshot.LoadAverage1:F2} / {snapshot.LoadAverage5:F2} / {snapshot.LoadAverage15:F2}");
        Temperature = snapshot.Temperature is { } temperature ? String.Create(culture, $"{temperature:F1} °C") : "-";
        Uptime = snapshot.Uptime.ToString(@"d\.hh\:mm\:ss", culture);
    }
}

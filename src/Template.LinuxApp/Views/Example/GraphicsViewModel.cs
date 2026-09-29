namespace Template.LinuxApp.Views.Example;

using Avalonia.Threading;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class GraphicsViewModel : AppViewModelBase
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

    private readonly TimeProvider timeProvider;

    private readonly DispatcherTimer timer;

    [ObservableProperty]
    public partial TimeSpan Time { get; set; }

    [ObservableProperty]
    public partial string TimeText { get; set; } = "--:--:--";

    public GraphicsViewModel(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;

        timer = new DispatcherTimer { Interval = RefreshInterval };
        timer.Tick += (_, _) => Refresh();
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
        var now = timeProvider.GetLocalNow();
        Time = new TimeSpan(now.Hour, now.Minute, now.Second);
        TimeText = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }
}

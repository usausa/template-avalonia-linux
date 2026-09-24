namespace Template.LinuxApp;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Smart.Mvvm.Resolver;

using Template.LinuxApp.Services;
using Template.LinuxApp.Settings;

// ReSharper disable once PartialTypeWithSinglePart
public partial class App : Application
{
    private IHost host = default!;

    private ILogger<App> log = default!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif

        host = CreateHost();

        ResolveProvider.Default.Provider = host.Services;

        log = host.Services.GetRequiredService<ILogger<App>>();

        // Exception hook
        AppDomain.CurrentDomain.UnhandledException += (_, args) => log.ErrorUnknownException((Exception)args.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            log.ErrorUnknownException(args.Exception);
            args.SetObserved();
        };
        Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            log.ErrorUnknownException(args.Exception);
            args.Handled = true;
            NotifyException(args.Exception);
        };
    }

    private static IHost CreateHost()
    {
        var builder = Host.CreateApplicationBuilder();

        // Container
        builder.ConfigureContainer();
        // Log
        builder.ConfigureLogging();
        // Components
        builder.ConfigureComponents();

        var host = builder.Build();
#if DEBUG
        if (host.Services is BunnyTail.DependencyInjection.GeneratedServiceProvider generatedProvider)
        {
            foreach (var line in BunnyTail.DependencyInjection.Diagnostics.ServiceFactoryReportExtensions.DescribeRuntimeFallbacks(generatedProvider).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
            {
                System.Diagnostics.Debug.WriteLine(line);
            }
        }

        // Setup navigator
        var navigator = host.Services.GetRequiredService<INavigator>();
        navigator.Navigated += (_, args) =>
        {
            // for debug
            System.Diagnostics.Debug.WriteLine($"Navigated: [{args.Context.FromId}]->[{args.Context.ToId}] : stacked=[{navigator.StackedCount}]");
        };
#endif
        return host;
    }

    // ReSharper disable once AsyncVoidMethod
    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // User setting
            var store = host.Services.GetRequiredService<UserSettingStore>();
            store.Load();

            // Theme
            host.Services.GetRequiredService<ThemeService>().Apply();

            // Main window
            var window = host.Services.GetRequiredService<MainWindow>();
            RestoreWindowPlacement(window, store.Value);
            window.Closing += (_, _) => SaveWindowPlacement(window, store.Value);

            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            window.Closed += async (_, _) =>
            {
                try
                {
                    await store.SaveAsync();
                    await host.ExitApplicationAsync();
                }
                finally
                {
                    desktop.Shutdown();
                }
            };
            desktop.MainWindow = window;

            // Start
            await host.StartApplicationAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    //--------------------------------------------------------------------------------
    // Window placement
    //--------------------------------------------------------------------------------

    private static void RestoreWindowPlacement(Window window, UserSetting setting)
    {
        if (setting.MainWindowPlacement is not { } placement)
        {
            return;
        }

        var position = new PixelPoint(placement.X, placement.Y);
        var width = placement.Width;
        var height = placement.Height;
        if ((window.Screens.ScreenFromPoint(position) ?? window.Screens.Primary) is { } screen)
        {
            var area = screen.WorkingArea;
            width = Math.Min(width, area.Width / screen.Scaling);
            height = Math.Min(height, area.Height / screen.Scaling);
            position = new PixelPoint(
                Math.Clamp(position.X, area.X, Math.Max(area.X, area.Right - (int)(width * screen.Scaling))),
                Math.Clamp(position.Y, area.Y, Math.Max(area.Y, area.Bottom - (int)(height * screen.Scaling))));
        }

        window.Position = position;
        window.Width = width;
        window.Height = height;
        if (placement.Maximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    private static void SaveWindowPlacement(Window window, UserSetting setting)
    {
        setting.MainWindowPlacement = new WindowPlacement
        {
            X = window.Position.X,
            Y = window.Position.Y,
            Width = window.Width,
            Height = window.Height,
            Maximized = window.WindowState == WindowState.Maximized
        };
    }

    //--------------------------------------------------------------------------------
    // Exception
    //--------------------------------------------------------------------------------

    private void NotifyException(Exception ex)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _ = host.Services.GetRequiredService<IDialogService>().NotifyAsync(ex.Message).AsTask();
        });
    }
}

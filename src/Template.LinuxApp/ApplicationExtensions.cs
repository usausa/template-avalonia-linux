namespace Template.LinuxApp;

using System.Runtime.InteropServices;

using BunnyTail.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Serilog;

using Smart.Avalonia;

using Template.LinuxApp.Components.Barcode;
using Template.LinuxApp.Components.Gamepad;
using Template.LinuxApp.Components.Motor;
using Template.LinuxApp.Components.Nfc;
using Template.LinuxApp.Components.Platform;
using Template.LinuxApp.Components.Printer;
using Template.LinuxApp.Components.Video;
using Template.LinuxApp.Devices.Input;
using Template.LinuxApp.Services;
using Template.LinuxApp.Settings;
using Template.LinuxApp.Shell;
using Template.LinuxApp.State;
using Template.LinuxApp.Views;

public static partial class ApplicationExtensions
{
    //--------------------------------------------------------------------------------
    // Container
    //--------------------------------------------------------------------------------

    public static HostApplicationBuilder ConfigureContainer(this HostApplicationBuilder builder)
    {
        builder.ConfigureContainer(new GeneratedServiceProviderFactory(static options => options.TrackTransientDisposables = false));

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Logging
    //--------------------------------------------------------------------------------

    public static HostApplicationBuilder ConfigureLogging(this HostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(options =>
        {
            options.ReadFrom.Configuration(builder.Configuration);
        });

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Components
    //--------------------------------------------------------------------------------

    public static HostApplicationBuilder ConfigureComponents(this HostApplicationBuilder builder)
    {
        builder.Services.AddAvaloniaServices();

        // Setting
        builder.Services.AddOptions<Setting>().BindConfiguration("Setting").ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton(static p => p.GetRequiredService<IOptions<Setting>>().Value);
        builder.Services.AddOptions<KioskSetting>().BindConfiguration("Kiosk").ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton(static p => p.GetRequiredService<IOptions<KioskSetting>>().Value);

        // Messenger
        builder.Services.AddSingleton<IReactiveMessenger>(ReactiveMessenger.Default);

        // Store
        builder.Services.AddSingleton<UserSettingStore>();

        // Navigation
        builder.Services.AddNavigator(static (_, config) =>
        {
            config.UseAvaloniaNavigationProvider();
            config.UseIdViewMapper(static m => m.AutoRegister(ViewSource()));
        });

        // Service
        builder.Services.AddServices();
        builder.Services.AddSingleton<IDialogService, DialogService>();

        // State
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<DeviceState>();

        // Input
        builder.Services.AddOptions<InputOption>().BindConfiguration("Input").ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton(static p => p.GetRequiredService<IOptions<InputOption>>().Value);
        builder.Services.AddSingleton<IInputDevice, PadInputDevice>();

        // Components
        builder.Services.AddOptions<GamepadReaderOption>().BindConfiguration("Gamepad").ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton(static p => p.GetRequiredService<IOptions<GamepadReaderOption>>().Value);
        builder.Services.AddSingleton<IGamepadReader, GamepadReader>();
        builder.Services.AddOptions<BarcodeReaderOption>().BindConfiguration("Barcode").ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton(static p => p.GetRequiredService<IOptions<BarcodeReaderOption>>().Value);
        builder.Services.AddSingleton<IBarcodeReader, BarcodeReader>();
        builder.Services.AddOptions<QrReaderOption>().BindConfiguration("Barcode").ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton(static p => p.GetRequiredService<IOptions<QrReaderOption>>().Value);
        builder.Services.AddSingleton<IQrReader, QrReader>();
        builder.Services.AddOptions<VideoSourceOption>().BindConfiguration("Camera").ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton(static p => p.GetRequiredService<IOptions<VideoSourceOption>>().Value);
        builder.Services.AddSingleton<IVideoSource, VideoSource>();
        builder.Services.AddOptions<FaceDetectorOption>().BindConfiguration("Detect").ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton(static p => p.GetRequiredService<IOptions<FaceDetectorOption>>().Value);
        builder.Services.AddSingleton<IFaceDetector, FaceDetector>();
        builder.Services.AddOptions<LinePrinterOption>().BindConfiguration("Printer").ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton(static p => p.GetRequiredService<IOptions<LinePrinterOption>>().Value);
        builder.Services.AddSingleton<ILinePrinter, LinePrinter>();
        builder.Services.AddOptions<ImagePrinterOption>().BindConfiguration("Printer").ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton(static p => p.GetRequiredService<IOptions<ImagePrinterOption>>().Value);
        builder.Services.AddSingleton<IImagePrinter, ImagePrinter>();
        builder.Services.AddOptions<MotorControllerOption>().BindConfiguration("Motor").ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton(static p => p.GetRequiredService<IOptions<MotorControllerOption>>().Value);
        builder.Services.AddSingleton<IMotorController, MotorController>();
        builder.Services.AddSingleton<ISuicaReader, SuicaReader>();
        builder.Services.AddSingleton<ISystemMonitor, SystemMonitor>();

        // Window
        builder.Services.AddSingleton<MainWindow>();
        builder.Services.AddSingleton<KioskController>();
        // View & ViewModel
        builder.Services.AddViews();
        builder.Services.AddViewModels();

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Startup
    //--------------------------------------------------------------------------------

    public static async ValueTask StartApplicationAsync(this IHost host)
    {
        // Start host
        await host.StartAsync().ConfigureAwait(false);

        // Startup log
        var log = host.Services.GetRequiredService<ILogger<App>>();
        var environment = host.Services.GetRequiredService<IHostEnvironment>();
        var kiosk = host.Services.GetRequiredService<KioskSetting>();
        ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);

        log.InfoStartup();
        log.InfoStartupSettingsRuntime(RuntimeInformation.OSDescription, RuntimeInformation.FrameworkDescription, RuntimeInformation.RuntimeIdentifier);
        log.InfoStartupSettingsGC(GCSettings.IsServerGC, GCSettings.LatencyMode, GCSettings.LargeObjectHeapCompactionMode);
        log.InfoStartupSettingsThreadPool(workerThreads, completionPortThreads);
        log.InfoStartupApplication(environment.ApplicationName, typeof(App).Assembly.GetName().Version);
        log.InfoStartupEnvironment(environment.EnvironmentName, environment.ContentRootPath);
        log.InfoStartupKiosk(kiosk.Enable, kiosk.HideCursor);

        // Device
        host.Services.GetRequiredService<IInputDevice>();
        host.Services.GetRequiredService<IBarcodeReader>();
        host.Services.GetRequiredService<IQrReader>();
        host.Services.GetRequiredService<IVideoSource>();
        host.Services.GetRequiredService<ILinePrinter>();
        host.Services.GetRequiredService<IImagePrinter>();
        host.Services.GetRequiredService<IMotorController>();
        host.Services.GetRequiredService<ISuicaReader>();

        // Navigate to view
        var navigator = host.Services.GetRequiredService<INavigator>();
        await navigator.ForwardAsync(ViewId.Dashboard).ConfigureAwait(false);
    }

    public static async ValueTask ExitApplicationAsync(this IHost host)
    {
        // Stop host
        await host.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        host.Dispose();
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    [ViewSource]
    public static partial IEnumerable<KeyValuePair<ViewId, Type>> ViewSource();

    //--------------------------------------------------------------------------------
    // Service
    //--------------------------------------------------------------------------------

    [ComponentRegistration(Lifetime.Singleton, "Service$")]
    public static partial IServiceCollection AddServices(this IServiceCollection services);

    //--------------------------------------------------------------------------------
    // View & ViewModel
    //--------------------------------------------------------------------------------

    [ComponentRegistration(Lifetime.Transient, "View$")]
    public static partial IServiceCollection AddViews(this IServiceCollection services);

    [ComponentRegistration(Lifetime.Transient, "ViewModel$")]
    public static partial IServiceCollection AddViewModels(this IServiceCollection services);
}

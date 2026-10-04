namespace Template.LinuxApp;

internal static partial class Log
{
    // Startup

    [LoggerMessage(Level = LogLevel.Information, Message = "Application start.")]
    public static partial void InfoStartup(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Runtime: os=[{osDescription}], framework=[{frameworkDescription}], rid=[{runtimeIdentifier}]")]
    public static partial void InfoStartupSettingsRuntime(this ILogger logger, string osDescription, string frameworkDescription, string runtimeIdentifier);

    [LoggerMessage(Level = LogLevel.Information, Message = "GCSettings: serverGC=[{isServerGC}], latencyMode=[{latencyMode}], largeObjectHeapCompactionMode=[{largeObjectHeapCompactionMode}]")]
    public static partial void InfoStartupSettingsGC(this ILogger logger, bool isServerGC, GCLatencyMode latencyMode, GCLargeObjectHeapCompactionMode largeObjectHeapCompactionMode);

    [LoggerMessage(Level = LogLevel.Information, Message = "ThreadPool: workerThreads=[{workerThreads}], completionPortThreads=[{completionPortThreads}]")]
    public static partial void InfoStartupSettingsThreadPool(this ILogger logger, int workerThreads, int completionPortThreads);

    [LoggerMessage(Level = LogLevel.Information, Message = "Application: application=[{application}], version=[{version}]")]
    public static partial void InfoStartupApplication(this ILogger logger, string application, Version? version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Environment: environment=[{environment}], contentRoot=[{contentRoot}]")]
    public static partial void InfoStartupEnvironment(this ILogger logger, string environment, string contentRoot);

    [LoggerMessage(Level = LogLevel.Information, Message = "Kiosk: enable=[{enable}], hideCursor=[{hideCursor}]")]
    public static partial void InfoStartupKiosk(this ILogger logger, bool enable, bool hideCursor);

    // Kiosk

    [LoggerMessage(Level = LogLevel.Information, Message = "Kiosk admin exit.")]
    public static partial void InfoKioskAdminExit(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kiosk admin PIN mismatch.")]
    public static partial void WarnKioskAdminPinMismatch(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kiosk admin code mismatch.")]
    public static partial void WarnKioskAdminCodeMismatch(this ILogger logger);

    // Device

    [LoggerMessage(Level = LogLevel.Information, Message = "Device disabled. name=[{name}]")]
    public static partial void InfoDeviceDisabled(this ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Device connected. name=[{name}]")]
    public static partial void InfoDeviceConnected(this ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Device disconnected. name=[{name}]")]
    public static partial void WarnDeviceDisconnected(this ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Device error. name=[{name}], message=[{message}]")]
    public static partial void WarnDeviceError(this ILogger logger, string name, string message);

    // Nfc

    [LoggerMessage(Level = LogLevel.Warning, Message = "Suica read failed. step=[{step}], status=[{status}], atr=[{atr}]")]
    public static partial void WarnSuicaReadFailed(this ILogger logger, string step, string status, string atr);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Suica session reset failed. status=[{status}]")]
    public static partial void WarnSuicaSessionResetFailed(this ILogger logger, string status);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Suica card inserted. atr=[{atr}]")]
    public static partial void DebugSuicaCardInserted(this ILogger logger, string atr);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Suica card ignored. atr=[{atr}]")]
    public static partial void DebugSuicaCardIgnored(this ILogger logger, string atr);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Suica card read. idm=[{idm}], balance=[{balance}], history=[{history}]")]
    public static partial void DebugSuicaCardRead(this ILogger logger, string idm, int balance, int history);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Suica reader reconnected. status=[{status}]")]
    public static partial void DebugSuicaReconnected(this ILogger logger, string status);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Suica session reset. attempt=[{attempt}]")]
    public static partial void DebugSuicaSessionReset(this ILogger logger, int attempt);

    // Platform

    [LoggerMessage(Level = LogLevel.Warning, Message = "Performance sample failed.")]
    public static partial void WarnPerformanceSampleFailed(this ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "System read failed. section=[{section}]")]
    public static partial void WarnSystemReadFailed(this ILogger logger, string section, Exception ex);

    // Capture

    [LoggerMessage(Level = LogLevel.Information, Message = "Screen captured. path=[{path}]")]
    public static partial void InfoScreenCaptured(this ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Screen capture failed. path=[{path}]")]
    public static partial void WarnScreenCaptureFailed(this ILogger logger, Exception ex, string path);

    // Error

    [LoggerMessage(Level = LogLevel.Error, Message = "Unknown exception.")]
    public static partial void ErrorUnknownException(this ILogger logger, Exception ex);
}

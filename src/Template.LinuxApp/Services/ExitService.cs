namespace Template.LinuxApp.Services;

using Avalonia.Controls.ApplicationLifetimes;

using Template.LinuxApp.Settings;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class ExitService
{
    private readonly ILogger<ExitService> log;

    private readonly KioskSetting setting;

    private readonly IDialogService dialogService;

    public ExitService(ILogger<ExitService> log, KioskSetting setting, IDialogService dialogService)
    {
        this.log = log;
        this.setting = setting;
        this.dialogService = dialogService;
    }

    public async ValueTask RequestExitAsync()
    {
        if (!String.IsNullOrEmpty(setting.AdminPin))
        {
            var pin = await dialogService.PinAsync("Admin PIN");
            if (pin is null)
            {
                return;
            }

            if (!String.Equals(pin, setting.AdminPin, StringComparison.Ordinal))
            {
                log.WarnKioskAdminPinMismatch();
                await dialogService.NotifyAsync("PIN is incorrect.");
                return;
            }
        }

        log.InfoKioskAdminExit();
        (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Close();
    }
}

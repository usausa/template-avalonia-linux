namespace Template.LinuxApp.Services;

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

using Template.LinuxApp.Views.Dialogs;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class DialogService : IDialogService
{
    private static Window? GetOwner() =>
        (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public async ValueTask<bool> ConfirmAsync(string message)
    {
        var owner = GetOwner();
        if (owner is null)
        {
            return false;
        }

        return await new ConfirmDialog { Message = message }.ShowDialog<bool>(owner);
    }

    public async ValueTask<string?> InputAsync(string title, string? initial = null)
    {
        var owner = GetOwner();
        if (owner is null)
        {
            return null;
        }

        return await new InputDialog { Title = title, Value = initial ?? string.Empty }.ShowDialog<string?>(owner);
    }

    public async ValueTask NotifyAsync(string message)
    {
        var owner = GetOwner();
        if (owner is null)
        {
            return;
        }

        await new NoticeDialog { Message = message }.ShowDialog(owner);
    }
}

namespace Template.LinuxApp.Services;

using Avalonia.Controls.ApplicationLifetimes;

using Template.LinuxApp.Controls;
using Template.LinuxApp.Views.Dialogs;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class DialogService : IDialogService
{
    private static DialogLayer? GetLayer() =>
        ((Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow)?.DialogLayer;

    public bool IsOpen => GetLayer()?.IsVisible ?? false;

    public async ValueTask<bool> ConfirmAsync(string message)
    {
        var layer = GetLayer();
        if (layer is null)
        {
            return false;
        }

        var dialog = new ConfirmDialog { Message = message };
        using (layer.Open(dialog))
        {
            return await dialog.Result;
        }
    }

    public async ValueTask<string?> InputAsync(string title, string? initial = null, bool password = false)
    {
        var layer = GetLayer();
        if (layer is null)
        {
            return null;
        }

        var dialog = new InputDialog { Title = title, Value = initial ?? string.Empty, Password = password };
        using (layer.Open(dialog))
        {
            return await dialog.Result;
        }
    }

    public async ValueTask NotifyAsync(string message)
    {
        var layer = GetLayer();
        if (layer is null)
        {
            return;
        }

        var dialog = new NoticeDialog { Message = message };
        using (layer.Open(dialog))
        {
            await dialog.Result;
        }
    }
}

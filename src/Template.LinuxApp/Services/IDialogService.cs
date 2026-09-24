namespace Template.LinuxApp.Services;

public interface IDialogService
{
    ValueTask<bool> ConfirmAsync(string message);

    ValueTask<string?> InputAsync(string title, string? initial = null);

    ValueTask NotifyAsync(string message);
}

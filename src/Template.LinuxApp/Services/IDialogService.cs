namespace Template.LinuxApp.Services;

public interface IDialogService
{
    bool IsOpen { get; }

    ValueTask<bool> ConfirmAsync(string message);

    ValueTask<string?> InputAsync(string title, string? initial = null);

    ValueTask<string?> PinAsync(string title);

    ValueTask NotifyAsync(string message);
}

namespace Template.LinuxApp.Views.Dialogs;

using Avalonia.Controls;
using Avalonia.Interactivity;

public sealed partial class ConfirmDialog : UserControl
{
    private readonly TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Message
    {
        get => MessageText.Text ?? string.Empty;
        set => MessageText.Text = value;
    }

    public Task<bool> Result => completion.Task;

    public ConfirmDialog()
    {
        InitializeComponent();
    }

    private void OnYesClick(object? sender, RoutedEventArgs e) => completion.TrySetResult(true);

    private void OnNoClick(object? sender, RoutedEventArgs e) => completion.TrySetResult(false);
}

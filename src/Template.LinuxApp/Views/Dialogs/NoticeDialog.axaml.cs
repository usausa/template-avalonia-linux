namespace Template.LinuxApp.Views.Dialogs;

using Avalonia.Controls;
using Avalonia.Interactivity;

public sealed partial class NoticeDialog : UserControl
{
    private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Message
    {
        get => MessageText.Text ?? string.Empty;
        set => MessageText.Text = value;
    }

    public Task Result => completion.Task;

    public NoticeDialog()
    {
        InitializeComponent();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => completion.TrySetResult();
}

namespace Template.LinuxApp.Views.Dialogs;

using Avalonia.Controls;
using Avalonia.Interactivity;

public sealed partial class InputDialog : UserControl
{
    private readonly TaskCompletionSource<string?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Title
    {
        get => TitleText.Text ?? string.Empty;
        set => TitleText.Text = value;
    }

    public string Value
    {
        get => ValueText.Text ?? string.Empty;
        set => ValueText.Text = value;
    }

    public bool Password
    {
        get => ValueText.PasswordChar != default;
        set => ValueText.PasswordChar = value ? '*' : default;
    }

    public Task<string?> Result => completion.Task;

    public InputDialog()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ValueText.Focus();
        ValueText.SelectAll();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => completion.TrySetResult(Value);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => completion.TrySetResult(null);
}

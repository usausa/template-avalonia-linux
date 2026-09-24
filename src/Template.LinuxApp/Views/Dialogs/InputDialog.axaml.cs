namespace Template.LinuxApp.Views.Dialogs;

using Avalonia.Controls;
using Avalonia.Interactivity;

public sealed partial class InputDialog : Window
{
    public string Value
    {
        get => ValueText.Text ?? string.Empty;
        set => ValueText.Text = value;
    }

    public InputDialog()
    {
        InitializeComponent();

        Opened += (_, _) =>
        {
            ValueText.Focus();
            ValueText.SelectAll();
        };
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close(Value);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}

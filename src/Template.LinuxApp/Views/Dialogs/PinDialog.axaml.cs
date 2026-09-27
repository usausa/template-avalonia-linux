namespace Template.LinuxApp.Views.Dialogs;

using Avalonia.Controls;
using Avalonia.Interactivity;

public sealed partial class PinDialog : UserControl
{
    private const int MaxLength = 12;

    private readonly TaskCompletionSource<string?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly StringBuilder digits = new();

    public string Title
    {
        get => TitleText.Text ?? string.Empty;
        set => TitleText.Text = value;
    }

    public Task<string?> Result => completion.Task;

    public PinDialog()
    {
        InitializeComponent();
    }

    private void OnDigitClick(object? sender, RoutedEventArgs e)
    {
        if ((sender is Button { Content: string digit }) && (digits.Length < MaxLength))
        {
            digits.Append(digit);
            UpdateValue();
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (digits.Length > 0)
        {
            digits.Length--;
            UpdateValue();
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => completion.TrySetResult(digits.ToString());

    private void OnCancelClick(object? sender, RoutedEventArgs e) => completion.TrySetResult(null);

    private void UpdateValue() => ValueText.Text = new string('\u25CF', digits.Length);
}

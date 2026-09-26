namespace Template.LinuxApp.Views.Dialogs;

using Avalonia.Controls;

public sealed partial class CodeDialog : UserControl
{
    public int Length
    {
        get;
        set
        {
            field = value;
            UpdateProgress();
        }
    }

    public int Entered
    {
        get;
        set
        {
            field = value;
            UpdateProgress();
        }
    }

    public string Message
    {
        get => MessageText.Text ?? string.Empty;
        set
        {
            MessageText.Text = value;
            MessageText.IsVisible = !String.IsNullOrEmpty(value);
        }
    }

    public CodeDialog()
    {
        InitializeComponent();
    }

    private void UpdateProgress() =>
        ProgressText.Text = new string('\u25CF', Math.Min(Entered, Length)) + new string('\u25CB', Math.Max(Length - Entered, 0));
}

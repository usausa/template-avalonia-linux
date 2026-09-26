namespace Template.LinuxApp.Controls;

using Avalonia.Controls;

public sealed class DialogLayer : Panel
{
    public DialogLayer()
    {
        IsVisible = false;
    }

    public IDisposable Open(Control dialog)
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager.GetFocusedElement();
        if (Children.Count > 0)
        {
            Children[^1].IsEnabled = false;
        }

        var frame = new Border { Classes = { "dialog-frame" }, Child = dialog };
        Children.Add(frame);
        IsVisible = true;

        return Disposable.Create(() =>
        {
            Children.Remove(frame);
            if (Children.Count > 0)
            {
                Children[^1].IsEnabled = true;
            }
            else
            {
                IsVisible = false;
            }

            focused?.Focus();
        });
    }
}

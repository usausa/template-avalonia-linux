namespace Template.LinuxApp.Shell;

using Template.LinuxApp.Views;

public sealed partial class NavigationItem : ObservableObject
{
    public ViewId Id { get; }

    public string Title { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public NavigationItem(ViewId id, string title)
    {
        Id = id;
        Title = title;
    }
}

namespace Template.LinuxApp.Settings;

public sealed class WindowPlacement
{
    public int X { get; set; }

    public int Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public bool Maximized { get; set; }
}

public sealed class UserSetting
{
    public string Theme { get; set; } = "System";

    public WindowPlacement? MainWindowPlacement { get; set; }
}

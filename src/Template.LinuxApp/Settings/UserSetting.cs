namespace Template.LinuxApp.Settings;

using System.Text.Json.Serialization;

public sealed class WindowPlacement
{
    public int X { get; set; }

    public int Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public bool Maximized { get; set; }
}

public sealed class MotorPoseSetting
{
    public int Servo1 { get; set; }

    public int Servo2 { get; set; }

    public byte Red { get; set; }

    public byte Green { get; set; }

    public byte Blue { get; set; }
}

public sealed class UserSetting
{
    public string Theme { get; set; } = "System";

    public WindowPlacement? MainWindowPlacement { get; set; }

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Collection<MotorPoseSetting> MotorPoses { get; } = [];
}

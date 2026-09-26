namespace Template.LinuxApp.Components.Video;

public sealed class VideoSourceOption
{
    public string Device { get; set; } = string.Empty;

    [Range(1, 8192)]
    public int Width { get; set; } = 640;

    [Range(1, 8192)]
    public int Height { get; set; } = 480;

    [Range(0, 240)]
    public int Fps { get; set; }
}

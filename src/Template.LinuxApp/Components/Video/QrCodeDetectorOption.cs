namespace Template.LinuxApp.Components.Video;

public sealed class QrCodeDetectorOption
{
    public bool Enable { get; set; }

    [Range(50, 5000)]
    public int Interval { get; set; } = 200;
}

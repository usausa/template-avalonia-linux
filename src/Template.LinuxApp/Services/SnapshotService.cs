namespace Template.LinuxApp.Services;

using Avalonia.Media.Imaging;

public sealed class SnapshotService
{
    private readonly TimeProvider timeProvider;

    public string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) is { Length: > 0 } pictures ? pictures : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Template.LinuxApp");

    public SnapshotService(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public string Save(Bitmap bitmap)
    {
        Directory.CreateDirectory(Folder);
        var file = Path.Combine(Folder, String.Create(CultureInfo.InvariantCulture, $"snapshot-{timeProvider.GetLocalNow():yyyyMMdd-HHmmss-fff}.png"));
        using var stream = File.Create(file);
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return file;
    }
}

namespace Template.LinuxApp.Components.Video;

using LinuxDotNet.Video4Linux2;

public sealed record CameraResolution(int Width, int Height);

public sealed record CameraFormat(PixelFormat PixelFormat, int Sizes);

public sealed record CameraDevice(
    string Device,
    string Name,
    string Driver,
    string BusInfo,
    IReadOnlyList<CameraFormat> Formats,
    IReadOnlyList<CameraResolution> Resolutions);

public static class CameraCatalog
{
    public static IReadOnlyList<CameraDevice> GetDevices()
    {
        if (!OperatingSystem.IsLinux())
        {
            return [];
        }

        try
        {
            return [.. VideoInfo.GetAllVideo()
                .Where(static x => x.IsAvailable && x.IsVideoCapture)
                .Select(ToDevice)
                .Where(static x => x.Resolutions.Count > 0)
                .OrderBy(static x => x.Device, StringComparer.Ordinal)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static CameraDevice ToDevice(VideoInfo info)
    {
        var resolutions = info.SupportedFormats
            .Where(static x => x.PixelFormat == PixelFormat.YUYV)
            .SelectMany(static x => x.SupportedResolutions)
            .Select(static x => new CameraResolution(x.Width, x.Height))
            .Distinct()
            .OrderBy(static x => x.Width * x.Height)
            .ToList();
        var formats = info.SupportedFormats.Select(static x => new CameraFormat(x.PixelFormat, x.SupportedResolutions.Count)).ToList();
        return new CameraDevice(info.Device, info.Name, info.Driver, info.BusInfo, formats, resolutions);
    }
}

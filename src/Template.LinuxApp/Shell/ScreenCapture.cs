namespace Template.LinuxApp.Shell;

using System.Runtime.Versioning;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using NAudio.Wave;
using NAudio.Wave.Alsa;

using Template.LinuxApp.Settings;

public sealed class ScreenCapture
{
    private static readonly Uri ShutterSoundUri = new("avares://Template.LinuxApp/Assets/Sounds/Shutter.wav");

    private readonly ILogger<ScreenCapture> log;

    private readonly TimeProvider timeProvider;

    private readonly INavigator navigator;

    private readonly CaptureSetting setting;

    private byte[]? shutterSound;

    public ScreenCapture(ILogger<ScreenCapture> log, TimeProvider timeProvider, INavigator navigator, CaptureSetting setting)
    {
        this.log = log;
        this.timeProvider = timeProvider;
        this.navigator = navigator;
        this.setting = setting;
    }

    public async Task<string?> CaptureAsync()
    {
        if (TopLevel.GetTopLevel(navigator.CurrentView as Visual) is not { } target)
        {
            return null;
        }

        PlayShutterSound();
        var name = String.Create(CultureInfo.InvariantCulture, $"{timeProvider.GetLocalNow():yyyyMMdd-HHmmss-fff}_{navigator.CurrentViewId}.png");
        var path = Path.GetFullPath(Path.Combine(setting.Directory, name));
        try
        {
            var image = Render(target);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, image).ConfigureAwait(false);
            log.InfoScreenCaptured(path);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.WarnScreenCaptureFailed(ex, path);
            return null;
        }
    }

    private void PlayShutterSound()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var data = shutterSound ??= LoadShutterSound();
        _ = PlayShutterSoundAsync(data);
    }

    [SupportedOSPlatform("linux")]
    private async Task PlayShutterSoundAsync(byte[] data)
    {
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        try
        {
            var reader = new WaveFileReader(new MemoryStream(data, false));
            await using (reader.ConfigureAwait(false))
            {
                using var output = new AlsaOut();
                var stopped = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
                output.PlaybackStopped += (_, e) => stopped.TrySetResult(e.Exception);
                output.Init(reader);
                output.Play();
                if (await stopped.Task.ConfigureAwait(false) is { } error)
                {
                    log.WarnShutterSoundFailed(error);
                }
            }
        }
        catch (Exception ex) when (ex is AlsaException or NotSupportedException or DllNotFoundException)
        {
            log.WarnShutterSoundFailed(ex);
        }
    }

    private static byte[] LoadShutterSound()
    {
        using var stream = AssetLoader.Open(ShutterSoundUri);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static byte[] Render(TopLevel target)
    {
        var mode = TextOptions.GetTextRenderingMode(target);
        TextOptions.SetTextRenderingMode(target, TextRenderingMode.Antialias);
        try
        {
            var scaling = target.RenderScaling;
            using var bitmap = new RenderTargetBitmap(PixelSize.FromSize(target.Bounds.Size, scaling), new Vector(96 * scaling, 96 * scaling));
            bitmap.Render(target);
            using var stream = new MemoryStream();
            bitmap.Save(stream, PngBitmapEncoderOptions.Default);
            return stream.ToArray();
        }
        finally
        {
            TextOptions.SetTextRenderingMode(target, mode);
        }
    }
}

namespace Template.LinuxApp.Components.Video;

using Template.LinuxApp.State;

using ZXing;
using ZXing.Common;

public sealed record QrCodeBox(string Text, float Left, float Top, float Right, float Bottom);

public interface IQrCodeDetector
{
    event EventHandler<EventArgs<IReadOnlyList<QrCodeBox>>>? Detected;

    bool IsEnabled { get; }

    bool IsRunning { get; }

    void Start();

    ValueTask StopAsync();
}

public sealed class QrCodeDetector : IQrCodeDetector, IDisposable
{
    private const float Margin = 0.2f;

    private readonly TimeProvider timeProvider;

    private readonly QrCodeDetectorOption option;

    private readonly IVideoSource videoSource;

    private readonly DeviceStatus status;

    private readonly BarcodeReaderGeneric reader = new()
    {
        AutoRotate = false,
        Options = new DecodingOptions
        {
            PossibleFormats = [BarcodeFormat.QR_CODE],
            TryHarder = false
        }
    };

    private byte[] frameBuffer = [];

    private CancellationTokenSource? cts;

    private Task? loopTask;

    public event EventHandler<EventArgs<IReadOnlyList<QrCodeBox>>>? Detected;

    public bool IsEnabled => status.IsEnabled;

    public bool IsRunning => loopTask is not null;

    public QrCodeDetector(TimeProvider timeProvider, QrCodeDetectorOption option, DeviceState deviceState, IVideoSource videoSource)
    {
        this.timeProvider = timeProvider;
        this.option = option;
        this.videoSource = videoSource;
        status = deviceState.Register("QR detector", option.Enable);
    }

    public void Dispose()
    {
        StopAsync().AsTask().GetAwaiter().GetResult();
    }

    public void Start()
    {
        if (!status.IsEnabled || (loopTask is not null))
        {
            return;
        }

        cts = new CancellationTokenSource();
        var token = cts.Token;
        loopTask = Task.Run(() => LoopAsync(token), token);
        status.ReportStarted();
    }

    public async ValueTask StopAsync()
    {
        if ((cts is null) || (loopTask is null))
        {
            return;
        }

        await cts.CancelAsync().ConfigureAwait(false);
        try
        {
            await loopTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        cts.Dispose();
        cts = null;
        loopTask = null;
        status.ReportStopped();
    }

    private async Task LoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(option.Interval), timeProvider);
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            var width = videoSource.Width;
            var height = videoSource.Height;
            if (!videoSource.IsRunning || (width <= 0) || (height <= 0))
            {
                continue;
            }

            var size = width * height * 4;
            if (frameBuffer.Length != size)
            {
                frameBuffer = new byte[size];
            }

            if (!videoSource.TryReadFrame(frameBuffer))
            {
                continue;
            }

            var results = reader.DecodeMultiple(frameBuffer, width, height, RGBLuminanceSource.BitmapFormat.RGBA32);
            var codes = results is null ? [] : results.Select(x => ToCode(x, width, height)).ToList();
            if (codes.Count > 0)
            {
                status.ReportEvent();
            }

            Detected?.Invoke(this, new EventArgs<IReadOnlyList<QrCodeBox>>(codes));
        }
    }

    private static QrCodeBox ToCode(Result result, int width, int height)
    {
        var points = result.ResultPoints;
        if ((points is null) || (points.Length == 0))
        {
            return new QrCodeBox(result.Text, 0, 0, 0, 0);
        }

        var left = points.Min(static x => x.X);
        var right = points.Max(static x => x.X);
        var top = points.Min(static x => x.Y);
        var bottom = points.Max(static x => x.Y);
        var marginX = (right - left) * Margin;
        var marginY = (bottom - top) * Margin;
        return new QrCodeBox(
            result.Text,
            Math.Clamp((left - marginX) / width, 0f, 1f),
            Math.Clamp((top - marginY) / height, 0f, 1f),
            Math.Clamp((right + marginX) / width, 0f, 1f),
            Math.Clamp((bottom + marginY) / height, 0f, 1f));
    }
}

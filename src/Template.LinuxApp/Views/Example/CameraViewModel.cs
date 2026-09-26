namespace Template.LinuxApp.Views.Example;

using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

using Template.LinuxApp.Components.Video;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class CameraViewModel : AppViewModelBase
{
    private const float Alpha = 0.5f;

    private readonly TimeProvider timeProvider;

    private readonly IDispatcher dispatcher;

    private readonly IVideoSource videoSource;

    private readonly DispatcherTimer statusTimer;

    private readonly List<FaceBox> faceBuffer = [];

    private byte[] frameBuffer = [];

    private WriteableBitmap? bitmap;

    private int updating;

    private long lastStatusTimestamp;

    private int frameCount;

    private int lastGc0Count;

    private int lastGc1Count;

    private int lastGc2Count;

    [ObservableProperty]
    public partial WriteableBitmap? Bitmap { get; set; }

    public ObservableCollection<FaceBox> FaceBoxes { get; } = [];

    [ObservableProperty]
    public partial int FrameWidth { get; set; } = 640;

    [ObservableProperty]
    public partial int FrameHeight { get; set; } = 480;

    [ObservableProperty]
    public partial float Fps { get; set; }

    [ObservableProperty]
    public partial float Gc0PerSec { get; set; }

    [ObservableProperty]
    public partial float Gc1PerSec { get; set; }

    [ObservableProperty]
    public partial float Gc2PerSec { get; set; }

    [ObservableProperty]
    public partial bool IsStarted { get; set; }

    public ICommand StartCommand { get; }

    public ICommand StopCommand { get; }

    public CameraViewModel(TimeProvider timeProvider, IDispatcher dispatcher, IVideoSource videoSource)
    {
        this.timeProvider = timeProvider;
        this.dispatcher = dispatcher;
        this.videoSource = videoSource;

        statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        statusTimer.Tick += (_, _) => UpdateStatus();

        Disposables.Add(Observable
            .FromEventPattern(h => videoSource.FrameUpdated += h, h => videoSource.FrameUpdated -= h)
            .Subscribe(_ => OnFrameUpdated()));

        StartCommand = MakeDelegateCommand(StartCapture, () => !IsStarted);
        StopCommand = MakeAsyncCommand(StopCaptureAsync, () => IsStarted);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            statusTimer.Stop();
            Bitmap = null;
            bitmap?.Dispose();
            bitmap = null;
        }

        base.Dispose(disposing);
    }

    public override Task OnNavigatingFromAsync(INavigationContext context) =>
        IsStarted ? StopCaptureAsync() : Task.CompletedTask;

    protected override Task OnShellStartAsync()
    {
        if (IsStarted)
        {
            return StopCaptureAsync();
        }

        StartCapture();
        return Task.CompletedTask;
    }

    private void StartCapture()
    {
        videoSource.Start();
        IsStarted = true;

        frameCount = 0;
        lastGc0Count = GC.CollectionCount(0);
        lastGc1Count = GC.CollectionCount(1);
        lastGc2Count = GC.CollectionCount(2);
        lastStatusTimestamp = timeProvider.GetTimestamp();
        statusTimer.Start();
    }

    private async Task StopCaptureAsync()
    {
        statusTimer.Stop();
        await videoSource.StopAsync();
        IsStarted = false;
    }

    private void OnFrameUpdated()
    {
        if (Interlocked.Exchange(ref updating, 1) == 0)
        {
            dispatcher.Post(UpdateFrame);
        }
    }

    private void UpdateFrame()
    {
        Interlocked.Exchange(ref updating, 0);

        var width = videoSource.Width;
        var height = videoSource.Height;
        if (!IsStarted || (width <= 0) || (height <= 0))
        {
            return;
        }

        if ((bitmap is null) || (bitmap.PixelSize.Width != width) || (bitmap.PixelSize.Height != height))
        {
            Bitmap = null;
            bitmap?.Dispose();
            bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Premul);
            FrameWidth = width;
            FrameHeight = height;
        }

        var size = width * height * 4;
        if (frameBuffer.Length != size)
        {
            frameBuffer = new byte[size];
        }

        if (!videoSource.TryReadFrame(frameBuffer, faceBuffer))
        {
            return;
        }

        using (var locked = bitmap.Lock())
        {
            if (locked.RowBytes != width * 4)
            {
                return;
            }

            Marshal.Copy(frameBuffer, 0, locked.Address, size);
        }

        FaceBoxes.Clear();
        foreach (var box in faceBuffer)
        {
            FaceBoxes.Add(box);
        }

        Bitmap = null;
        Bitmap = bitmap;

        frameCount++;
    }

    private void UpdateStatus()
    {
        var now = timeProvider.GetTimestamp();
        var elapsed = (float)timeProvider.GetElapsedTime(lastStatusTimestamp, now).TotalSeconds;
        lastStatusTimestamp = now;
        if (elapsed <= 0)
        {
            return;
        }

        Fps = Smooth(frameCount / elapsed, Fps);
        frameCount = 0;

        var gc0Count = GC.CollectionCount(0);
        var gc1Count = GC.CollectionCount(1);
        var gc2Count = GC.CollectionCount(2);
        Gc0PerSec = Smooth((gc0Count - lastGc0Count) / elapsed, Gc0PerSec);
        Gc1PerSec = Smooth((gc1Count - lastGc1Count) / elapsed, Gc1PerSec);
        Gc2PerSec = Smooth((gc2Count - lastGc2Count) / elapsed, Gc2PerSec);
        lastGc0Count = gc0Count;
        lastGc1Count = gc1Count;
        lastGc2Count = gc2Count;

        static float Smooth(float current, float previous) => (current * Alpha) + (previous * (1.0f - Alpha));
    }
}

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

    private const int MaxQrHistory = 8;

    private static readonly TimeSpan QrRepeatInterval = TimeSpan.FromSeconds(3);

    private static readonly string SnapshotDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) is { Length: > 0 } pictures ? pictures : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Template.LinuxApp");

    private readonly TimeProvider timeProvider;

    private readonly IDispatcher dispatcher;

    private readonly IVideoSource videoSource;

    private readonly IQrCodeDetector qrCodeDetector;

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

    private int snapshotCount;

    private string? lastQrText;

    private long lastQrTimestamp;

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

    [ObservableProperty]
    public partial IReadOnlyList<CameraDevice> Cameras { get; set; } = [];

    [ObservableProperty]
    public partial CameraDevice? SelectedCamera { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<CameraResolution> Resolutions { get; set; } = [];

    [ObservableProperty]
    public partial CameraResolution? SelectedResolution { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<InfoItem> CameraInfo { get; set; } = [];

    [ObservableProperty]
    public partial Bitmap? LastSnapshot { get; set; }

    [ObservableProperty]
    public partial string SnapshotText { get; set; } = SnapshotDirectory;

    public bool IsQrEnabled => qrCodeDetector.IsEnabled;

    [ObservableProperty]
    public partial bool IsQrScan { get; set; } = true;

    [ObservableProperty]
    public partial IReadOnlyList<QrCodeBox>? QrCodes { get; set; }

    [ObservableProperty]
    public partial string QrText { get; set; } = "—";

    public ObservableCollection<string> QrHistory { get; } = [];

    public ICommand StartCommand { get; }

    public ICommand StopCommand { get; }

    public ICommand ApplyCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand SnapshotCommand { get; }

    public CameraViewModel(TimeProvider timeProvider, IDispatcher dispatcher, IVideoSource videoSource, IQrCodeDetector qrCodeDetector)
    {
        this.timeProvider = timeProvider;
        this.dispatcher = dispatcher;
        this.videoSource = videoSource;
        this.qrCodeDetector = qrCodeDetector;

        statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        statusTimer.Tick += (_, _) => UpdateStatus();

        Disposables.Add(Observable
            .FromEventPattern(h => videoSource.FrameUpdated += h, h => videoSource.FrameUpdated -= h)
            .Subscribe(_ => OnFrameUpdated()));
        Disposables.Add(Observable
            .FromEvent<EventHandler<EventArgs<IReadOnlyList<QrCodeBox>>>, EventArgs<IReadOnlyList<QrCodeBox>>>(static h => (_, e) => h(e), h => qrCodeDetector.Detected += h, h => qrCodeDetector.Detected -= h)
            .Subscribe(x => dispatcher.Post(() => ApplyQr(x.Data))));

        StartCommand = MakeDelegateCommand(StartCapture, () => !IsStarted);
        StopCommand = MakeAsyncCommand(StopCaptureAsync, () => IsStarted);
        ApplyCommand = MakeAsyncCommand(ApplyAsync, () => (SelectedCamera is not null) && (SelectedResolution is not null));
        RefreshCommand = MakeAsyncCommand(RefreshCamerasAsync);
        SnapshotCommand = MakeDelegateCommand(TakeSnapshot, () => IsStarted);

        SubscribeSelectedCamera(x =>
        {
            Resolutions = x?.Resolutions ?? [];
            SelectedResolution = Resolutions.FirstOrDefault(r => (r.Width == videoSource.RequestedWidth) && (r.Height == videoSource.RequestedHeight)) ?? (Resolutions.Count > 0 ? Resolutions[^1] : null);
            CameraInfo = FormatCamera(x);
        });
        SubscribeIsQrScan(x =>
        {
            if (!IsStarted)
            {
                return;
            }

            if (x)
            {
                qrCodeDetector.Start();
            }
            else
            {
                _ = StopQrAsync();
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            statusTimer.Stop();
            Bitmap = null;
            bitmap?.Dispose();
            bitmap = null;
            LastSnapshot?.Dispose();
            LastSnapshot = null;
        }

        base.Dispose(disposing);
    }

    public override Task OnNavigatedToAsync(INavigationContext context) => RefreshCamerasAsync();

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

    private async Task RefreshCamerasAsync()
    {
        Cameras = await Task.Run(CameraCatalog.GetDevices);
        SelectedCamera = Cameras.FirstOrDefault(x => x.Device == videoSource.Device) ?? (Cameras.Count > 0 ? Cameras[0] : null);
    }

    private async Task ApplyAsync()
    {
        if ((SelectedCamera is not { } camera) || (SelectedResolution is not { } resolution))
        {
            return;
        }

        await videoSource.ChangeAsync(camera.Device, resolution.Width, resolution.Height);
    }

    private void TakeSnapshot()
    {
        if (bitmap is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(SnapshotDirectory);
            var file = Path.Combine(SnapshotDirectory, String.Create(CultureInfo.InvariantCulture, $"snapshot-{timeProvider.GetLocalNow():yyyyMMdd-HHmmss-fff}.png"));
            using (var stream = File.Create(file))
            {
                bitmap.Save(stream, PngBitmapEncoderOptions.Default);
            }

            var previous = LastSnapshot;
            LastSnapshot = new Bitmap(file);
            previous?.Dispose();
            snapshotCount++;
            SnapshotText = String.Create(CultureInfo.InvariantCulture, $"{file} ({snapshotCount})");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SnapshotText = ex.Message;
        }
    }

    private static List<InfoItem> FormatCamera(CameraDevice? camera)
    {
        if (camera is null)
        {
            return [];
        }

        return
        [
            new InfoItem("Name", camera.Name),
            new InfoItem("Driver", camera.Driver),
            new InfoItem("Bus", camera.BusInfo),
            new InfoItem("Formats", camera.Formats)
        ];
    }

    private void StartCapture()
    {
        videoSource.Start();
        IsStarted = true;
        if (IsQrScan)
        {
            qrCodeDetector.Start();
        }

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
        await StopQrAsync();
        await videoSource.StopAsync();
        IsStarted = false;
    }

    private async Task StopQrAsync()
    {
        await qrCodeDetector.StopAsync();
        QrCodes = null;
    }

    private void ApplyQr(IReadOnlyList<QrCodeBox> codes)
    {
        if (!IsStarted || !IsQrScan)
        {
            return;
        }

        QrCodes = codes;
        if (codes.Count == 0)
        {
            return;
        }

        var text = codes[0].Text;
        var now = timeProvider.GetTimestamp();
        if ((text != lastQrText) || (timeProvider.GetElapsedTime(lastQrTimestamp, now) >= QrRepeatInterval))
        {
            QrHistory.Insert(0, String.Create(CultureInfo.InvariantCulture, $"{timeProvider.GetLocalNow():HH:mm:ss}  {text}"));
            while (QrHistory.Count > MaxQrHistory)
            {
                QrHistory.RemoveAt(QrHistory.Count - 1);
            }
        }

        QrText = text;
        lastQrText = text;
        lastQrTimestamp = now;
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

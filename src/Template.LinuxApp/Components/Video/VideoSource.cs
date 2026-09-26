namespace Template.LinuxApp.Components.Video;

using LinuxDotNet.Video4Linux2;

using Template.LinuxApp.Helpers;
using Template.LinuxApp.State;

public interface IVideoSource
{
    event EventHandler? FrameUpdated;

    bool IsRunning { get; }

    int Width { get; }

    int Height { get; }

    void Start();

    ValueTask StopAsync();

    bool TryReadFrame(Span<byte> destination, ICollection<FaceBox> faceBoxes);
}

public sealed class VideoSource : IVideoSource, IDisposable
{
    private const int SlotCount = 4;

    private const int Depth = 4;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(5);

    private readonly Lock sync = new();

    private readonly TimeProvider timeProvider;

    private readonly VideoSourceOption option;

    private readonly IFaceDetector faceDetector;

    private readonly DeviceStatus status;

    private CancellationTokenSource? cts;

    private Task? loopTask;

    private VideoCapture? capture;

    private BufferManager? bufferManager;

    private long lastFrameTimestamp;

    public event EventHandler? FrameUpdated;

    public bool IsRunning => loopTask is not null;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public VideoSource(TimeProvider timeProvider, VideoSourceOption option, DeviceState deviceState, IFaceDetector faceDetector)
    {
        this.timeProvider = timeProvider;
        this.option = option;
        this.faceDetector = faceDetector;
        status = deviceState.Register("Camera", !String.IsNullOrEmpty(option.Device));
    }

    public void Dispose()
    {
        StopAsync().AsTask().GetAwaiter().GetResult();

        lock (sync)
        {
            bufferManager?.Dispose();
            bufferManager = null;
        }
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

    public bool TryReadFrame(Span<byte> destination, ICollection<FaceBox> faceBoxes)
    {
        lock (sync)
        {
            var slot = bufferManager?.LastUpdatedSlot();
            if (slot is null)
            {
                return false;
            }

            lock (slot.Lock)
            {
                if (destination.Length < slot.Buffer.Length)
                {
                    return false;
                }

                slot.Buffer.CopyTo(destination);
                faceBoxes.Clear();
                foreach (var box in slot.FaceBoxes)
                {
                    faceBoxes.Add(box);
                }
            }
        }

        return true;
    }

    private async Task LoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (capture is null)
                {
                    if (File.Exists(option.Device))
                    {
                        Open();
                    }
                }
                else if (timeProvider.GetElapsedTime(Interlocked.Read(ref lastFrameTimestamp)) > FrameTimeout)
                {
                    Close();
                    status.ReportDisconnected();
                }

                await Task.Delay(CheckInterval, timeProvider, token).ConfigureAwait(false);
            }
        }
        finally
        {
            Close();
        }
    }

    private void Open()
    {
        var video = new VideoCapture(option.Device);
        if (!video.Open(option.Width, option.Height))
        {
            video.Dispose();
            status.ReportError("Failed to open the camera.");
            return;
        }

        lock (sync)
        {
            if ((bufferManager is null) || (bufferManager.Width != video.Width) || (bufferManager.Height != video.Height))
            {
                bufferManager?.Dispose();
                bufferManager = new BufferManager(SlotCount, video.Width, video.Height, Depth);
            }

            Width = video.Width;
            Height = video.Height;
        }

        Interlocked.Exchange(ref lastFrameTimestamp, timeProvider.GetTimestamp());
        video.FrameCaptured += OnFrameCaptured;
        if (!video.StartCapture(option.Fps))
        {
            video.FrameCaptured -= OnFrameCaptured;
            video.Dispose();
            status.ReportError("Failed to start the capture.");
            return;
        }

        capture = video;
        status.ReportConnected();
    }

    private void Close()
    {
        if (capture is null)
        {
            return;
        }

        capture.FrameCaptured -= OnFrameCaptured;
        capture.StopCapture();
        capture.Dispose();
        capture = null;
    }

    private void OnFrameCaptured(FrameBuffer frame)
    {
        BufferManager? manager;
        lock (sync)
        {
            manager = bufferManager;
        }

        if (manager is null)
        {
            return;
        }

        var slot = manager.NextSlot();
        lock (slot.Lock)
        {
            ImageHelper.ConvertYuyvToRgba(frame.Span, slot.Buffer);
            faceDetector.Detect(slot.Buffer, manager.Width, manager.Height, slot.FaceBoxes);
            slot.MarkUpdated();
        }

        Interlocked.Exchange(ref lastFrameTimestamp, timeProvider.GetTimestamp());
        status.ReportEvent();
        FrameUpdated?.Invoke(this, EventArgs.Empty);
    }
}

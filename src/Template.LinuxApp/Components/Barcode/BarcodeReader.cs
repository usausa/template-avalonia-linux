namespace Template.LinuxApp.Components.Barcode;

using System.Collections.Frozen;

using LinuxDotNet.InputEvent;

using Template.LinuxApp.State;

public sealed class BarcodeEventArgs : EventArgs
{
    public string Code { get; }

    public BarcodeEventArgs(string code)
    {
        Code = code;
    }
}

public interface IBarcodeReader
{
    event EventHandler<BarcodeEventArgs>? Scanned;

    void Start();

    ValueTask StopAsync();
}

public sealed class BarcodeReader : IBarcodeReader, IDisposable
{
    private const int ReadTimeout = 100;

    private const ushort EnterCode = 28;

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2);

    private static readonly FrozenDictionary<ushort, char> KeyMap = new Dictionary<ushort, char>
    {
        { 2, '1' }, { 3, '2' }, { 4, '3' }, { 5, '4' }, { 6, '5' },
        { 7, '6' }, { 8, '7' }, { 9, '8' }, { 10, '9' }, { 11, '0' },
        { 16, 'Q' }, { 17, 'W' }, { 18, 'E' }, { 19, 'R' }, { 20, 'T' },
        { 21, 'Y' }, { 22, 'U' }, { 23, 'I' }, { 24, 'O' }, { 25, 'P' },
        { 30, 'A' }, { 31, 'S' }, { 32, 'D' }, { 33, 'F' }, { 34, 'G' },
        { 35, 'H' }, { 36, 'J' }, { 37, 'K' }, { 38, 'L' },
        { 44, 'Z' }, { 45, 'X' }, { 46, 'C' }, { 47, 'V' }, { 48, 'B' },
        { 49, 'N' }, { 50, 'M' },
        { 57, ' ' },
        { 12, '-' },
        { 13, '=' },
        { 26, '[' }, { 27, ']' }, { 39, ';' }, { 40, '\'' },
        { 41, '`' }, { 43, '\\' }, { 51, ',' }, { 52, '.' }, { 53, '/' }
    }.ToFrozenDictionary();

    private readonly TimeProvider timeProvider;

    private readonly BarcodeReaderOption option;

    private readonly DeviceStatus status;

    private CancellationTokenSource? cts;

    private Task? loopTask;

    public event EventHandler<BarcodeEventArgs>? Scanned;

    public BarcodeReader(TimeProvider timeProvider, BarcodeReaderOption option, DeviceState deviceState)
    {
        this.timeProvider = timeProvider;
        this.option = option;
        status = deviceState.Register("Barcode", !String.IsNullOrEmpty(option.Name));
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
        while (!token.IsCancellationRequested)
        {
            if (FindDevice() is { } path)
            {
                using var device = new EventDevice(path);
                try
                {
                    device.Open(true);
                    status.ReportConnected();
                    Read(device, token);
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    status.ReportDisconnected();
                    status.ReportError(ex.Message);
                }
            }

            await Task.Delay(RetryInterval, timeProvider, token).ConfigureAwait(false);
        }
    }

    private string? FindDevice() =>
        EventDeviceInfo.GetDevices().FirstOrDefault(x => x.Name.Contains(option.Name, StringComparison.OrdinalIgnoreCase))?.Device;

    private void Read(EventDevice device, CancellationToken token)
    {
        var buffer = new StringBuilder();
        while (!token.IsCancellationRequested)
        {
            if (!device.Read(out var result, ReadTimeout) ||
                (result.Type != EventType.Key) ||
                ((EventValue)result.Value != EventValue.Pressed))
            {
                continue;
            }

            if (result.Code == EnterCode)
            {
                status.ReportEvent();
                Scanned?.Invoke(this, new BarcodeEventArgs(buffer.ToString()));
                buffer.Clear();
            }
            else if (KeyMap.TryGetValue(result.Code, out var c))
            {
                buffer.Append(c);
            }
        }
    }
}

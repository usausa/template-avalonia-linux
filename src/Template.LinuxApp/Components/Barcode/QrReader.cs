namespace Template.LinuxApp.Components.Barcode;

using System.IO.Ports;

using Mofucat.SerialIO;

using Template.LinuxApp.State;

public interface IQrReader
{
    event EventHandler<BarcodeEventArgs>? Scanned;

    void Start();

    ValueTask StopAsync();

    void ResumeReading();

    void PauseReading();
}

public sealed class QrReader : IQrReader, IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);

    private static readonly byte[] ResumeCommand = [.. "R\r"u8];

    private static readonly byte[] PauseCommand = [.. "Z\r"u8];

    private readonly Lock sync = new();

    private readonly TimeProvider timeProvider;

    private readonly QrReaderOption option;

    private readonly DeviceStatus status;

    private CancellationTokenSource? cts;

    private Task? loopTask;

    private SerialPort? port;

    private SerialLineReader? reader;

    public event EventHandler<BarcodeEventArgs>? Scanned;

    public QrReader(TimeProvider timeProvider, QrReaderOption option, DeviceState deviceState)
    {
        this.timeProvider = timeProvider;
        this.option = option;
        status = deviceState.Register("QR", !String.IsNullOrEmpty(option.Port));
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

    public void ResumeReading() => Write(ResumeCommand);

    public void PauseReading() => Write(PauseCommand);

    private async Task LoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (port is null)
                {
                    if (File.Exists(option.Port))
                    {
                        Open();
                    }
                }
                else if (!File.Exists(option.Port))
                {
                    Close();
                    status.ReportDisconnected();
                }

                await Task.Delay(CheckInterval, timeProvider, token).ConfigureAwait(false);
            }
        }
        finally
        {
            Write(ResumeCommand);
            Close();
        }
    }

    private void Open()
    {
        var serial = new SerialPort(option.Port)
        {
            BaudRate = 19200,
            Parity = Parity.None,
            DataBits = 8,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };
        try
        {
            serial.Open();
            serial.DiscardInBuffer();
            serial.DiscardOutBuffer();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            serial.Dispose();
            status.ReportError(ex.Message);
            return;
        }

        var lineReader = new SerialLineReader(serial, [(byte)'\r']);
        lineReader.LineReceived += OnLineReceived;
        lock (sync)
        {
            port = serial;
            reader = lineReader;
        }

        status.ReportConnected();
    }

    private void Close()
    {
        lock (sync)
        {
            if (reader is not null)
            {
                reader.LineReceived -= OnLineReceived;
                reader.Dispose();
                reader = null;
            }

            port?.Dispose();
            port = null;
        }
    }

    private void Write(byte[] command)
    {
        lock (sync)
        {
            if (port is null)
            {
                return;
            }

            try
            {
                port.Write(command, 0, command.Length);
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException)
            {
                status.ReportError(ex.Message);
            }
        }
    }

    private void OnLineReceived(object? sender, ReadOnlySpan<byte> bytes)
    {
        status.ReportEvent();
        Scanned?.Invoke(this, new BarcodeEventArgs(Encoding.UTF8.GetString(bytes)));
    }
}

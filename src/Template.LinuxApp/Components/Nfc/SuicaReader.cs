namespace Template.LinuxApp.Components.Nfc;

using PCSC;
using PCSC.Exceptions;
using PCSC.Monitoring;

using Template.LinuxApp.Domain.Logic;
using Template.LinuxApp.State;

public sealed record SuicaHistoryRecord(DateTime DateTime, byte Terminal, byte Process, int Balance);

public sealed class SuicaReadEventArgs : EventArgs
{
    public string Idm { get; }

    public int Balance { get; }

    public IReadOnlyList<SuicaHistoryRecord> History { get; }

    public SuicaReadEventArgs(string idm, int balance, IReadOnlyList<SuicaHistoryRecord> history)
    {
        Idm = idm;
        Balance = balance;
        History = history;
    }
}

public interface ISuicaReader
{
    event EventHandler<SuicaReadEventArgs>? CardRead;

    void Start();

    ValueTask StopAsync();
}

public sealed class SuicaReader : ISuicaReader, IDisposable
{
    private const int BlockSize = 16;

    private const int HistoryCount = 20;

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2);

    private readonly TimeProvider timeProvider;

    private readonly DeviceStatus status;

    private CancellationTokenSource? cts;

    private Task? loopTask;

    public event EventHandler<SuicaReadEventArgs>? CardRead;

    public SuicaReader(TimeProvider timeProvider, DeviceState deviceState)
    {
        this.timeProvider = timeProvider;
        status = deviceState.Register("NFC", true);
    }

    public void Dispose()
    {
        StopAsync().AsTask().GetAwaiter().GetResult();
    }

    public void Start()
    {
        if (loopTask is not null)
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
            string? readerName;
            try
            {
                readerName = FindReader();
            }
            catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException)
            {
                status.ReportError(ex.Message);
                return;
            }

            if (readerName is not null)
            {
                await MonitorAsync(readerName, token).ConfigureAwait(false);
            }

            await Task.Delay(RetryInterval, timeProvider, token).ConfigureAwait(false);
        }
    }

    private string? FindReader()
    {
        try
        {
            using var context = ContextFactory.Instance.Establish(SCardScope.System);
            var readers = context.GetReaders();
            return readers.Length > 0 ? readers[0] : null;
        }
        catch (PCSCException ex)
        {
            status.ReportError(ex.Message);
            return null;
        }
    }

    private async Task MonitorAsync(string readerName, CancellationToken token)
    {
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var monitor = MonitorFactory.Instance.Create(SCardScope.System);
        monitor.CardInserted += OnCardInserted;
        monitor.MonitorException += OnMonitorException;
        try
        {
            monitor.Start(readerName);
            status.ReportConnected();
            await failed.Task.WaitAsync(token).ConfigureAwait(false);
            status.ReportDisconnected();
        }
        catch (PCSCException ex)
        {
            status.ReportError(ex.Message);
        }
        finally
        {
            monitor.CardInserted -= OnCardInserted;
            monitor.MonitorException -= OnMonitorException;
            monitor.Cancel();
        }

        void OnMonitorException(object sender, PCSCException ex)
        {
            status.ReportError(ex.Message);
            failed.TrySetResult();
        }
    }

    private void OnCardInserted(object sender, CardStatusEventArgs e)
    {
        try
        {
            using var context = ContextFactory.Instance.Establish(SCardScope.System);
            using var reader = context.ConnectReader(e.ReaderName, SCardShareMode.Shared, SCardProtocol.Any);
            if (ReadCard(reader) is { } args)
            {
                status.ReportEvent();
                CardRead?.Invoke(this, args);
            }
        }
        catch (Exception ex) when (ex is PCSCException or ArgumentException)
        {
            status.ReportError(ex.Message);
        }
    }

    private static SuicaReadEventArgs? ReadCard(ICardReader reader)
    {
        var response = SendCommand(reader, CreateCommand(0xFF, 0xCA, 0x00, 0x00, 0x00));
        if (!response.IsSuccess)
        {
            return null;
        }

        var idm = Convert.ToHexString(response.Data);

        response = SendCommand(reader, CreateCommand(0xFF, 0xA4, 0x00, 0x01, [0x8B, 0x00]));
        if (!response.IsSuccess)
        {
            return null;
        }

        response = SendCommand(reader, CreateCommand(0xFF, 0xB0, 0x00, 0x00, 0x00));
        if (!response.IsSuccess || (response.Data.Length < BlockSize))
        {
            return null;
        }

        var balance = SuicaLogic.ExtractAccessBalance(response.Data);

        response = SendCommand(reader, CreateCommand(0xFF, 0xA4, 0x00, 0x01, [0x0F, 0x09]));
        if (!response.IsSuccess)
        {
            return null;
        }

        var history = new List<SuicaHistoryRecord>();
        for (var i = 0; i < HistoryCount; i++)
        {
            response = SendCommand(reader, CreateCommand(0xFF, 0xB0, 0x00, (byte)i, 0x00));
            if (!response.IsSuccess || (response.Data.Length < BlockSize))
            {
                return null;
            }

            if (SuicaLogic.IsValidLog(response.Data))
            {
                history.Add(new SuicaHistoryRecord(
                    SuicaLogic.ExtractLogDateTime(response.Data),
                    SuicaLogic.ExtractLogTerminal(response.Data),
                    SuicaLogic.ExtractLogProcess(response.Data),
                    SuicaLogic.ExtractLogBalance(response.Data)));
            }
        }

        return new SuicaReadEventArgs(idm, balance, history);
    }

    private static Response SendCommand(ICardReader reader, byte[] command)
    {
        var buffer = new byte[258];
        var length = reader.Transmit(command, buffer);
        return new Response(buffer, length);
    }

    private static byte[] CreateCommand(byte cla, byte ins, byte p1, byte p2, byte[] data)
    {
        var command = new byte[5 + data.Length];
        command[0] = cla;
        command[1] = ins;
        command[2] = p1;
        command[3] = p2;
        command[4] = (byte)data.Length;
        data.CopyTo(command.AsSpan(5));
        return command;
    }

    private static byte[] CreateCommand(byte cla, byte ins, byte p1, byte p2, int le) =>
        [cla, ins, p1, p2, (byte)le];

    private readonly struct Response
    {
        private readonly byte[] buffer;

        private readonly int length;

        public ReadOnlySpan<byte> Data => buffer.AsSpan(0, Math.Max(length - 2, 0));

        public bool IsSuccess => (length >= 2) && (buffer[length - 2] == 0x90) && (buffer[length - 1] == 0x00);

        public Response(byte[] buffer, int length)
        {
            this.buffer = buffer;
            this.length = length;
        }
    }
}

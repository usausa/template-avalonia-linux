namespace Template.LinuxApp.Components.Nfc;

using System.Buffers.Binary;
using System.Threading.Channels;

using PCSC;
using PCSC.Exceptions;
using PCSC.Monitoring;

using Template.LinuxApp.Domain.Logic;
using Template.LinuxApp.State;

public sealed record SuicaHistoryRecord(DateTime DateTime, byte Terminal, byte Process, int Balance, int TransactionId);

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

    event EventHandler? ReadFailed;

    DeviceStatus Status { get; }

    string Device { get; }

    void Start();

    ValueTask StopAsync();
}

public sealed class SuicaReader : ISuicaReader, IDisposable
{
    private const int BlockSize = 16;

    private const int HistoryCount = 20;

    private const int HistoryChunk = 8;

    private const int ResponseSize = 512;

    private const uint ExchangeTimeoutMicroseconds = 100_000;

    private const ushort DefaultSystemCode = 0x0003;

    private const int PollAttempts = 3;

    private const int ResetAttempts = 5;

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan SwitchGuardTime = TimeSpan.FromMilliseconds(20);

    private static readonly TimeSpan ResetInterval = TimeSpan.FromMilliseconds(100);

    private static readonly TimeSpan RedetectInterval = TimeSpan.FromMilliseconds(1500);

    private static readonly byte[] StartSessionCommand = [0xFF, 0x50, 0x00, 0x00, 0x02, 0x81, 0x00, 0x00];

    private static readonly byte[] EndSessionCommand = [0xFF, 0x50, 0x00, 0x00, 0x02, 0x82, 0x00, 0x00];

    private static readonly byte[] SwitchFeliCaCommand = [0xFF, 0x50, 0x00, 0x02, 0x04, 0x8F, 0x02, 0x03, 0x00, 0x00];

    private readonly ILogger<SuicaReader> log;

    private readonly TimeProvider timeProvider;

    private readonly ushort[] systemCodes;

    private CancellationTokenSource? cts;

    private Task? loopTask;

    private volatile string? currentReader;

    private string lastStatus = string.Empty;

    private long lastReadTimestamp;

    public event EventHandler<SuicaReadEventArgs>? CardRead;

    public event EventHandler? ReadFailed;

    public DeviceStatus Status { get; }

    public string Device => currentReader ?? "PC/SC";

    public SuicaReader(ILogger<SuicaReader> log, TimeProvider timeProvider, SuicaReaderOption option, DeviceState deviceState)
    {
        this.log = log;
        this.timeProvider = timeProvider;
        systemCodes = ParseSystemCodes(option.SystemCodes);
        Status = deviceState.Register("NFC", true);
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
        Status.ReportStarted();
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
        Status.ReportStopped();
    }

    private static ushort[] ParseSystemCodes(IEnumerable<string> values)
    {
        var codes = new List<ushort>();
        foreach (var value in values)
        {
            if (UInt16.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
            {
                codes.Add(code);
            }
        }

        return codes.Count > 0 ? [.. codes] : [DefaultSystemCode];
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
                Status.ReportError(ex.Message);
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
            Status.ReportError(ex.Message);
            return null;
        }
    }

    private async Task MonitorAsync(string readerName, CancellationToken token)
    {
        await ResetSessionAsync(readerName, token).ConfigureAwait(false);

        var requests = Channel.CreateBounded<string>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = true });
        using var monitor = MonitorFactory.Instance.Create(SCardScope.System);
        monitor.CardInserted += OnCardInserted;
        monitor.MonitorException += OnMonitorException;
        try
        {
            monitor.Start(readerName);
            currentReader = readerName;
            Status.ReportConnected();
            await foreach (var atr in requests.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                await HandleCardAsync(readerName, atr, token).ConfigureAwait(false);
                while (requests.Reader.TryRead(out var ignored))
                {
                    log.DebugSuicaCardIgnored(ignored);
                }
            }

            currentReader = null;
            Status.ReportDisconnected();
        }
        catch (PCSCException ex)
        {
            Status.ReportError(ex.Message);
        }
        finally
        {
            currentReader = null;
            monitor.CardInserted -= OnCardInserted;
            monitor.MonitorException -= OnMonitorException;
            monitor.Cancel();
        }

        void OnCardInserted(object sender, CardStatusEventArgs e) => requests.Writer.TryWrite(Convert.ToHexString(e.Atr ?? []));

        void OnMonitorException(object sender, PCSCException ex)
        {
            Status.ReportError(ex.Message);
            requests.Writer.TryComplete();
        }
    }

    private async Task ResetSessionAsync(string readerName, CancellationToken token)
    {
        PCSCException? error = null;
        for (var attempt = 1; attempt <= ResetAttempts; attempt++)
        {
            try
            {
                using var context = ContextFactory.Instance.Establish(SCardScope.System);
                using var reader = context.ConnectReader(readerName, SCardShareMode.Direct, SCardProtocol.Unset);
                Transmit(reader, EndSessionCommand, new byte[ResponseSize]);
                log.DebugSuicaSessionReset(attempt);
                return;
            }
            catch (PCSCException ex)
            {
                error = ex;
            }

            await Task.Delay(ResetInterval, timeProvider, token).ConfigureAwait(false);
        }

        if (error is not null)
        {
            log.WarnSuicaSessionResetFailed(error.SCardError.ToString());
            Status.ReportError(error.Message);
        }
    }

    private async Task HandleCardAsync(string readerName, string atr, CancellationToken token)
    {
        if ((lastReadTimestamp != 0) && (timeProvider.GetElapsedTime(lastReadTimestamp) < RedetectInterval))
        {
            log.DebugSuicaCardIgnored(atr);
            return;
        }

        log.DebugSuicaCardInserted(atr);

        SuicaReadEventArgs? args = null;
        var closed = false;
        try
        {
            using var context = ContextFactory.Instance.Establish(SCardScope.System);
            using var reader = context.ConnectReader(readerName, SCardShareMode.Direct, SCardProtocol.Unset);
            (args, closed) = await ReadCardAsync(reader, atr, token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is PCSCException or ArgumentException)
        {
            Status.ReportError(ex.Message);
        }

        if (!closed)
        {
            await ResetSessionAsync(readerName, token).ConfigureAwait(false);
        }

        if (args is null)
        {
            ReadFailed?.Invoke(this, EventArgs.Empty);
            return;
        }

        lastReadTimestamp = timeProvider.GetTimestamp();
        log.DebugSuicaCardRead(args.Idm, args.Balance, args.History.Count);
        Status.ReportEvent();
        CardRead?.Invoke(this, args);
    }

    private async Task<(SuicaReadEventArgs? Args, bool Closed)> ReadCardAsync(ICardReader reader, string atr, CancellationToken token)
    {
        if (Send(reader, StartSessionCommand) is null)
        {
            return (Fail("start session", atr), false);
        }

        SuicaReadEventArgs? args;
        bool closed;
        try
        {
            if (Send(reader, SwitchFeliCaCommand) is null)
            {
                args = Fail("switch felica", atr);
            }
            else
            {
                await Task.Delay(SwitchGuardTime, timeProvider, token).ConfigureAwait(false);
                args = ReadSuica(reader, atr);
            }
        }
        finally
        {
            closed = Send(reader, EndSessionCommand) is not null;
        }

        return (args, closed);
    }

    private SuicaReadEventArgs? ReadSuica(ICardReader reader, string atr)
    {
        var idm = Poll(reader);
        if (idm is null)
        {
            return Fail("polling", atr);
        }

        var access = ReadBlocks(reader, idm, 0x008B, 0, 1);
        if (access is null)
        {
            return Fail("read 008B", atr);
        }

        var history = new List<SuicaHistoryRecord>();
        for (var start = 0; start < HistoryCount; start += HistoryChunk)
        {
            var count = Math.Min(HistoryChunk, HistoryCount - start);
            var blocks = ReadBlocks(reader, idm, 0x090F, start, count);
            if (blocks is null)
            {
                return Fail(String.Create(CultureInfo.InvariantCulture, $"read 090F {start}"), atr);
            }

            for (var i = 0; i < count; i++)
            {
                var block = blocks.AsSpan(i * BlockSize, BlockSize);
                if (SuicaLogic.IsValidLog(block))
                {
                    history.Add(new SuicaHistoryRecord(
                        SuicaLogic.ExtractLogDateTime(block),
                        SuicaLogic.ExtractLogTerminal(block),
                        SuicaLogic.ExtractLogProcess(block),
                        SuicaLogic.ExtractLogBalance(block),
                        SuicaLogic.ExtractLogTransactionId(block)));
                }
            }
        }

        return new SuicaReadEventArgs(Convert.ToHexString(idm), SuicaLogic.ExtractAccessBalance(access), history);
    }

    private SuicaReadEventArgs? Fail(string step, string atr)
    {
        log.WarnSuicaReadFailed(step, lastStatus, atr);
        return null;
    }

    private byte[]? Poll(ICardReader reader)
    {
        for (var attempt = 0; attempt < PollAttempts; attempt++)
        {
            foreach (var systemCode in systemCodes)
            {
                var response = Exchange(reader, [0x06, 0x00, (byte)(systemCode >> 8), (byte)systemCode, 0x01, 0x0F]);
                if ((response is not null) && (response.Length >= 18) && (response[1] == 0x01))
                {
                    return response[2..10];
                }
            }
        }

        return null;
    }

    private byte[]? ReadBlocks(ICardReader reader, byte[] idm, ushort service, int start, int count)
    {
        var command = new byte[14 + (count * 2)];
        command[0] = (byte)command.Length;
        command[1] = 0x06;
        idm.CopyTo(command, 2);
        command[10] = 0x01;
        command[11] = (byte)service;
        command[12] = (byte)(service >> 8);
        command[13] = (byte)count;
        for (var i = 0; i < count; i++)
        {
            command[14 + (i * 2)] = 0x80;
            command[15 + (i * 2)] = (byte)(start + i);
        }

        var response = Exchange(reader, command);
        var size = count * BlockSize;
        if ((response is null) || (response.Length < 13 + size) || (response[1] != 0x07) || (response[10] != 0x00) || (response[11] != 0x00))
        {
            return null;
        }

        return response[13..(13 + size)];
    }

    private byte[]? Exchange(ICardReader reader, ReadOnlySpan<byte> felica)
    {
        var length = 11 + felica.Length;
        var command = new byte[7 + length + 3];
        command[0] = 0xFF;
        command[1] = 0x50;
        command[3] = 0x01;
        command[5] = (byte)(length >> 8);
        command[6] = (byte)length;
        command[7] = 0x5F;
        command[8] = 0x46;
        command[9] = 0x04;
        BinaryPrimitives.WriteUInt32LittleEndian(command.AsSpan(10), ExchangeTimeoutMicroseconds);
        command[14] = 0x95;
        command[15] = 0x82;
        command[16] = (byte)(felica.Length >> 8);
        command[17] = (byte)felica.Length;
        felica.CopyTo(command.AsSpan(18));

        var response = Send(reader, command);
        if ((response is null) || !TryFindValue(response.AsSpan(0, response.Length - 2), 0x97, out var data))
        {
            return null;
        }

        return data.ToArray();
    }

    private byte[]? Send(ICardReader reader, byte[] command)
    {
        var buffer = new byte[ResponseSize];
        int length;
        try
        {
            length = Transmit(reader, command, buffer);
        }
        catch (PCSCException ex)
        {
            lastStatus = ex.SCardError.ToString();
            return null;
        }

        var response = buffer.AsSpan(0, length);
        if ((length < 2) || (response[^2] != 0x90) || (response[^1] != 0x00))
        {
            lastStatus = Convert.ToHexString(response);
            return null;
        }

        if (TryFindValue(response[..^2], 0xC0, out var status) && ((status.Length < 3) || (status[0] != 0x00) || (status[1] != 0x90) || (status[2] != 0x00)))
        {
            lastStatus = Convert.ToHexString(status);
            return null;
        }

        lastStatus = string.Empty;
        return response.ToArray();
    }

    private int Transmit(ICardReader reader, byte[] command, byte[] buffer)
    {
        try
        {
            return TransmitOnce(reader, command, buffer);
        }
        catch (PCSCException ex) when (IsCardChanged(ex))
        {
            reader.Reconnect(SCardShareMode.Direct, SCardProtocol.Unset, SCardReaderDisposition.Leave);
            log.DebugSuicaReconnected(ex.SCardError.ToString());
            return TransmitOnce(reader, command, buffer);
        }
    }

    private static int TransmitOnce(ICardReader reader, byte[] command, byte[] buffer)
    {
        try
        {
            return reader.Transmit(SCardPCI.T1, command, buffer);
        }
        catch (PCSCException ex) when (!IsCardChanged(ex))
        {
            return reader.Transmit(SCardPCI.Raw, command, buffer);
        }
    }

    private static bool IsCardChanged(PCSCException ex) =>
        ex.SCardError is SCardError.RemovedCard or SCardError.ResetCard;

    private static bool TryFindValue(ReadOnlySpan<byte> data, byte tag, out ReadOnlySpan<byte> value)
    {
        var offset = 0;
        while (offset < data.Length)
        {
            var current = data[offset++];
            if (current == 0x5F)
            {
                offset++;
            }

            if (offset >= data.Length)
            {
                break;
            }

            int length = data[offset++];
            if ((length == 0x81) && (offset < data.Length))
            {
                length = data[offset++];
            }
            else if ((length == 0x82) && (offset + 1 < data.Length))
            {
                length = (data[offset] << 8) | data[offset + 1];
                offset += 2;
            }

            if (offset + length > data.Length)
            {
                break;
            }

            if (current == tag)
            {
                value = data.Slice(offset, length);
                return true;
            }

            offset += length;
        }

        value = default;
        return false;
    }
}

namespace Template.LinuxApp.Components.Motor;

using System.Buffers.Text;
using System.IO.Ports;

using Template.LinuxApp.State;

public enum ServoChannel
{
    Servo1,
    Servo2
}

public interface IMotorController
{
    void Start();

    ValueTask StopAsync();

    void SetLed(byte r, byte g, byte b);

    void SetServo(ServoChannel channel, int angle);
}

public sealed class MotorController : IMotorController, IDisposable
{
    private const int NeutralAngle = 90;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);

    private readonly Lock sync = new();

    private readonly TimeProvider timeProvider;

    private readonly MotorControllerOption option;

    private readonly DeviceStatus status;

    private readonly int[] servoAngles = [NeutralAngle, NeutralAngle];

    private (byte R, byte G, byte B) led;

    private CancellationTokenSource? cts;

    private Task? loopTask;

    private SerialPort? port;

    public MotorController(TimeProvider timeProvider, MotorControllerOption option, DeviceState deviceState)
    {
        this.timeProvider = timeProvider;
        this.option = option;
        status = deviceState.Register("Motor", !String.IsNullOrEmpty(option.Port));
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

    public void SetLed(byte r, byte g, byte b)
    {
        lock (sync)
        {
            led = (r, g, b);
            WriteLed();
        }
    }

    public void SetServo(ServoChannel channel, int angle)
    {
        lock (sync)
        {
            servoAngles[(int)channel] = angle;
            WriteServo(channel);
        }
    }

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
            lock (sync)
            {
                led = default;
                servoAngles.AsSpan().Fill(NeutralAngle);
                WriteAll();
            }

            Close();
        }
    }

    private void Open()
    {
        var serial = new SerialPort(option.Port)
        {
            BaudRate = 115200,
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

        lock (sync)
        {
            port = serial;
            WriteAll();
        }

        status.ReportConnected();
    }

    private void Close()
    {
        lock (sync)
        {
            port?.Dispose();
            port = null;
        }
    }

    private void WriteAll()
    {
        WriteLed();
        WriteServo(ServoChannel.Servo1);
        WriteServo(ServoChannel.Servo2);
    }

    private void WriteLed()
    {
        Span<byte> buffer = stackalloc byte[32];
        "LED "u8.CopyTo(buffer);
        var pos = 4;
        pos += Format(led.R, buffer[pos..]);
        buffer[pos++] = (byte)' ';
        pos += Format(led.G, buffer[pos..]);
        buffer[pos++] = (byte)' ';
        pos += Format(led.B, buffer[pos..]);
        buffer[pos++] = (byte)'\n';
        Write(buffer[..pos]);
    }

    private void WriteServo(ServoChannel channel)
    {
        Span<byte> buffer = stackalloc byte[32];
        "SERVO"u8.CopyTo(buffer);
        var pos = 5;
        buffer[pos++] = (byte)('1' + (int)channel);
        buffer[pos++] = (byte)' ';
        pos += Format(servoAngles[(int)channel], buffer[pos..]);
        buffer[pos++] = (byte)'\n';
        Write(buffer[..pos]);
    }

    private static int Format(int value, Span<byte> destination)
    {
        Utf8Formatter.TryFormat(value, destination, out var written);
        return written;
    }

    private void Write(ReadOnlySpan<byte> command)
    {
        if (port is null)
        {
            return;
        }

        try
        {
            port.BaseStream.Write(command);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException)
        {
            status.ReportError(ex.Message);
        }
    }
}

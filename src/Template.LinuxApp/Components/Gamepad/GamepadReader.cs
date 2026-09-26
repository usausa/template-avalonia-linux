namespace Template.LinuxApp.Components.Gamepad;

using LinuxDotNet.GameInput;

using Template.LinuxApp.State;

public sealed class GamepadButtonEventArgs : EventArgs
{
    public byte Button { get; }

    public bool Pressed { get; }

    public GamepadButtonEventArgs(byte button, bool pressed)
    {
        Button = button;
        Pressed = pressed;
    }
}

public sealed class GamepadConnectionEventArgs : EventArgs
{
    public bool Connected { get; }

    public GamepadConnectionEventArgs(bool connected)
    {
        Connected = connected;
    }
}

public interface IGamepadReader
{
    event EventHandler<GamepadButtonEventArgs>? ButtonChanged;

    event EventHandler<GamepadConnectionEventArgs>? ConnectionChanged;

    bool IsConnected { get; }

    bool GetButtonPressed(byte button);

    short GetAxisValue(byte axis);
}

public sealed class GamepadReader : IGamepadReader, IDisposable
{
    public event EventHandler<GamepadButtonEventArgs>? ButtonChanged;

    public event EventHandler<GamepadConnectionEventArgs>? ConnectionChanged;

    private readonly DeviceStatus status;

    private readonly GameController? controller;

    public bool IsConnected => controller?.IsConnected ?? false;

    public GamepadReader(GamepadReaderOption option, DeviceState deviceState)
    {
        status = deviceState.Register("Gamepad", !String.IsNullOrEmpty(option.Device));
        if (!status.IsEnabled)
        {
            return;
        }

        controller = new GameController(option.Device);
        controller.ButtonChanged += OnButtonChanged;
        controller.AxisChanged += OnAxisChanged;
        controller.ConnectionChanged += OnConnectionChanged;
        controller.Start();
        status.ReportStarted();
    }

    public void Dispose()
    {
        if (controller is null)
        {
            return;
        }

        controller.ButtonChanged -= OnButtonChanged;
        controller.AxisChanged -= OnAxisChanged;
        controller.ConnectionChanged -= OnConnectionChanged;
        controller.Dispose();
    }

    public bool GetButtonPressed(byte button) => controller?.GetButtonPressed(button) ?? false;

    public short GetAxisValue(byte axis) => controller?.GetAxisValue(axis) ?? 0;

    private void OnButtonChanged(byte button, bool pressed)
    {
        status.ReportEvent();
        ButtonChanged?.Invoke(this, new GamepadButtonEventArgs(button, pressed));
    }

    private void OnAxisChanged(byte axis, short value)
    {
        status.ReportEvent();
    }

    private void OnConnectionChanged(bool connected)
    {
        if (connected)
        {
            status.ReportConnected();
        }
        else
        {
            status.ReportDisconnected();
        }

        ConnectionChanged?.Invoke(this, new GamepadConnectionEventArgs(connected));
    }
}

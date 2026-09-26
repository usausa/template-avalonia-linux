namespace Template.LinuxApp.Devices.Input;

using Template.LinuxApp.Components.Gamepad;

public sealed class PadInputDevice : IInputDevice, IDisposable
{
    public event EventHandler<EventArgs<InputSignal>>? Handle;

    private readonly Dictionary<byte, InputGestureDetector> detectors = [];

    private readonly IGamepadReader gamepadReader;

    public PadInputDevice(TimeProvider timeProvider, InputOption option, IGamepadReader gamepadReader)
    {
        foreach (var button in option.Pad)
        {
            var key = button.Key;
            detectors.Add(button.Button, new InputGestureDetector(timeProvider, button, x => Raise(key, x)));
        }

        this.gamepadReader = gamepadReader;
        gamepadReader.ButtonChanged += OnButtonChanged;
        gamepadReader.ConnectionChanged += OnConnectionChanged;
    }

    public void Dispose()
    {
        gamepadReader.ButtonChanged -= OnButtonChanged;
        gamepadReader.ConnectionChanged -= OnConnectionChanged;

        foreach (var detector in detectors.Values)
        {
            detector.Dispose();
        }
    }

    private void OnButtonChanged(object? sender, GamepadButtonEventArgs e)
    {
        if (!detectors.TryGetValue(e.Button, out var detector))
        {
            return;
        }

        if (e.Pressed)
        {
            detector.Down();
        }
        else
        {
            detector.Up();
        }
    }

    private void OnConnectionChanged(object? sender, GamepadConnectionEventArgs e)
    {
        if (e.Connected)
        {
            return;
        }

        foreach (var detector in detectors.Values)
        {
            detector.Reset();
        }
    }

    private void Raise(InputKey key, InputAction action)
    {
        Handle?.Invoke(this, new EventArgs<InputSignal>(new InputSignal(key, action)));
    }
}

namespace Template.LinuxApp.Devices.Input;

public interface IInputDevice
{
    event EventHandler<EventArgs<InputSignal>> Handle;
}

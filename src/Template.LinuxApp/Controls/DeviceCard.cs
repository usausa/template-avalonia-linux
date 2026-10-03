namespace Template.LinuxApp.Controls;

using Avalonia;
using Avalonia.Controls;

using Template.LinuxApp.State;

public sealed class DeviceCard : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<DeviceCard, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DeviceProperty = AvaloniaProperty.Register<DeviceCard, string?>(nameof(Device));

    public static readonly StyledProperty<DeviceStatus?> StatusProperty = AvaloniaProperty.Register<DeviceCard, DeviceStatus?>(nameof(Status));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Device
    {
        get => GetValue(DeviceProperty);
        set => SetValue(DeviceProperty, value);
    }

    public DeviceStatus? Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }
}

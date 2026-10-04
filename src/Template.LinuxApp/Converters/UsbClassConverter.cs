namespace Template.LinuxApp.Converters;

using Avalonia.Data.Converters;

using LinuxDotNet.SystemInfo;

public sealed class UsbClassConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            UsbClass usbClass => ToText(usbClass),
            IEnumerable<UsbClass> classes => String.Join(", ", classes.Select(ToText)),
            _ => String.Empty
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static string ToText(UsbClass value) =>
        value switch
        {
            UsbClass.PerInterface => "Device",
            UsbClass.Hid => "HID",
            UsbClass.CdcData => "CDC data",
            UsbClass.MassStorage => "Mass storage",
            UsbClass.SmartCard => "Smart card",
            UsbClass.ContentSecurity => "Content security",
            UsbClass.PersonalHealthcare => "Healthcare",
            UsbClass.AudioVideo => "Audio/Video",
            UsbClass.TypeCBridge => "Type-C bridge",
            UsbClass.WirelessController => "Wireless",
            UsbClass.ApplicationSpecific => "Application",
            UsbClass.VendorSpecific => "Vendor specific",
            _ => value.ToString()
        };
}

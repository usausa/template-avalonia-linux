namespace Template.LinuxApp.Converters;

using Avalonia.Data.Converters;

public sealed class UsbSpeedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            double speed when !Double.IsFinite(speed) => String.Empty,
            double speed when speed >= 1000 => String.Create(CultureInfo.InvariantCulture, $"{speed / 1000:0.#} Gb/s"),
            double speed => String.Create(CultureInfo.InvariantCulture, $"{speed:0.#} Mb/s"),
            _ => String.Empty
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

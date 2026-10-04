namespace Template.LinuxApp.Converters;

using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;

using Template.LinuxApp.Domain.Logic;

public sealed class SmartHealthBrushConverter : IValueConverter
{
    public Color Unknown { get; set; } = Colors.Gray;

    public Color Good { get; set; } = Colors.Gray;

    public Color Warning { get; set; } = Colors.Gray;

    public Color Critical { get; set; } = Colors.Gray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new ImmutableSolidColorBrush(value switch
        {
            SmartHealth.Good => Good,
            SmartHealth.Warning => Warning,
            SmartHealth.Critical => Critical,
            _ => Unknown
        });

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

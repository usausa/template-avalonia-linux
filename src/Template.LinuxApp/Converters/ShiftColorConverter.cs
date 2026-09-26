namespace Template.LinuxApp.Converters;

using Avalonia.Data.Converters;
using Avalonia.Media;

public sealed class ShiftColorConverter : IValueConverter
{
    public IBrush ActiveColor { get; set; } = Brushes.Turquoise;

    public IBrush InactiveColor { get; set; } = Brushes.Gray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value is int shift) && (parameter is int gear) && (shift == gear) ? ActiveColor : InactiveColor;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

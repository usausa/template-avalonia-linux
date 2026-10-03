namespace Template.LinuxApp.Converters;

using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;

using LinuxDotNet.Cups;

public sealed class PrinterStateBrushConverter : IValueConverter
{
    public Color Idle { get; set; } = Colors.Gray;

    public Color Processing { get; set; } = Colors.Gray;

    public Color Stopped { get; set; } = Colors.Gray;

    public Color Other { get; set; } = Colors.Gray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new ImmutableSolidColorBrush(value switch
        {
            PrinterState.Idle => Idle,
            PrinterState.Processing => Processing,
            PrinterState.Stopped => Stopped,
            _ => Other
        });

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

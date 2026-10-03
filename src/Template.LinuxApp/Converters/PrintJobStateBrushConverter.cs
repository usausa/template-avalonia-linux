namespace Template.LinuxApp.Converters;

using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;

using LinuxDotNet.Cups;

public sealed class PrintJobStateBrushConverter : IValueConverter
{
    public Color Processing { get; set; } = Colors.Gray;

    public Color Held { get; set; } = Colors.Gray;

    public Color Stopped { get; set; } = Colors.Gray;

    public Color Aborted { get; set; } = Colors.Gray;

    public Color Completed { get; set; } = Colors.Gray;

    public Color Other { get; set; } = Colors.Gray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new ImmutableSolidColorBrush(value switch
        {
            PrintJobState.Processing => Processing,
            PrintJobState.Held => Held,
            PrintJobState.Stopped => Stopped,
            PrintJobState.Aborted => Aborted,
            PrintJobState.Completed => Completed,
            _ => Other
        });

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

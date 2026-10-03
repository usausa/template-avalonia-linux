namespace Template.LinuxApp.Converters;

using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;

using Template.LinuxApp.Domain.Logic;

public sealed class SuicaProcessBrushConverter : IValueConverter
{
    private static readonly IBrush Teal = new ImmutableSolidColorBrush(Color.FromRgb(0x00, 0xAB, 0xA9));

    private static readonly IBrush OrangeDark = new ImmutableSolidColorBrush(Color.FromRgb(0xDA, 0x53, 0x2C));

    private static readonly IBrush OrangeDarkBrighter = new ImmutableSolidColorBrush(Color.FromRgb(0xF7, 0x70, 0x49));

    private static readonly IBrush BlueDarkBrighter = new ImmutableSolidColorBrush(Color.FromRgb(0x48, 0x74, 0xB4));

    private static readonly IBrush Blue = new ImmutableSolidColorBrush(Color.FromRgb(0x2D, 0x89, 0xEF));

    private static readonly IBrush LightGreen = new ImmutableSolidColorBrush(Color.FromRgb(0x99, 0xB4, 0x33));

    private static readonly IBrush Magenta = new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x97));

    private static readonly IBrush Yellow = new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0xC4, 0x0D));

    private static readonly IBrush Gray = new ImmutableSolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is byte process ? ToBrush(SuicaLogic.ConvertProcessType(process)) : Gray;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static IBrush ToBrush(byte processType) =>
        processType switch
        {
            1 or 3 or 4 or 5 or 6 => Teal,
            2 or 20 or 21 or 31 => OrangeDark,
            7 or 8 or 17 => BlueDarkBrighter,
            13 or 15 or 35 => Blue,
            19 => LightGreen,
            70 or 75 => Magenta,
            72 or 73 => OrangeDarkBrighter,
            74 => Yellow,
            _ => Gray
        };
}

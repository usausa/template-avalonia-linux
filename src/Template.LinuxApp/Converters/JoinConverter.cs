namespace Template.LinuxApp.Converters;

using Avalonia.Data.Converters;

public sealed class JoinConverter : IValueConverter
{
    public string Separator { get; set; } = "  ";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is IEnumerable<string> values ? String.Join(Separator, values) : String.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

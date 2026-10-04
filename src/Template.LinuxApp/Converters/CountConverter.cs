namespace Template.LinuxApp.Converters;

using Avalonia.Data.Converters;

public sealed class CountConverter : IValueConverter
{
    public string Singular { get; set; } = String.Empty;

    public string Plural { get; set; } = String.Empty;

    public string Zero { get; set; } = String.Empty;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            0 => Zero,
            1 => String.Create(CultureInfo.InvariantCulture, $"1 {Singular}"),
            int count => String.Create(CultureInfo.InvariantCulture, $"{count:N0} {Plural}"),
            _ => String.Empty
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

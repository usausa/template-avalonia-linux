namespace Template.LinuxApp.Converters;

using Avalonia.Data.Converters;

public sealed class ByteSizeConverter : IValueConverter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public double Divisor { get; set; } = 1024;

    public string Suffix { get; set; } = String.Empty;

    public string Empty { get; set; } = "—";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var size = value switch
        {
            ulong x => x,
            long x => x,
            uint x => x,
            int x => x,
            double x => x,
            float x => x,
            _ => Double.NaN
        };
        if (!Double.IsFinite(size))
        {
            return Empty;
        }

        var unit = 0;
        while ((size >= Divisor) && (unit < Units.Length - 1))
        {
            size /= Divisor;
            unit++;
        }

        return unit == 0
            ? String.Create(CultureInfo.InvariantCulture, $"{size:F0} {Units[unit]}{Suffix}")
            : String.Create(CultureInfo.InvariantCulture, $"{size:0.0} {Units[unit]}{Suffix}");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

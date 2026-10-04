namespace Template.LinuxApp.Converters;

using Avalonia.Data.Converters;

using Template.LinuxApp.Domain.Logic;

public sealed class SmartRawValueConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if ((values.Count < 2) || (values[0] is not byte id) || (values[1] is not ulong raw))
        {
            return String.Empty;
        }

        if (!SmartLogic.IsTemperature(id))
        {
            return raw.ToString("N0", CultureInfo.InvariantCulture);
        }

        var (current, minimum, maximum) = SmartLogic.GetTemperatureRange(raw);
        return (minimum > 0) && (minimum <= current) && (current <= maximum)
            ? String.Create(CultureInfo.InvariantCulture, $"{current} ({minimum}/{maximum})")
            : current.ToString(CultureInfo.InvariantCulture);
    }
}

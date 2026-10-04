namespace Template.LinuxApp.Converters;

using Avalonia.Data.Converters;

using Template.LinuxApp.Domain.Logic;

public sealed class SmartValueConverter : IMultiValueConverter
{
    private static readonly ByteSizeConverter SizeConverter = new() { Divisor = 1000 };

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if ((values.Count < 2) || (values[1] is not SmartValueUnit unit) || (unit == SmartValueUnit.None))
        {
            return String.Empty;
        }

        if ((values[0] is not double value) || !Double.IsFinite(value))
        {
            return "—";
        }

        return unit switch
        {
            SmartValueUnit.Count => value.ToString("N0", CultureInfo.InvariantCulture),
            SmartValueUnit.Percent => String.Create(CultureInfo.InvariantCulture, $"{value:F0}%"),
            SmartValueUnit.Celsius => String.Create(CultureInfo.InvariantCulture, $"{value:F0} °C"),
            SmartValueUnit.Minutes => String.Create(CultureInfo.InvariantCulture, $"{value:N0} min"),
            SmartValueUnit.Hours => String.Create(CultureInfo.InvariantCulture, $"{value:N0} h"),
            SmartValueUnit.Days => String.Create(CultureInfo.InvariantCulture, $"{value:N0} days"),
            SmartValueUnit.Bytes => SizeConverter.Convert(value, typeof(string), null, culture),
            SmartValueUnit.CriticalWarning => FormatCriticalWarning((byte)value),
            _ => String.Empty
        };
    }

    public static string FormatCriticalWarning(byte value)
    {
        if (value == 0)
        {
            return "None";
        }

        var names = SmartLogic.GetCriticalWarningNames(value);
        return names.Count > 0 ? String.Join(", ", names) : String.Create(CultureInfo.InvariantCulture, $"0x{value:X2}");
    }
}

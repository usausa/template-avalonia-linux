namespace Template.LinuxApp.Converters;

using Avalonia.Data.Converters;

public sealed class DurationConverter : IValueConverter
{
    public bool ShowMinutesWithDays { get; set; } = true;

    public string Empty { get; set; } = "—";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TimeSpan duration)
        {
            return Empty;
        }

        if (duration.TotalDays >= 1)
        {
            return ShowMinutesWithDays
                ? String.Create(CultureInfo.InvariantCulture, $"{(int)duration.TotalDays}d {duration.Hours:D2}h {duration.Minutes:D2}m")
                : String.Create(CultureInfo.InvariantCulture, $"{(int)duration.TotalDays}d {duration.Hours:D2}h");
        }

        return duration.TotalHours >= 1
            ? String.Create(CultureInfo.InvariantCulture, $"{duration.Hours}h {duration.Minutes:D2}m")
            : String.Create(CultureInfo.InvariantCulture, $"{duration.Minutes}m {duration.Seconds:D2}s");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

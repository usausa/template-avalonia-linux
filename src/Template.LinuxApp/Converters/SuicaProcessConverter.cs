namespace Template.LinuxApp.Converters;

using Avalonia.Data.Converters;

using Template.LinuxApp.Domain.Logic;

public sealed class SuicaProcessConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is byte process ? SuicaLogic.ConvertProcessString(process) : String.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

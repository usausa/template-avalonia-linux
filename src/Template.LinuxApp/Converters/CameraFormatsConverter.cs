namespace Template.LinuxApp.Converters;

using Avalonia.Data.Converters;

using Template.LinuxApp.Components.Video;

public sealed class CameraFormatsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is IEnumerable<CameraFormat> formats
            ? String.Join(", ", formats.Select(static x => String.Create(CultureInfo.InvariantCulture, $"{x.PixelFormat} {x.Sizes} sizes")))
            : String.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

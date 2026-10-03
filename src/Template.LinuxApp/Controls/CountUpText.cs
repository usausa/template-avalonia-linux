namespace Template.LinuxApp.Controls;

using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;

public sealed class CountUpText : TextBlock
{
    private static readonly TimeSpan CountDuration = TimeSpan.FromMilliseconds(600);

    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<CountUpText, double>(nameof(Value));

    public static readonly StyledProperty<double> DisplayValueProperty = AvaloniaProperty.Register<CountUpText, double>(nameof(DisplayValue));

    public static readonly StyledProperty<string> FormatProperty = AvaloniaProperty.Register<CountUpText, string>(nameof(Format), "{0:#,0}");

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double DisplayValue
    {
        get => GetValue(DisplayValueProperty);
        set => SetValue(DisplayValueProperty, value);
    }

    public string Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    public CountUpText()
    {
        Transitions =
        [
            new DoubleTransition { Property = DisplayValueProperty, Duration = CountDuration, Easing = new CubicEaseOut() }
        ];
        UpdateText();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty)
        {
            DisplayValue = Value;
        }
        else if ((change.Property == DisplayValueProperty) || (change.Property == FormatProperty))
        {
            UpdateText();
        }
    }

    private void UpdateText() => Text = String.Format(CultureInfo.InvariantCulture, Format, Math.Round(DisplayValue));
}

namespace Template.LinuxApp.Devices.Input;

public class InputButtonOption
{
    public InputKey Key { get; set; }

    public int LongPressMilliseconds { get; set; }

    public int RepeatDelayMilliseconds { get; set; }

    public int RepeatIntervalMilliseconds { get; set; }

    public int MinimumIntervalMilliseconds { get; set; }
}

public sealed class PadButtonOption : InputButtonOption
{
    public byte Button { get; set; }
}

public sealed class InputOption : IValidatableObject
{
    public Collection<PadButtonOption> Pad { get; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var group in Pad.GroupBy(static x => x.Button).Where(static x => x.Count() > 1))
        {
            yield return new ValidationResult($"Pad button is duplicated. button=[{group.Key}]", [nameof(Pad)]);
        }

        foreach (var button in Pad)
        {
            if (button.Key == InputKey.Unknown)
            {
                yield return new ValidationResult($"Input key is not specified. button=[{button.Button}]", [nameof(Pad)]);
            }

            if ((button.LongPressMilliseconds > 0) && (button.RepeatDelayMilliseconds > 0))
            {
                yield return new ValidationResult($"Long press and repeat cannot be combined. key=[{button.Key}]", [nameof(Pad)]);
            }

            if ((button.RepeatDelayMilliseconds > 0) && (button.RepeatIntervalMilliseconds <= 0))
            {
                yield return new ValidationResult($"Repeat interval is not specified. key=[{button.Key}]", [nameof(Pad)]);
            }
        }
    }
}

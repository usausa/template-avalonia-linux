namespace Template.LinuxApp.Settings;

public sealed class KioskSetting : IValidatableObject
{
    public bool Enable { get; set; }

    public bool HideCursor { get; set; }

    public string AdminPin { get; set; } = string.Empty;

    [Range(1, 60)]
    public int AdminHoldSeconds { get; set; } = 3;

    public Collection<byte> AdminPadButtons { get; } = [];

    public Collection<byte> AdminPadCode { get; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Enable && String.IsNullOrEmpty(AdminPin))
        {
            yield return new ValidationResult("Admin PIN is required in kiosk mode.", [nameof(AdminPin)]);
        }

        if ((AdminPadButtons.Count > 0) && (AdminPadCode.Count == 0))
        {
            yield return new ValidationResult("Admin pad code is required when admin pad buttons are set.", [nameof(AdminPadCode)]);
        }
    }
}

namespace Template.LinuxApp.Settings;

public sealed class Setting
{
    [Required]
    public string Value { get; set; } = default!;
}

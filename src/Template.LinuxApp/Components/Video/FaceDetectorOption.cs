namespace Template.LinuxApp.Components.Video;

public sealed class FaceDetectorOption : IValidatableObject
{
    public bool Enable { get; set; }

    public string Model { get; set; } = string.Empty;

    public bool Parallel { get; set; }

    [Range(0, 64)]
    public int IntraOpNumThreads { get; set; }

    [Range(0, 64)]
    public int InterOpNumThreads { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Enable && String.IsNullOrEmpty(Model))
        {
            yield return new ValidationResult("Face detection model is required.", [nameof(Model)]);
        }
    }
}

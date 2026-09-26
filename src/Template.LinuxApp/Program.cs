namespace Template.LinuxApp;

using System;

using Avalonia;
using Avalonia.Media;

using SkiaSharp;

public static class Program
{
    private static readonly string[] GenericFamilyNames = ["sans-serif", "serif", "monospace"];

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions { OverlayPopups = true })
            .With(new Win32PlatformOptions { OverlayPopups = true })
            .With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter", FontFamilyMappings = CreateGenericFamilyMappings() })
            .WithInterFont()
            .LogToTrace();

    private static Dictionary<string, FontFamily> CreateGenericFamilyMappings()
    {
        var mappings = new Dictionary<string, FontFamily>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in GenericFamilyNames)
        {
            using var typeface = SKFontManager.Default.MatchCharacter(name, 'A');
            if (typeface is not null)
            {
                mappings[name] = new FontFamily(typeface.FamilyName);
            }
        }

        return mappings;
    }
}

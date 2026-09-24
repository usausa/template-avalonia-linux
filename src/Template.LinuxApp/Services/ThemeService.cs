namespace Template.LinuxApp.Services;

using Avalonia.Styling;

using Template.LinuxApp.Settings;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class ThemeService
{
    private readonly UserSettingStore store;

    public string Current => store.Value.Theme;

    public ThemeService(UserSettingStore store)
    {
        this.store = store;
    }

    public void Apply() => ApplyVariant(store.Value.Theme);

    public async ValueTask ChangeAsync(string theme)
    {
        store.Value.Theme = theme;
        ApplyVariant(theme);
        await store.SaveAsync().ConfigureAwait(true);
    }

    private static void ApplyVariant(string theme)
    {
        var application = Avalonia.Application.Current;

        application?.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}

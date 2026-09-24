namespace Template.LinuxApp.Settings;

using System.Text.Json;

public sealed class UserSettingStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Template.LinuxApp", "usersetting.json");

    public UserSetting Value { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(path))
            {
                Value = JsonSerializer.Deserialize<UserSetting>(File.ReadAllText(path), SerializerOptions) ?? new UserSetting();
            }
        }
        catch (JsonException)
        {
            Value = new UserSetting();
        }
        catch (IOException)
        {
            Value = new UserSetting();
        }
    }

    public async ValueTask SaveAsync()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tempPath = path + ".tmp";
            await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(Value, SerializerOptions)).ConfigureAwait(false);
            File.Move(tempPath, path, true);
        }
        catch (IOException)
        {
        }
    }
}

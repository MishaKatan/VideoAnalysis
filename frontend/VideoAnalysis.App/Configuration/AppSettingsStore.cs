using System.Text.Json;

namespace VideoAnalysis.App.Configuration;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public AppSettingsStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        var json = File.ReadAllText(_settingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath) ?? AppContext.BaseDirectory);
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(_settingsPath, json);
    }
}

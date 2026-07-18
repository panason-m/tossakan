using System.Text.Json;

namespace Tossakan.Services;

public class AppSettings
{
    /// <summary>Same convention as Board.BackgroundImagePath: null = no photo,
    /// BackgroundImageService.HomeDefaultImageMarker = bundled photo, otherwise a stored custom photo path.
    /// Defaults to the bundled photo until the user explicitly picks something else.</summary>
    public string? HomeBackgroundImagePath { get; set; } = BackgroundImageService.HomeDefaultImageMarker;
}

/// <summary>
/// Persists app-wide (non-board) settings as a small JSON file under %LOCALAPPDATA%\Tossakan\, since
/// there's exactly one row of them and a full EF table (plus the EnsureCreated migration dance that
/// entails) would be overkill.
/// </summary>
public class AppSettingsService
{
    private static readonly string SettingsPath = Path.Combine(AppPaths.DataFolder, "settings.json");
    private AppSettings? _cached;

    public async Task<AppSettings> LoadAsync()
    {
        if (_cached is not null) return _cached;
        if (File.Exists(SettingsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(SettingsPath);
                _cached = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                _cached = new AppSettings();
            }
        }
        else
        {
            _cached = new AppSettings();
        }
        return _cached;
    }

    public async Task SaveAsync(AppSettings settings)
    {
        _cached = settings;
        AppPaths.EnsureFolders();
        await File.WriteAllTextAsync(SettingsPath, JsonSerializer.Serialize(settings));
    }
}

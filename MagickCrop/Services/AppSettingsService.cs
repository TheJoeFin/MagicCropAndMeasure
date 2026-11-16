using System.IO;
using System.Text.Json;

namespace MagickCrop.Services;

/// <summary>
/// Small JSON-backed store for canvas preferences that should survive a restart. Kept alongside
/// the recent-project data in <c>%LocalAppData%\MagickCrop</c>.
/// </summary>
public class AppSettingsService
{
    private const string SettingsFileName = "settings.json";
    private readonly string _settingsPath;

    /// <summary>Whether transform handles may be dragged past the edge of the image.</summary>
    public bool AllowHandlesOutsideImage { get; set; } = true;

    /// <summary>Whether the canvas mini map overlay is shown.</summary>
    public bool ShowMiniMap { get; set; } = true;

    public AppSettingsService()
    {
        string appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MagickCrop");

        Directory.CreateDirectory(appDataFolder);
        _settingsPath = Path.Combine(appDataFolder, SettingsFileName);

        Load();
    }

    private void Load()
    {
        if (!File.Exists(_settingsPath))
            return;

        try
        {
            SettingsDto? saved = JsonSerializer.Deserialize<SettingsDto>(File.ReadAllText(_settingsPath));
            if (saved is null)
                return;

            AllowHandlesOutsideImage = saved.AllowHandlesOutsideImage;
            ShowMiniMap = saved.ShowMiniMap;
        }
        catch (Exception)
        {
            // Corrupt or unreadable settings just fall back to the defaults.
        }
    }

    public void Save()
    {
        try
        {
            SettingsDto dto = new()
            {
                AllowHandlesOutsideImage = AllowHandlesOutsideImage,
                ShowMiniMap = ShowMiniMap,
            };

            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(dto));
        }
        catch (Exception)
        {
            // Preferences are not worth interrupting the user over.
        }
    }

    private sealed class SettingsDto
    {
        public bool AllowHandlesOutsideImage { get; set; } = true;
        public bool ShowMiniMap { get; set; } = true;
    }
}

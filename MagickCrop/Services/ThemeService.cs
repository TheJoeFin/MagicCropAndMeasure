using System.IO;
using System.Text.Json;
using System.Windows;
using MagickCrop.Services.Interfaces;
using Wpf.Ui.Appearance;

namespace MagickCrop.Services;

/// <summary>
/// Service for managing application theme (light/dark mode).
/// </summary>
public class ThemeService : IThemeService
{
    private readonly IAppPaths _appPaths;
    private bool _isDarkTheme;
    private const string ThemeSettingsFile = "theme-settings.json";

    public ThemeService(IAppPaths appPaths)
    {
        _appPaths = appPaths;
        _isDarkTheme = LoadThemePreference();
        ApplyCurrentTheme();
    }

    /// <summary>
    /// Gets the current theme.
    /// </summary>
    public bool IsDarkTheme => _isDarkTheme;

    /// <summary>
    /// Sets the application theme to dark mode.
    /// </summary>
    public void SetDarkTheme()
    {
        _isDarkTheme = true;
        ApplyCurrentTheme();
        SaveThemePreference();
    }

    /// <summary>
    /// Sets the application theme to light mode.
    /// </summary>
    public void SetLightTheme()
    {
        _isDarkTheme = false;
        ApplyCurrentTheme();
        SaveThemePreference();
    }

    private void ApplyCurrentTheme()
    {
        if (Application.Current?.Resources == null)
            return;

        try
        {
            var applicationTheme = _isDarkTheme ? ApplicationTheme.Dark : ApplicationTheme.Light;
            ApplicationThemeManager.Apply(applicationTheme);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error applying theme: {ex.Message}");
        }
    }

    private bool LoadThemePreference()
    {
        try
        {
            var settingsPath = GetThemeSettingsPath();
            
            if (!File.Exists(settingsPath))
                return true; // Default to dark theme
            
            var json = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize<ThemeSettings>(json);
            
            return settings?.IsDarkTheme ?? true;
        }
        catch
        {
            return true; // Default to dark theme on error
        }
    }

    private void SaveThemePreference()
    {
        try
        {
            var settingsPath = GetThemeSettingsPath();
            var directory = Path.GetDirectoryName(settingsPath);
            
            if (directory != null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            
            var settings = new ThemeSettings { IsDarkTheme = _isDarkTheme };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            
            File.WriteAllText(settingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving theme preference: {ex.Message}");
        }
    }

    private string GetThemeSettingsPath()
    {
        _appPaths.EnsureDirectoriesExist();
        return Path.Combine(_appPaths.AppDataRoot, ThemeSettingsFile);
    }

    private class ThemeSettings
    {
        public bool IsDarkTheme { get; set; }
    }
}

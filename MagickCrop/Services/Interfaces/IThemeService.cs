namespace MagickCrop.Services.Interfaces;

/// <summary>
/// Service for managing application theme.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Sets the application theme to dark mode.
    /// </summary>
    void SetDarkTheme();

    /// <summary>
    /// Sets the application theme to light mode.
    /// </summary>
    void SetLightTheme();

    /// <summary>
    /// Gets the current theme.
    /// </summary>
    bool IsDarkTheme { get; }
}

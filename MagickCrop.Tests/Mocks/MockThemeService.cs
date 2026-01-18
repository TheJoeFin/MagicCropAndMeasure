namespace MagickCrop.Tests.Mocks;

/// <summary>
/// Mock implementation of IThemeService for testing
/// </summary>
public class MockThemeService : IThemeService
{
    private bool _isDarkTheme = false;

    public bool IsDarkTheme => _isDarkTheme;

    public void SetDarkTheme()
    {
        _isDarkTheme = true;
    }

    public void SetLightTheme()
    {
        _isDarkTheme = false;
    }
}

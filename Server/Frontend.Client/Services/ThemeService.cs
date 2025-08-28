using System;

namespace Frontend.Client.Services;

public class ThemeService
{
    private bool _isDarkMode = false;
    
    public bool IsDarkMode => _isDarkMode;
    
    public event Action? OnThemeChanged;
    
    public void ToggleDarkMode()
    {
        _isDarkMode = !_isDarkMode;
        OnThemeChanged?.Invoke();
    }
    
    public void SetDarkMode(bool isDarkMode)
    {
        if (_isDarkMode != isDarkMode)
        {
            _isDarkMode = isDarkMode;
            OnThemeChanged?.Invoke();
        }
    }
}

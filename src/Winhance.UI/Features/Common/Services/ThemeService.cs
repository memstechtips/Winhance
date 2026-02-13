using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Windows.UI.ViewManagement;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// Service for managing application themes in WinUI 3.
/// </summary>
public class ThemeService : IThemeService
{
    private readonly IUserPreferencesService _userPreferences;
    private readonly UISettings _uiSettings;
    private WinhanceTheme _currentTheme = WinhanceTheme.System;

    /// <inheritdoc />
    public WinhanceTheme CurrentTheme => _currentTheme;

    /// <inheritdoc />
    public event EventHandler<WinhanceTheme>? ThemeChanged;

    public ThemeService(IUserPreferencesService userPreferences)
    {
        _userPreferences = userPreferences;
        _uiSettings = new UISettings();

        // Listen for Windows theme changes to update System theme followers
        _uiSettings.ColorValuesChanged += OnWindowsThemeChanged;
    }

    /// <inheritdoc />
    public void SetTheme(WinhanceTheme theme)
    {
        _currentTheme = theme;
        ApplyTheme(theme);

        // Save preference asynchronously (fire and forget since UI has already updated)
        _ = SaveThemePreferenceAsync(theme);

        ThemeChanged?.Invoke(this, theme);
    }

    /// <inheritdoc />
    public void LoadSavedTheme()
    {
        // Load theme preference synchronously to avoid async/await deadlock on UI thread
        _currentTheme = LoadThemePreferenceSync();
        ApplyTheme(_currentTheme);
    }

    private WinhanceTheme LoadThemePreferenceSync()
    {
        try
        {
            // Use synchronous method to get preference to avoid deadlock
            var themeString = _userPreferences.GetPreference<string>("Theme", string.Empty);

            if (string.IsNullOrEmpty(themeString))
            {
                return WinhanceTheme.System; // Default to following Windows
            }

            if (Enum.TryParse<WinhanceTheme>(themeString, out var theme))
            {
                return theme;
            }
        }
        catch
        {
            // Fall through to default
        }

        return WinhanceTheme.System;
    }

    /// <inheritdoc />
    public ElementTheme GetEffectiveTheme()
    {
        return _currentTheme switch
        {
            WinhanceTheme.System => IsWindowsDarkTheme() ? ElementTheme.Dark : ElementTheme.Light,
            WinhanceTheme.LightNative => ElementTheme.Light,
            WinhanceTheme.DarkNative => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private void ApplyTheme(WinhanceTheme theme)
    {
        if (App.MainWindow?.Content is not FrameworkElement rootElement)
            return;

        switch (theme)
        {
            case WinhanceTheme.System:
                rootElement.RequestedTheme = ElementTheme.Default;
                break;

            case WinhanceTheme.LightNative:
                rootElement.RequestedTheme = ElementTheme.Light;
                break;

            case WinhanceTheme.DarkNative:
                rootElement.RequestedTheme = ElementTheme.Dark;
                break;
        }
    }

    private async Task SaveThemePreferenceAsync(WinhanceTheme theme)
    {
        try
        {
            await _userPreferences.SetPreferenceAsync("Theme", theme.ToString());
        }
        catch
        {
            // Silently ignore save failures - theme is already applied in memory
        }
    }

    private async Task<WinhanceTheme> LoadThemePreferenceAsync()
    {
        try
        {
            var themeString = await _userPreferences.GetPreferenceAsync<string>("Theme", string.Empty);

            if (string.IsNullOrEmpty(themeString))
            {
                return WinhanceTheme.System; // Default to following Windows
            }

            if (Enum.TryParse<WinhanceTheme>(themeString, out var theme))
            {
                return theme;
            }
        }
        catch
        {
            // Fall through to default
        }

        return WinhanceTheme.System;
    }

    private bool IsWindowsDarkTheme()
    {
        // Check Windows apps use dark theme setting
        var foreground = _uiSettings.GetColorValue(UIColorType.Foreground);
        // If foreground is light, it's dark mode
        return foreground.R > 128 && foreground.G > 128 && foreground.B > 128;
    }

    private void OnWindowsThemeChanged(UISettings sender, object args)
    {
        // Only react if we're following system theme
        if (_currentTheme == WinhanceTheme.System)
        {
            // Must dispatch to UI thread
            App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                // Re-apply to trigger any listeners that depend on effective theme
                ThemeChanged?.Invoke(this, WinhanceTheme.System);
            });
        }
    }
}

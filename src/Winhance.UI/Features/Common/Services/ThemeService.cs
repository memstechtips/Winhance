using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// Service for managing application themes in WinUI 3.
/// Supports native WinUI 3 themes and legacy Winhance custom themes.
/// </summary>
public class ThemeService : IThemeService
{
    private readonly IUserPreferencesService _userPreferences;
    private readonly UISettings _uiSettings;
    private WinhanceTheme _currentTheme = WinhanceTheme.System;

    // Legacy Dark Theme Colors (Yellow Accent)
    private static readonly Color LegacyDarkAccent = Color.FromArgb(255, 255, 222, 0);      // #FFDE00
    private static readonly Color LegacyDarkBackground = Color.FromArgb(255, 32, 32, 32);    // #202020
    private static readonly Color LegacyDarkSurface = Color.FromArgb(255, 37, 38, 40);       // #252628
    private static readonly Color LegacyDarkContentSection = Color.FromArgb(255, 31, 32, 34); // #1F2022

    // Legacy Light Theme Colors (Blue Accent)
    private static readonly Color LegacyLightAccent = Color.FromArgb(255, 0, 120, 212);      // #0078D4
    private static readonly Color LegacyLightBackground = Color.FromArgb(255, 246, 248, 252); // #F6F8FC
    private static readonly Color LegacyLightSurface = Color.FromArgb(255, 255, 255, 255);   // #FFFFFF
    private static readonly Color LegacyLightContentSection = Color.FromArgb(255, 234, 236, 242); // #EAECF2

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
            WinhanceTheme.LegacyWhite => ElementTheme.Light,
            WinhanceTheme.LegacyDark => ElementTheme.Dark,
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
                ClearLegacyColors();
                break;

            case WinhanceTheme.LightNative:
                rootElement.RequestedTheme = ElementTheme.Light;
                ClearLegacyColors();
                break;

            case WinhanceTheme.DarkNative:
                rootElement.RequestedTheme = ElementTheme.Dark;
                ClearLegacyColors();
                break;

            case WinhanceTheme.LegacyWhite:
                rootElement.RequestedTheme = ElementTheme.Light;
                ApplyLegacyLightColors();
                break;

            case WinhanceTheme.LegacyDark:
                rootElement.RequestedTheme = ElementTheme.Dark;
                ApplyLegacyDarkColors();
                break;
        }
    }

    private void ApplyLegacyDarkColors()
    {
        var resources = Application.Current.Resources;

        // Override system accent color with Winhance yellow
        resources["SystemAccentColor"] = LegacyDarkAccent;
        resources["SystemAccentColorLight1"] = LegacyDarkAccent;
        resources["SystemAccentColorLight2"] = LegacyDarkAccent;
        resources["SystemAccentColorLight3"] = LegacyDarkAccent;
        resources["SystemAccentColorDark1"] = LegacyDarkAccent;
        resources["SystemAccentColorDark2"] = LegacyDarkAccent;
        resources["SystemAccentColorDark3"] = LegacyDarkAccent;

        // Background colors
        resources["ApplicationPageBackgroundThemeBrush"] = CreateBrush(LegacyDarkBackground);
        resources["LayerFillColorDefaultBrush"] = CreateBrush(LegacyDarkSurface);

        // Custom Winhance resources for views that need them
        resources["WinhanceAccentColor"] = LegacyDarkAccent;
        resources["WinhanceBackgroundColor"] = LegacyDarkBackground;
        resources["WinhanceSurfaceColor"] = LegacyDarkSurface;
        resources["WinhanceContentSectionColor"] = LegacyDarkContentSection;
    }

    private void ApplyLegacyLightColors()
    {
        var resources = Application.Current.Resources;

        // Override system accent color with Winhance blue
        resources["SystemAccentColor"] = LegacyLightAccent;
        resources["SystemAccentColorLight1"] = LegacyLightAccent;
        resources["SystemAccentColorLight2"] = LegacyLightAccent;
        resources["SystemAccentColorLight3"] = LegacyLightAccent;
        resources["SystemAccentColorDark1"] = LegacyLightAccent;
        resources["SystemAccentColorDark2"] = LegacyLightAccent;
        resources["SystemAccentColorDark3"] = LegacyLightAccent;

        // Background colors
        resources["ApplicationPageBackgroundThemeBrush"] = CreateBrush(LegacyLightBackground);
        resources["LayerFillColorDefaultBrush"] = CreateBrush(LegacyLightSurface);

        // Custom Winhance resources for views that need them
        resources["WinhanceAccentColor"] = LegacyLightAccent;
        resources["WinhanceBackgroundColor"] = LegacyLightBackground;
        resources["WinhanceSurfaceColor"] = LegacyLightSurface;
        resources["WinhanceContentSectionColor"] = LegacyLightContentSection;
    }

    private void ClearLegacyColors()
    {
        var resources = Application.Current.Resources;

        // Remove custom overrides to restore native theme behavior
        var keysToRemove = new[]
        {
            "WinhanceAccentColor",
            "WinhanceBackgroundColor",
            "WinhanceSurfaceColor",
            "WinhanceContentSectionColor"
        };

        foreach (var key in keysToRemove)
        {
            if (resources.ContainsKey(key))
            {
                resources.Remove(key);
            }
        }

        // Note: System accent colors will automatically restore to Windows settings
        // when not explicitly overridden in the resource chain
    }

    private static Microsoft.UI.Xaml.Media.SolidColorBrush CreateBrush(Color color)
    {
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Microsoft.UI.ColorHelper.FromArgb(color.A, color.R, color.G, color.B));
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

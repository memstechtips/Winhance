using Microsoft.UI.Xaml;

namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Defines the available theme options for Winhance.
/// </summary>
public enum WinhanceTheme
{
    /// <summary>Follow Windows system theme setting.</summary>
    System,
    /// <summary>Pure WinUI 3 light mode with Windows accent color.</summary>
    LightNative,
    /// <summary>Pure WinUI 3 dark mode with Windows accent color.</summary>
    DarkNative,
    /// <summary>Custom light theme with blue accent (original Winhance light theme).</summary>
    LegacyWhite,
    /// <summary>Custom dark theme with yellow accent (original Winhance dark theme).</summary>
    LegacyDark
}

/// <summary>
/// Service for managing application themes.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Gets the currently applied theme.
    /// </summary>
    WinhanceTheme CurrentTheme { get; }

    /// <summary>
    /// Raised when the theme changes.
    /// </summary>
    event EventHandler<WinhanceTheme>? ThemeChanged;

    /// <summary>
    /// Sets and applies the specified theme.
    /// </summary>
    /// <param name="theme">The theme to apply.</param>
    void SetTheme(WinhanceTheme theme);

    /// <summary>
    /// Loads the saved theme preference and applies it.
    /// </summary>
    void LoadSavedTheme();

    /// <summary>
    /// Gets the actual effective theme (Light or Dark) accounting for System theme following Windows.
    /// </summary>
    ElementTheme GetEffectiveTheme();
}

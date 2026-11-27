using Microsoft.UI.Xaml;

namespace Winhance.UI.Features.Common.Interfaces;

public enum WinhanceTheme
{
    System,
    LightNative,
    DarkNative
}

public interface IThemeService
{
    WinhanceTheme CurrentTheme { get; }

    event EventHandler<WinhanceTheme>? ThemeChanged;

    void SetTheme(WinhanceTheme theme);

    void LoadSavedTheme();

    ElementTheme GetEffectiveTheme();
}

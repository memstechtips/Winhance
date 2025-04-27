using System;

namespace Winhance.WPF.Features.Common.Resources.Theme
{
    public interface IThemeManager : IDisposable
    {
        bool IsDarkTheme { get; set; }
        void ToggleTheme();
        void ApplyTheme();
        void LoadThemePreference();
    }
}
namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IWindowsThemeQueryService
    {
        bool IsDarkModeEnabled();
        void SetDarkMode(bool enabled);
    }
}
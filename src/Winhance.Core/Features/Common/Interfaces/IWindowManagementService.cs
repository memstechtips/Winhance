using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IWindowManagementService
    {
        void MinimizeWindow();
        void MaximizeRestoreWindow();
        Task CloseWindowAsync();
        void HandleWindowStateChanged(WindowState windowState);
        string GetThemeIconPath();
        string GetDefaultIconPath();
        void RequestThemeIconUpdate();
        void ToggleTheme();
        bool IsDarkTheme { get; }
    }
}
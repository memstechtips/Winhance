using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Customize.Interfaces
{
    /// <summary>
    /// Service interface for managing Windows theme settings.
    /// Handles dark/light mode, transparency effects, and wallpaper changes.
    /// Extends IThemeStateQuery for ISP compliance.
    /// </summary>
    public interface IWindowsThemeService : IDomainService, IThemeStateQuery
    {
        /// <summary>
        /// Checks if dark mode is enabled.
        /// </summary>
        /// <returns>True if dark mode is enabled; otherwise, false.</returns>
        bool IsDarkModeEnabled();
    }
}

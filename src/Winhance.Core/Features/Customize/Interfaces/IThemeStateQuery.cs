using System.Threading.Tasks;

namespace Winhance.Core.Features.Customize.Interfaces
{
    /// <summary>
    /// Interface for querying Windows theme state information.
    /// Segregated interface following ISP for services that only need theme state queries.
    /// </summary>
    public interface IThemeStateQuery
    {
        /// <summary>
        /// Checks if dark mode is currently enabled in the system.
        /// Registry operations are inherently synchronous, so async version is unnecessary.
        /// </summary>
        /// <returns>True if dark mode is enabled; otherwise, false.</returns>
        bool IsDarkModeEnabled();
    }
}

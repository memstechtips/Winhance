using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service for detecting PowerShell configuration and providing cached results.
    /// </summary>
    public interface IPowerShellDetectionService
    {
        /// <summary>
        /// Gets the cached PowerShell information.
        /// </summary>
        /// <returns>Immutable PowerShell information.</returns>
        PowerShellInfo GetPowerShellInfo();

        /// <summary>
        /// Determines whether Windows PowerShell 5.1 should be used.
        /// </summary>
        /// <returns>True if Windows PowerShell should be used; otherwise, false.</returns>
        bool ShouldUseWindowsPowerShell();
    }
}

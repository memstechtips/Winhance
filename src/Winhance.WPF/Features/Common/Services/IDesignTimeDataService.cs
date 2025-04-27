using System.Collections.Generic;
using Winhance.WPF.Features.SoftwareApps.Models;

namespace Winhance.WPF.Features.Common.Services
{
    /// <summary>
    /// Interface for providing design-time data for the application.
    /// This allows for proper visualization in the designer without running the application.
    /// </summary>
    public interface IDesignTimeDataService
    {
        /// <summary>
        /// Gets a collection of sample third-party applications for design-time.
        /// </summary>
        /// <returns>A collection of ThirdPartyApp instances.</returns>
        IEnumerable<ThirdPartyApp> GetSampleThirdPartyApps();

        /// <summary>
        /// Gets a collection of sample Windows applications for design-time.
        /// </summary>
        /// <returns>A collection of WindowsApp instances.</returns>
        IEnumerable<WindowsApp> GetSampleWindowsApps();

        /// <summary>
        /// Gets a collection of sample Windows capabilities for design-time.
        /// </summary>
        /// <returns>A collection of WindowsApp instances configured as capabilities.</returns>
        IEnumerable<WindowsApp> GetSampleWindowsCapabilities();

        /// <summary>
        /// Gets a collection of sample Windows features for design-time.
        /// </summary>
        /// <returns>A collection of WindowsApp instances configured as features.</returns>
        IEnumerable<WindowsApp> GetSampleWindowsFeatures();
    }
}

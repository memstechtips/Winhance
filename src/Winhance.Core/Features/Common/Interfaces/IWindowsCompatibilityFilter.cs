using System.Collections.Generic;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Interface for filtering settings based on Windows version compatibility.
    /// Follows SRP by handling only Windows version filtering concerns.
    /// </summary>
    public interface IWindowsCompatibilityFilter
    {
        /// <summary>
        /// Filters settings based on Windows version and build number compatibility.
        /// Supports both SettingDefinition and SettingDefinition polymorphically.
        /// </summary>
        /// <param name="settings">The settings to filter.</param>
        /// <returns>Settings that are compatible with the current Windows version.</returns>
        IEnumerable<SettingDefinition> FilterSettingsByWindowsVersion(
            IEnumerable<SettingDefinition> settings
        );
    }
}

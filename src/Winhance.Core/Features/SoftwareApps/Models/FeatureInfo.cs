// This contains the model for Windows optional feature information

using System;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    /// <summary>
    /// Represents information about a Windows optional feature.
    /// </summary>
    public class FeatureInfo : IInstallableItem
    {
        public string PackageId => PackageName;
        public string DisplayName => Name;
        public InstallItemType ItemType => InstallItemType.Feature;
        public bool RequiresRestart => RequiresReboot;

        /// <summary>
        /// Gets or sets the name of the feature.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the feature.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the package name of the feature.
        /// This is the identifier used by Windows to reference the feature.
        /// </summary>
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the category of the feature.
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the feature is installed.
        /// </summary>
        public bool IsInstalled { get; set; }

        /// <summary>
        /// Gets or sets the registry settings associated with this feature.
        /// </summary>
        public AppRegistrySetting[]? RegistrySettings { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the feature is protected by the system.
        /// </summary>
        public bool IsSystemProtected { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the feature requires a reboot after installation or removal.
        /// </summary>
        public bool RequiresReboot { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the feature can be reenabled after disabling.
        /// </summary>
        public bool CanBeReenabled { get; set; } = true;
    }
}

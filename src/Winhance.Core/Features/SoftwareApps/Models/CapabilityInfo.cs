// This contains the model for Windows capability information

using System;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    /// <summary>
    /// Represents information about a Windows capability.
    /// </summary>
    public class CapabilityInfo : IInstallableItem
    {
        public string PackageId => PackageName;
        public string DisplayName => Name;
        public InstallItemType ItemType => InstallItemType.Capability;
        public bool RequiresRestart { get; set; }

        /// <summary>
        /// Gets or sets the name of the capability.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the capability.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the package name of the capability.
        /// This is the identifier used by Windows to reference the capability.
        /// </summary>
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the category of the capability.
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the capability is installed.
        /// </summary>
        public bool IsInstalled { get; set; }

        /// <summary>
        /// Gets or sets the registry settings associated with this capability.
        /// </summary>
        public AppRegistrySetting[]? RegistrySettings { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the capability is protected by the system.
        /// </summary>
        public bool IsSystemProtected { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the capability can be reenabled after disabling.
        /// </summary>
        public bool CanBeReenabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the capability is selected for installation or removal.
        /// </summary>
        public bool IsSelected { get; set; }
    }
}

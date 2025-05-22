// This contains the model for standard application information

using System;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    /// <summary>
    /// Defines the type of application.
    /// </summary>
    public enum AppType
    {
        /// <summary>
        /// Standard application.
        /// </summary>
        StandardApp,

        /// <summary>
        /// Windows capability.
        /// </summary>
        Capability,

        /// <summary>
        /// Windows optional feature.
        /// </summary>
        OptionalFeature,
    }

    /// <summary>
    /// Represents information about a standard application.
    /// </summary>
    public class AppInfo : IInstallableItem
    {
        string IInstallableItem.PackageId => PackageID;
        string IInstallableItem.DisplayName => Name;
        InstallItemType IInstallableItem.ItemType =>
            Type switch
            {
                AppType.Capability => InstallItemType.Capability,
                AppType.OptionalFeature => InstallItemType.Feature,
                _ => InstallItemType.WindowsApp,
            };
        bool IInstallableItem.RequiresRestart => false;

        /// <summary>
        /// Gets or sets the name of the application.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the application.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the package name of the application.
        /// </summary>
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the package ID of the application.
        /// </summary>
        public string PackageID { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the category of the application.
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the application is installed.
        /// </summary>
        public bool IsInstalled { get; set; }

        /// <summary>
        /// Gets or sets the type of application.
        /// </summary>
        public AppType Type { get; set; } = AppType.StandardApp;

        /// <summary>
        /// Gets or sets a value indicating whether the application requires a custom installation process.
        /// </summary>
        public bool IsCustomInstall { get; set; }

        /// <summary>
        /// Gets or sets the sub-packages associated with this application.
        /// </summary>
        public string[]? SubPackages { get; set; }

        /// <summary>
        /// Gets or sets the registry settings associated with this application.
        /// </summary>
        public AppRegistrySetting[]? RegistrySettings { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the application requires special handling.
        /// </summary>
        public bool RequiresSpecialHandling { get; set; }

        /// <summary>
        /// Gets or sets the special handler type for this application.
        /// </summary>
        public string? SpecialHandlerType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the application is protected by the system.
        /// </summary>
        public bool IsSystemProtected { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the application can be reinstalled after removal.
        /// </summary>
        public bool CanBeReinstalled { get; set; } = true;

        /// <summary>
        /// Gets or sets the version of the application.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the last operation error message, if any.
        /// </summary>
        public string? LastOperationError { get; set; }
    }

    /// <summary>
    /// Represents a registry setting for an application.
    /// </summary>
    public class AppRegistrySetting
    {
        /// <summary>
        /// Gets or sets the registry path.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the registry value name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the registry value.
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// Gets or sets the registry value kind.
        /// </summary>
        public Microsoft.Win32.RegistryValueKind ValueKind { get; set; }

        /// <summary>
        /// Gets or sets the description of the registry setting.
        /// </summary>
        public string? Description { get; set; }
    }

    /// <summary>
    /// Represents a package removal script.
    /// </summary>
    public class PackageRemovalScript
    {
        /// <summary>
        /// Gets or sets the name of the script.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the content of the script.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target scheduled task name.
        /// </summary>
        public string? TargetScheduledTaskName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the script should run on startup.
        /// </summary>
        public bool RunOnStartup { get; set; }
    }

    /// <summary>
    /// Defines the types of special app handlers.
    /// </summary>
    public enum AppHandlerType
    {
        /// <summary>
        /// No special handling required.
        /// </summary>
        None,

        /// <summary>
        /// Microsoft Edge browser.
        /// </summary>
        Edge,

        /// <summary>
        /// Microsoft OneDrive.
        /// </summary>
        OneDrive,

        /// <summary>
        /// Microsoft Copilot.
        /// </summary>
        Copilot,
    }
}

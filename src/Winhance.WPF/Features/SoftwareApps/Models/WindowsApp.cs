using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;
using System.Linq;

namespace Winhance.WPF.Features.SoftwareApps.Models
{
    /// <summary>
    /// Represents the type of Windows app.
    /// </summary>
    public enum WindowsAppType
    {
        /// <summary>
        /// Standard Windows application.
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
    /// Represents a Windows built-in application that can be installed or removed.
    /// This includes system components, default Windows apps, and capabilities.
    /// </summary>
    public partial class WindowsApp : ObservableObject, IWindowsApp, IInstallableItem, ISearchable
    {
        // IWindowsApp implementation
        public string Id => PackageID;
        public string PackageId => PackageID;
        public string DisplayName => Name;
        public InstallItemType ItemType => AppType switch
        {
            WindowsAppType.Capability => InstallItemType.Capability,
            WindowsAppType.OptionalFeature => InstallItemType.Feature,
            _ => InstallItemType.WindowsApp
        };
        public bool RequiresRestart { get; set; }

        // List of package names that cannot be reinstalled via winget/Microsoft Store
        private static readonly string[] NonReinstallablePackages = new string[]
        {
            "Microsoft.MicrosoftOfficeHub",
            "Microsoft.MSPaint",
            "Microsoft.Getstarted",
        };

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _packageName = string.Empty;

        [ObservableProperty]
        private string _packageID = string.Empty;

        [ObservableProperty]
        private string _category = string.Empty;

        [ObservableProperty]
        private bool _isInstalled;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isSpecialHandler;

        [ObservableProperty]
        private string _specialHandlerType = string.Empty;

        [ObservableProperty]
        private string[]? _subPackages;

        [ObservableProperty]
        private AppRegistrySetting[]? _registrySettings;

        [ObservableProperty]
        private bool _isSystemProtected;

        [ObservableProperty]
        private bool _isNotReinstallable;

        /// <summary>
        /// Gets or sets a value indicating whether the item can be reinstalled or reenabled after removal.
        /// </summary>
        [ObservableProperty]
        private bool _canBeReinstalled = true;

        /// <summary>
        /// Gets or sets the type of Windows app.
        /// </summary>
        [ObservableProperty]
        private WindowsAppType _appType = WindowsAppType.StandardApp;

        /// <summary>
        /// Determines if the app should show its description.
        /// </summary>
        public bool HasDescription => !string.IsNullOrEmpty(Description);

        /// <summary>
        /// Gets a value indicating whether this app is a Windows capability.
        /// </summary>
        public bool IsCapability => AppType == WindowsAppType.Capability;

        /// <summary>
        /// Gets a value indicating whether this app is a Windows optional feature.
        /// </summary>
        public bool IsOptionalFeature => AppType == WindowsAppType.OptionalFeature;

        /// <summary>
        /// Creates a WindowsApp instance from an AppInfo model.
        /// </summary>
        /// <param name="appInfo">The AppInfo model containing the app's data.</param>
        /// <returns>A new WindowsApp instance.</returns>
        public static WindowsApp FromAppInfo(AppInfo appInfo)
        {
            // Map AppType to WindowsAppType
            WindowsAppType appType;
            switch (appInfo.Type)
            {
                case Winhance.Core.Features.SoftwareApps.Models.AppType.Capability:
                    appType = WindowsAppType.Capability;
                    break;
                case Winhance.Core.Features.SoftwareApps.Models.AppType.OptionalFeature:
                    appType = WindowsAppType.OptionalFeature;
                    break;
                case Winhance.Core.Features.SoftwareApps.Models.AppType.StandardApp:
                default:
                    appType = WindowsAppType.StandardApp;
                    break;
            }

            var app = new WindowsApp
            {
                Name = appInfo.Name,
                Description = appInfo.Description,
                PackageName = appInfo.PackageName,
                PackageID = appInfo.PackageID,
                Category = appInfo.Category,
                IsInstalled = appInfo.IsInstalled,
                IsSpecialHandler = appInfo.RequiresSpecialHandling,
                SpecialHandlerType = appInfo.SpecialHandlerType ?? string.Empty,
                SubPackages = appInfo.SubPackages,
                RegistrySettings = appInfo.RegistrySettings,
                IsSystemProtected = appInfo.IsSystemProtected,
                IsSelected = false,
                CanBeReinstalled = appInfo.CanBeReinstalled,
                AppType = appType,
            };

            // Check if this app is in the list of non-reinstallable packages
            app.IsNotReinstallable = Array.Exists(
                NonReinstallablePackages,
                p => p.Equals(appInfo.PackageName, StringComparison.OrdinalIgnoreCase)
            );

            return app;
        }

        /// <summary>
        /// Creates a WindowsApp instance from a CapabilityInfo model.
        /// </summary>
        /// <param name="capabilityInfo">The CapabilityInfo model containing the capability's data.</param>
        /// <returns>A new WindowsApp instance.</returns>
        public static WindowsApp FromCapabilityInfo(CapabilityInfo capabilityInfo)
        {
            var app = new WindowsApp
            {
                Name = capabilityInfo.Name,
                Description = capabilityInfo.Description,
                PackageName = capabilityInfo.PackageName,
                PackageID = capabilityInfo.PackageName, // Ensure PackageID is set for capabilities
                Category = capabilityInfo.Category,
                IsInstalled = capabilityInfo.IsInstalled,
                RegistrySettings = capabilityInfo.RegistrySettings,
                IsSystemProtected = capabilityInfo.IsSystemProtected,
                CanBeReinstalled = capabilityInfo.CanBeReenabled,
                IsSelected = false,
                AppType = WindowsAppType.Capability,
            };

            return app;
        }

        /// <summary>
        /// Creates a WindowsApp instance from a FeatureInfo model.
        /// </summary>
        /// <param name="featureInfo">The FeatureInfo model containing the feature's data.</param>
        /// <returns>A new WindowsApp instance.</returns>
        public static WindowsApp FromFeatureInfo(FeatureInfo featureInfo)
        {
            var app = new WindowsApp
            {
                Name = featureInfo.Name,
                Description = featureInfo.Description,
                PackageName = featureInfo.PackageName,
                PackageID = featureInfo.PackageName, // Ensure PackageID is set for features
                Category = featureInfo.Category,
                IsInstalled = featureInfo.IsInstalled,
                RegistrySettings = featureInfo.RegistrySettings,
                IsSystemProtected = featureInfo.IsSystemProtected,
                CanBeReinstalled = featureInfo.CanBeReenabled,
                IsSelected = false,
                AppType = WindowsAppType.OptionalFeature,
            };

            return app;
        }

        /// <summary>
        /// Converts this WindowsApp to an AppInfo object.
        /// </summary>
        /// <returns>An AppInfo object with data from this WindowsApp.</returns>
        public AppInfo ToAppInfo()
        {
            // Map WindowsAppType to AppType
            Winhance.Core.Features.SoftwareApps.Models.AppType appType;
            switch (AppType)
            {
                case WindowsAppType.Capability:
                    appType = Winhance.Core.Features.SoftwareApps.Models.AppType.Capability;
                    break;
                case WindowsAppType.OptionalFeature:
                    appType = Winhance.Core.Features.SoftwareApps.Models.AppType.OptionalFeature;
                    break;
                case WindowsAppType.StandardApp:
                default:
                    appType = Winhance.Core.Features.SoftwareApps.Models.AppType.StandardApp;
                    break;
            }

            return new AppInfo
            {
                Name = Name,
                Description = Description,
                PackageName = PackageName,
                PackageID = PackageID,
                Category = Category,
                IsInstalled = IsInstalled,
                RequiresSpecialHandling = IsSpecialHandler,
                SpecialHandlerType = SpecialHandlerType,
                SubPackages = SubPackages,
                RegistrySettings = RegistrySettings,
                IsSystemProtected = IsSystemProtected,
                CanBeReinstalled = CanBeReinstalled,
                Type = appType,
                Version = string.Empty // Default to empty string for version
            };
        }
        
        /// <summary>
        /// Converts this WindowsApp to a CapabilityInfo object.
        /// </summary>
        /// <returns>A CapabilityInfo object with data from this WindowsApp.</returns>
        public CapabilityInfo ToCapabilityInfo()
        {
            if (AppType != WindowsAppType.Capability)
            {
                throw new InvalidOperationException("Cannot convert non-capability WindowsApp to CapabilityInfo");
            }
            
            return new CapabilityInfo
            {
                Name = Name,
                Description = Description,
                PackageName = PackageName,
                Category = Category,
                IsInstalled = IsInstalled,
                RegistrySettings = RegistrySettings,
                IsSystemProtected = IsSystemProtected,
                CanBeReenabled = CanBeReinstalled
            };
        }
        
        /// <summary>
        /// Converts this WindowsApp to a FeatureInfo object.
        /// </summary>
        /// <returns>A FeatureInfo object with data from this WindowsApp.</returns>
        public FeatureInfo ToFeatureInfo()
        {
            if (AppType != WindowsAppType.OptionalFeature)
            {
                throw new InvalidOperationException("Cannot convert non-feature WindowsApp to FeatureInfo");
            }
            
            return new FeatureInfo
            {
                Name = Name,
                Description = Description,
                PackageName = PackageName,
                Category = Category,
                IsInstalled = IsInstalled,
                RegistrySettings = RegistrySettings,
                IsSystemProtected = IsSystemProtected,
                CanBeReenabled = CanBeReinstalled
            };
        }

        /// <summary>
        /// Determines if the app matches the given search term.
        /// </summary>
        /// <param name="searchTerm">The search term to match against.</param>
        /// <returns>True if the app matches the search term, false otherwise.</returns>
        public bool MatchesSearch(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return true;
            }

            searchTerm = searchTerm.ToLowerInvariant();
            
            // Check if the search term matches any of the searchable properties
            return Name.ToLowerInvariant().Contains(searchTerm) ||
                   (!string.IsNullOrEmpty(Description) && Description.ToLowerInvariant().Contains(searchTerm)) ||
                   (!string.IsNullOrEmpty(PackageName) && PackageName.ToLowerInvariant().Contains(searchTerm)) ||
                   (!string.IsNullOrEmpty(Category) && Category.ToLowerInvariant().Contains(searchTerm));
        }

        /// <summary>
        /// Gets the searchable properties of the app.
        /// </summary>
        /// <returns>An array of property names that should be searched.</returns>
        public string[] GetSearchableProperties()
        {
            return new[] { nameof(Name), nameof(Description), nameof(PackageName), nameof(Category) };
        }
    }
}

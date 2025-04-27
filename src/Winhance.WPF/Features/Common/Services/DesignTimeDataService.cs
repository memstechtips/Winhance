using System.Collections.Generic;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.WPF.Features.SoftwareApps.Models;

namespace Winhance.WPF.Features.Common.Services
{
    /// <summary>
    /// Implementation of the IDesignTimeDataService interface that provides
    /// realistic sample data for design-time visualization.
    /// </summary>
    public class DesignTimeDataService : IDesignTimeDataService
    {
        /// <summary>
        /// Gets a collection of sample third-party applications for design-time.
        /// </summary>
        /// <returns>A collection of ThirdPartyApp instances.</returns>
        public IEnumerable<ThirdPartyApp> GetSampleThirdPartyApps()
        {
            return new List<ThirdPartyApp>
            {
                new ThirdPartyApp
                {
                    Name = "Visual Studio Code",
                    Description = "Lightweight code editor with powerful features",
                    PackageName = "Microsoft.VisualStudioCode",
                    Category = "Development",
                    IsInstalled = true,
                    IsCustomInstall = false
                },
                new ThirdPartyApp
                {
                    Name = "Mozilla Firefox",
                    Description = "Fast, private and secure web browser",
                    PackageName = "Mozilla.Firefox",
                    Category = "Web Browsers",
                    IsInstalled = false,
                    IsCustomInstall = false
                },
                new ThirdPartyApp
                {
                    Name = "7-Zip",
                    Description = "File archiver with high compression ratio",
                    PackageName = "7zip.7zip",
                    Category = "Utilities",
                    IsInstalled = true,
                    IsCustomInstall = false
                },
                new ThirdPartyApp
                {
                    Name = "Adobe Photoshop",
                    Description = "Professional image editing software",
                    PackageName = "Adobe.Photoshop",
                    Category = "Graphics & Design",
                    IsInstalled = false,
                    IsCustomInstall = true
                }
            };
        }

        /// <summary>
        /// Gets a collection of sample Windows applications for design-time.
        /// </summary>
        /// <returns>A collection of WindowsApp instances.</returns>
        public IEnumerable<WindowsApp> GetSampleWindowsApps()
        {
            // Create sample AppInfo objects to use with the factory method
            var appInfos = new List<AppInfo>
            {
                new AppInfo
                {
                    Name = "Microsoft Store",
                    Description = "Microsoft's digital distribution platform",
                    PackageName = "Microsoft.WindowsStore",
                    PackageID = "9WZDNCRFJBMP",
                    Category = "System",
                    IsInstalled = true,
                    CanBeReinstalled = true,
                    IsSystemProtected = false
                },
                new AppInfo
                {
                    Name = "Xbox",
                    Description = "Xbox console companion app for Windows",
                    PackageName = "Microsoft.XboxApp",
                    PackageID = "9MV0B5HZVK9Z",
                    Category = "Entertainment",
                    IsInstalled = true,
                    CanBeReinstalled = true,
                    IsSystemProtected = false
                },
                new AppInfo
                {
                    Name = "Microsoft Edge",
                    Description = "Microsoft's web browser",
                    PackageName = "Microsoft.MicrosoftEdge",
                    PackageID = "9NBLGGH4M8RR",
                    Category = "Web Browsers",
                    IsInstalled = true,
                    RequiresSpecialHandling = true,
                    SpecialHandlerType = "Edge",
                    CanBeReinstalled = false,
                    IsSystemProtected = true
                },
                new AppInfo
                {
                    Name = "OneDrive",
                    Description = "Microsoft's cloud storage service",
                    PackageName = "Microsoft.OneDrive",
                    PackageID = "9WZDNCRFJ3PL",
                    Category = "Cloud Storage",
                    IsInstalled = false,
                    RequiresSpecialHandling = true,
                    SpecialHandlerType = "OneDrive",
                    CanBeReinstalled = true,
                    IsSystemProtected = false
                }
            };

            // Use the factory method to create properly configured WindowsApp instances
            var windowsApps = new List<WindowsApp>();
            foreach (var appInfo in appInfos)
            {
                windowsApps.Add(WindowsApp.FromAppInfo(appInfo));
            }

            return windowsApps;
        }

        /// <summary>
        /// Gets a collection of sample Windows capabilities for design-time.
        /// </summary>
        /// <returns>A collection of WindowsApp instances configured as capabilities.</returns>
        public IEnumerable<WindowsApp> GetSampleWindowsCapabilities()
        {
            // Create sample CapabilityInfo objects to use with the factory method
            var capabilityInfos = new List<CapabilityInfo>
            {
                new CapabilityInfo
                {
                    Name = "Windows Media Player",
                    Description = "Classic media player for Windows",
                    PackageName = "Media.WindowsMediaPlayer",
                    Category = "Media",
                    IsInstalled = true,
                    CanBeReenabled = true,
                    IsSystemProtected = false
                },
                new CapabilityInfo
                {
                    Name = "Internet Explorer",
                    Description = "Legacy web browser",
                    PackageName = "Browser.InternetExplorer",
                    Category = "Web Browsers",
                    IsInstalled = false,
                    CanBeReenabled = true,
                    IsSystemProtected = false
                },
                new CapabilityInfo
                {
                    Name = "Windows Hello Face",
                    Description = "Facial recognition authentication",
                    PackageName = "Hello.Face",
                    Category = "Security",
                    IsInstalled = true,
                    CanBeReenabled = true,
                    IsSystemProtected = true
                }
            };

            // Use the factory method to create properly configured WindowsApp instances
            var capabilities = new List<WindowsApp>();
            foreach (var capabilityInfo in capabilityInfos)
            {
                capabilities.Add(WindowsApp.FromCapabilityInfo(capabilityInfo));
            }

            return capabilities;
        }

        /// <summary>
        /// Gets a collection of sample Windows features for design-time.
        /// </summary>
        /// <returns>A collection of WindowsApp instances configured as features.</returns>
        public IEnumerable<WindowsApp> GetSampleWindowsFeatures()
        {
            // Create sample FeatureInfo objects to use with the factory method
            var featureInfos = new List<FeatureInfo>
            {
                new FeatureInfo
                {
                    Name = "Windows Subsystem for Linux",
                    Description = "Run Linux command-line tools on Windows",
                    PackageName = "Microsoft-Windows-Subsystem-Linux",
                    Category = "Development",
                    IsInstalled = true,
                    CanBeReenabled = true,
                    IsSystemProtected = false
                },
                new FeatureInfo
                {
                    Name = "Hyper-V",
                    Description = "Windows virtualization platform",
                    PackageName = "Microsoft-Hyper-V-All",
                    Category = "Virtualization",
                    IsInstalled = false,
                    CanBeReenabled = true,
                    IsSystemProtected = false
                },
                new FeatureInfo
                {
                    Name = "Windows Sandbox",
                    Description = "Isolated desktop environment for running applications",
                    PackageName = "Containers-DisposableClientVM",
                    Category = "Security",
                    IsInstalled = false,
                    CanBeReenabled = true,
                    IsSystemProtected = false
                }
            };

            // Use the factory method to create properly configured WindowsApp instances
            var features = new List<WindowsApp>();
            foreach (var featureInfo in featureInfos)
            {
                features.Add(WindowsApp.FromFeatureInfo(featureInfo));
            }

            return features;
        }
    }
}

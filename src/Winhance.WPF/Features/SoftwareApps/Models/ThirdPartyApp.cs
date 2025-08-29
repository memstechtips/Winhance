using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.WPF.Features.SoftwareApps.Models
{
    /// <summary>
    /// Represents a third-party application that can be installed through package managers like winget.
    /// These are applications not built into Windows but available for installation.
    /// </summary>
    public partial class ThirdPartyApp : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _packageName = string.Empty;

        [ObservableProperty]
        private string _category = string.Empty;

        [ObservableProperty]
        private bool _isInstalled;

        [ObservableProperty]
        private bool _isCustomInstall;

        /// <summary>
        /// Determines if the app has a description that should be displayed.
        /// </summary>
        public bool HasDescription => !string.IsNullOrEmpty(Description);

        public static ThirdPartyApp FromAppInfo(AppInfo appInfo)
        {
            return new ThirdPartyApp
            {
                Name = appInfo.Name,
                Description = appInfo.Description,
                PackageName = appInfo.PackageName,
                Category = appInfo.Category,
                IsInstalled = appInfo.IsInstalled,
                IsCustomInstall = appInfo.IsCustomInstall
            };
        }

        /// <summary>
        /// Converts this ThirdPartyApp to an AppInfo object.
        /// </summary>
        /// <returns>An AppInfo object representing this ThirdPartyApp.</returns>
        public AppInfo ToAppInfo()
        {
            return new AppInfo
            {
                Name = Name,
                Description = Description,
                PackageName = PackageName,
                Category = Category,
                IsInstalled = IsInstalled,
                IsCustomInstall = IsCustomInstall
            };
        }
    }
}

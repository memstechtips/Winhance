using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.WPF.Features.SoftwareApps.Models
{
    public partial class ExternalApp : ObservableObject, ISearchable
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string PackageName { get; set; }
        public string Version { get; set; } = string.Empty;
        
        [ObservableProperty]
        private bool _isInstalled;
        
        public bool CanBeReinstalled { get; set; }
        
        [ObservableProperty]
        private bool _isSelected;
        
        [ObservableProperty]
        private string _lastOperationError = string.Empty;

        public static ExternalApp FromAppInfo(AppInfo appInfo)
        {
            if (appInfo == null)
                throw new ArgumentNullException(nameof(appInfo));

            var app = new ExternalApp
            {
                Name = appInfo.Name,
                Description = appInfo.Description,
                PackageName = appInfo.PackageName,
                Version = appInfo.Version,
                CanBeReinstalled = appInfo.CanBeReinstalled,
                Category = appInfo.Category,
                LastOperationError = appInfo.LastOperationError
            };
            
            app.IsInstalled = appInfo.IsInstalled;
            
            return app;
        }

        public AppInfo ToAppInfo()
        {
            return new AppInfo
            {
                Name = Name,
                Description = Description,
                PackageName = PackageName,
                Version = Version,
                IsInstalled = IsInstalled,
                CanBeReinstalled = CanBeReinstalled,
                Category = Category,
                LastOperationError = LastOperationError
            };
        }

        /// <summary>
        /// Gets the category of the app.
        /// </summary>
        public string Category { get; set; } = string.Empty;

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

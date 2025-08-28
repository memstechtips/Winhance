using System.Collections.Generic;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.WPF.Features.SoftwareApps.Models
{
    /// <summary>
    /// Adapter class that wraps an ExternalApp and implements IExternalApp.
    /// </summary>
    public class ExternalAppSettingItem : IExternalApp
    {
        private readonly ExternalApp _externalApp;
        
        public ExternalAppSettingItem(ExternalApp externalApp)
        {
            _externalApp = externalApp;
        }
        
        // IExternalApp implementation
        public string Id => _externalApp.PackageName;
        public string Name => _externalApp.Name;
        public string Description => _externalApp.Description;
        public string PackageName => _externalApp.PackageName;
        public string Version => _externalApp.Version;
        public bool IsInstalled => _externalApp.IsInstalled;
        public bool CanBeReinstalled => _externalApp.CanBeReinstalled;
        public string Category => _externalApp.Category;
        
        public bool IsSelected 
        { 
            get => _externalApp.IsSelected;
            set => _externalApp.IsSelected = value;
        }
        
        // Method to convert back to ExternalApp
        public ExternalApp ToExternalApp()
        {
            // Update the original ExternalApp with any changes
            _externalApp.IsSelected = IsSelected;
            return _externalApp;
        }
        
        // Static method to convert a collection of ExternalApps to ExternalAppSettingItems
        public static IEnumerable<ExternalAppSettingItem> FromExternalApps(IEnumerable<ExternalApp> externalApps)
        {
            foreach (var app in externalApps)
            {
                yield return new ExternalAppSettingItem(app);
            }
        }
    }
}
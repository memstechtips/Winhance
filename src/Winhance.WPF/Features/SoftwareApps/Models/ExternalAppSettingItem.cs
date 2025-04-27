using System.Collections.Generic;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.SoftwareApps.Models
{
    /// <summary>
    /// Adapter class that wraps an ExternalApp and implements ISettingItem.
    /// </summary>
    public class ExternalAppSettingItem : ISettingItem
    {
        private readonly ExternalApp _externalApp;
        
        public ExternalAppSettingItem(ExternalApp externalApp)
        {
            _externalApp = externalApp;
            
            // Initialize properties required by ISettingItem
            Id = externalApp.PackageName;
            Name = externalApp.Name;
            Description = externalApp.Description;
            IsSelected = externalApp.IsSelected;
            GroupName = externalApp.Category;
            IsVisible = true;
            ControlType = ControlType.BinaryToggle;
            Dependencies = new List<SettingDependency>();
            
            // Create a command that does nothing (placeholder)
            ApplySettingCommand = new RelayCommand(() => { });
        }
        
        // ISettingItem implementation
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsSelected { get; set; }
        public string GroupName { get; set; }
        public bool IsVisible { get; set; }
        public ControlType ControlType { get; set; }
        public List<SettingDependency> Dependencies { get; set; }
        public bool IsUpdatingFromCode { get; set; }
        public ICommand ApplySettingCommand { get; }
        
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
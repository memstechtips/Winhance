using System.Collections.Generic;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.SoftwareApps.Models
{
    /// <summary>
    /// Adapter class that wraps a WindowsApp and implements ISettingItem.
    /// </summary>
    public class WindowsAppSettingItem : ISettingItem
    {
        private readonly WindowsApp _windowsApp;

        public WindowsAppSettingItem(WindowsApp windowsApp)
        {
            _windowsApp = windowsApp;

            // Initialize properties required by ISettingItem
            Id = windowsApp.PackageId;
            Name = windowsApp.DisplayName;
            Description = windowsApp.Description;
            IsSelected = windowsApp.IsSelected;
            GroupName = windowsApp.Category;
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
        public object? SelectedValue { get; set; }
        public List<SettingDependency> Dependencies { get; set; }

        public ICommand ApplySettingCommand { get; }

        // Method to convert back to WindowsApp
        public WindowsApp ToWindowsApp()
        {
            // Update the original WindowsApp with any changes
            _windowsApp.IsSelected = IsSelected;
            return _windowsApp;
        }

        // Static method to convert a collection of WindowsApps to WindowsAppSettingItems
        public static IEnumerable<WindowsAppSettingItem> FromWindowsApps(
            IEnumerable<WindowsApp> windowsApps
        )
        {
            foreach (var app in windowsApps)
            {
                yield return new WindowsAppSettingItem(app);
            }
        }
    }
}

using System.Collections.Generic;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.WPF.Features.SoftwareApps.Models
{
    /// <summary>
    /// Adapter class that wraps a WindowsApp and implements IWindowsApp.
    /// </summary>
    public class WindowsAppSettingItem : IWindowsApp
    {
        private readonly WindowsApp _windowsApp;

        public WindowsAppSettingItem(WindowsApp windowsApp)
        {
            _windowsApp = windowsApp;
        }

        // IWindowsApp implementation
        public string Id => _windowsApp.PackageId;
        public string Name => _windowsApp.Name;
        public string Description => _windowsApp.Description;
        public string PackageId => _windowsApp.PackageId;
        public bool IsInstalled => _windowsApp.IsInstalled;
        public bool RequiresRestart => _windowsApp.RequiresRestart;
        public bool CanBeReinstalled => _windowsApp.CanBeReinstalled;
        public string Category => _windowsApp.Category;

        public bool IsSelected
        {
            get => _windowsApp.IsSelected;
            set => _windowsApp.IsSelected = value;
        }

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

using System;
using System.Windows;
using System.Windows.Controls;
using Winhance.WPF.Features.SoftwareApps.ViewModels;

namespace Winhance.WPF.Features.SoftwareApps.Views
{
    /// <summary>
    /// DataTemplateSelector that selects the appropriate template based on view mode (List or Table)
    /// </summary>
    public class AppViewModeTemplateSelector : DataTemplateSelector
    {
        /// <summary>
        /// Template to use for List View mode
        /// </summary>
        public DataTemplate ListViewTemplate { get; set; }

        /// <summary>
        /// Template to use for Table View mode
        /// </summary>
        public DataTemplate TableViewTemplate { get; set; }

        /// <summary>
        /// Selects the appropriate template based on the IsTableViewMode property of the ViewModel
        /// </summary>
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            // Check if the item is one of our ViewModels with the IsTableViewMode property
            bool isTableViewMode = false;

            // Check for specific view model types
            if (item is WindowsAppsViewModel windowsAppsViewModel)
            {
                isTableViewMode = windowsAppsViewModel.IsTableViewMode;
            }
            else if (item is ExternalAppsViewModel externalAppsViewModel)
            {
                isTableViewMode = externalAppsViewModel.IsTableViewMode;
            }
            else if (item is SoftwareAppsViewModel softwareAppsViewModel)
            {
                isTableViewMode = softwareAppsViewModel.IsTableViewMode;
            }
            else
            {
                // Try to get the IsTableViewMode property using reflection for other view models
                try
                {
                    var property = item?.GetType().GetProperty("IsTableViewMode");
                    if (property != null)
                    {
                        isTableViewMode = (bool)property.GetValue(item);
                    }
                }
                catch (Exception)
                {
                    // If we can't get the property, default to list view
                    return ListViewTemplate;
                }
            }

            return isTableViewMode ? TableViewTemplate : ListViewTemplate;
        }
    }
}

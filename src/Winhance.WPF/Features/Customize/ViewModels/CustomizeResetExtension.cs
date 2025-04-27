using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Winhance.WPF.Features.Customize.Models;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// Extension methods for the CustomizeViewModel class
    /// </summary>
    public static class CustomizeResetExtension
    {
        /// <summary>
        /// Resets all checkboxes to their initial state
        /// </summary>
        public static void ResetAllCheckboxes(this CustomizeViewModel viewModel)
        {
            // Mark that we're updating checkboxes to prevent cascading events
            var field = typeof(CustomizeViewModel).GetField("_updatingCheckboxes", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                field.SetValue(viewModel, true);
            }
            
            try
            {
                // Reset the "Select All" checkbox
                viewModel.IsSelectAllSelected = false;
                
                // Reset all customization items
                foreach (var item in viewModel.CustomizationItems)
                {
                    // Reset the category selection
                    item.IsSelected = false;
                }
                
                // Use reflection to access the Settings property of each ViewModel and reset IsSelected
                ResetSettingsInViewModel(viewModel.TaskbarSettings);
                ResetSettingsInViewModel(viewModel.StartMenuSettings);
                ResetSettingsInViewModel(viewModel.ExplorerSettings);
            }
            finally
            {
                // Reset the updating flag
                if (field != null)
                {
                    field.SetValue(viewModel, false);
                }
            }
        }
        
        private static void ResetSettingsInViewModel(object viewModel)
        {
            if (viewModel == null) return;
            
            // Get the Settings property
            var settingsProp = viewModel.GetType().GetProperty("Settings");
            if (settingsProp == null) return;
            
            // Get the Settings collection
            var settings = settingsProp.GetValue(viewModel) as System.Collections.IEnumerable;
            if (settings == null) return;
            
            // Get the IsSelected property info for CustomizationSettingItem
            var isSelectedProp = typeof(CustomizationSettingItem).GetProperty("IsSelected");
            if (isSelectedProp == null) return;
            
            // Reset IsSelected for each setting
            foreach (var setting in settings)
            {
                isSelectedProp.SetValue(setting, false);
            }
        }
    }
}

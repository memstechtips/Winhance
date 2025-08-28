using System;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Represents an item in a configuration file.
    /// </summary>
    public class ConfigurationItem
    {
        /// <summary>
        /// Gets or sets the name of the item.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the package name of the item.
        /// </summary>
        public string PackageName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the item is selected.
        /// </summary>
        public bool IsSelected { get; set; }
        
        /// <summary>
        /// Gets or sets the type of input used for this item.
        /// </summary>
        public SettingInputType InputType { get; set; } = SettingInputType.Toggle;
        
        /// <summary>
        /// Gets or sets the selected value for Selection controls.
        /// This is used when InputType is Selection.
        /// </summary>
        public string SelectedValue { get; set; }
        
        /// <summary>
        /// Gets or sets additional properties for the item.
        /// This can be used to store custom properties like wallpaper change preference.
        /// </summary>
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();
        
        /// <summary>
        /// Ensures that SelectedValue is set for Selection controls based on SliderValue and available options.
        /// </summary>
        public void EnsureSelectedValueIsSet()
        {
            // Only process Selection controls with null SelectedValue
            if (InputType == SettingInputType.Selection && string.IsNullOrEmpty(SelectedValue))
            {
                // For Power Plan
                if (Name?.Contains("Power Plan") == true ||
                    (CustomProperties.TryGetValue("Id", out var id) && id?.ToString() == "PowerPlanComboBox"))
                {
                    // Try to get the value from PowerPlanOptions if available
                    if (CustomProperties.TryGetValue("PowerPlanOptions", out var options) &&
                        options is List<string> powerPlanOptions &&
                        powerPlanOptions.Count > 0 &&
                        CustomProperties.TryGetValue("SliderValue", out var sliderValue))
                    {
                        int index = Convert.ToInt32(sliderValue);
                        if (index >= 0 && index < powerPlanOptions.Count)
                        {
                            SelectedValue = powerPlanOptions[index];
                        }
                    }
                    // If PowerPlanOptions is not available, use default values
                    else if (CustomProperties.TryGetValue("SliderValue", out var sv))
                    {
                        int index = Convert.ToInt32(sv);
                        string[] defaultOptions = { "Balanced", "High Performance", "Ultimate Performance" };
                        if (index >= 0 && index < defaultOptions.Length)
                        {
                            SelectedValue = defaultOptions[index];
                        }
                    }
                    
                    // If we still don't have a SelectedValue, add PowerPlanOptions
                    if (string.IsNullOrEmpty(SelectedValue) && CustomProperties.TryGetValue("SliderValue", out var sv2))
                    {
                        int index = Convert.ToInt32(sv2);
                        string[] defaultOptions = { "Balanced", "High Performance", "Ultimate Performance" };
                        if (index >= 0 && index < defaultOptions.Length)
                        {
                            SelectedValue = defaultOptions[index];
                            CustomProperties["PowerPlanOptions"] = new List<string>(defaultOptions);
                        }
                    }
                }
            }
        }
    }
}

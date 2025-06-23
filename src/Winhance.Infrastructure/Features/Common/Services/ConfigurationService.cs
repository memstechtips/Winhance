using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Service for saving and loading application configuration files.
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private const string FileExtension = ".winhance";
        private const string FileFilter = "Winhance Configuration Files|*" + FileExtension;
        private const string UnifiedFileFilter = "Winhance Unified Configuration Files|*" + FileExtension;
        private readonly ILogService _logService;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationService"/> class.
        /// </summary>
        /// <param name="logService">The log service.</param>
        public ConfigurationService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Saves a configuration file containing the selected items.
        /// </summary>
        /// <typeparam name="T">The type of items to save.</typeparam>
        /// <param name="items">The collection of items to save.</param>
        /// <param name="configType">The type of configuration being saved.</param>
        /// <returns>A task representing the asynchronous operation. Returns true if successful, false otherwise.</returns>
        public async Task<bool> SaveConfigurationAsync<T>(IEnumerable<T> items, string configType)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Starting to save {configType} configuration");
                // Create a save file dialog
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = FileFilter,
                    DefaultExt = FileExtension,
                    Title = "Save Configuration",
                    FileName = $"{configType}_{DateTime.Now:yyyyMMdd}{FileExtension}"
                };

                // Show the save file dialog
                if (saveFileDialog.ShowDialog() != true)
                {
                    _logService.Log(LogLevel.Info, "Save configuration canceled by user");
                    return false;
                }
                
                _logService.Log(LogLevel.Info, $"Saving configuration to {saveFileDialog.FileName}");

                // Create a configuration file
                var configFile = new ConfigurationFile
                {
                    ConfigType = configType,
                    CreatedAt = DateTime.UtcNow,
                    Items = new List<ConfigurationItem>()
                };

                // Add selected items to the configuration file
                foreach (var item in items)
                {
                    var properties = item.GetType().GetProperties();
                    var nameProperty = properties.FirstOrDefault(p => p.Name == "Name");
                    var packageNameProperty = properties.FirstOrDefault(p => p.Name == "PackageName");
                    var isSelectedProperty = properties.FirstOrDefault(p => p.Name == "IsSelected");
                    var controlTypeProperty = properties.FirstOrDefault(p => p.Name == "ControlType");
                    var registrySettingProperty = properties.FirstOrDefault(p => p.Name == "RegistrySetting");
                    var idProperty = properties.FirstOrDefault(p => p.Name == "Id");

                    if (nameProperty != null && isSelectedProperty != null)
                    {
                        var name = nameProperty.GetValue(item)?.ToString();
                        var packageName = packageNameProperty?.GetValue(item)?.ToString();
                        var isSelected = (bool)(isSelectedProperty.GetValue(item) ?? false);
                        
                        // Create the configuration item
                        var configItem = new ConfigurationItem
                        {
                            Name = name,
                            PackageName = packageName,
                            IsSelected = isSelected
                        };
                        
                        // Add control type if available
                        if (controlTypeProperty != null)
                        {
                            var controlType = controlTypeProperty.GetValue(item);
                            if (controlType != null)
                            {
                                configItem.ControlType = (ControlType)controlType;
                            }
                        }
                        
                        // Store the Id in CustomProperties if available
                        if (idProperty != null)
                        {
                            var id = idProperty.GetValue(item);
                            if (id != null)
                            {
                                configItem.CustomProperties["Id"] = id.ToString();
                                _logService.Log(LogLevel.Info, $"Stored Id for {configItem.Name}: {id}");
                            }
                        }
                        
                        // Handle ComboBox and ThreeStateSlider control types
                        if (configItem.ControlType == ControlType.ComboBox || configItem.ControlType == ControlType.ThreeStateSlider)
                        {
                            // For ComboBox, get the selected value
                            var selectedThemeProperty = properties.FirstOrDefault(p => p.Name == "SelectedTheme");
                            if (selectedThemeProperty != null)
                            {
                                configItem.SelectedValue = selectedThemeProperty.GetValue(item)?.ToString();
                                _logService.Log(LogLevel.Info, $"Setting SelectedValue for {configItem.Name} to '{configItem.SelectedValue}'");
                            }
                            else
                            {
                                // Try to get the value from SliderLabels and SliderValue
                                var sliderLabelsProperty = properties.FirstOrDefault(p => p.Name == "SliderLabels");
                                var sliderValueProperty = properties.FirstOrDefault(p => p.Name == "SliderValue");
                                
                                if (sliderLabelsProperty != null && sliderValueProperty != null)
                                {
                                    var sliderLabels = sliderLabelsProperty.GetValue(item) as System.Collections.IList;
                                    var sliderValue = sliderValueProperty.GetValue(item);
                                    
                                    if (sliderLabels != null && sliderValue != null && Convert.ToInt32(sliderValue) < sliderLabels.Count)
                                    {
                                        configItem.SelectedValue = sliderLabels[Convert.ToInt32(sliderValue)]?.ToString();
                                        _logService.Log(LogLevel.Info, $"Derived SelectedValue for {configItem.Name} from SliderLabels[{sliderValue}]: '{configItem.SelectedValue}'");
                                    }
                                }
                            }
                            
                            // Store the SliderValue for ComboBox or ThreeStateSlider
                            // Note: In this application, ComboBox controls use SliderValue to store the selected index
                            if (configItem.ControlType == ControlType.ComboBox || configItem.ControlType == ControlType.ThreeStateSlider)
                            {
                                var sliderValueProperty = properties.FirstOrDefault(p => p.Name == "SliderValue");
                                if (sliderValueProperty != null)
                                {
                                    var sliderValue = sliderValueProperty.GetValue(item);
                                    if (sliderValue != null)
                                    {
                                        configItem.CustomProperties["SliderValue"] = sliderValue;
                                        _logService.Log(LogLevel.Info, $"Stored SliderValue for {configItem.ControlType} {configItem.Name}: {sliderValue}");
                                    }
                                }
                                
                                // Also store SliderLabels if available
                                var sliderLabelsProperty = properties.FirstOrDefault(p => p.Name == "SliderLabels");
                                if (sliderLabelsProperty != null)
                                {
                                    var sliderLabels = sliderLabelsProperty.GetValue(item) as System.Collections.IList;
                                    if (sliderLabels != null && sliderLabels.Count > 0)
                                    {
                                        // Store the labels as a comma-separated string
                                        var labelsString = string.Join(",", sliderLabels.Cast<object>().Select(l => l.ToString()));
                                        configItem.CustomProperties["SliderLabels"] = labelsString;
                                        _logService.Log(LogLevel.Info, $"Stored SliderLabels for {configItem.ControlType} {configItem.Name}: {labelsString}");
                                        
                                        // For Power Plan, also store the labels as PowerPlanOptions
                                        if (configItem.Name.Contains("Power Plan") ||
                                            (configItem.CustomProperties.ContainsKey("Id") &&
                                             configItem.CustomProperties["Id"]?.ToString() == "PowerPlanComboBox"))
                                        {
                                            // Store the actual list of options
                                            configItem.CustomProperties["PowerPlanOptions"] = sliderLabels.Cast<object>().Select(l => l.ToString()).ToList();
                                            _logService.Log(LogLevel.Info, $"Stored PowerPlanOptions for {configItem.Name}: {labelsString}");
                                        }
                                    }
                                }
                            }
                        }
                        
                        // Add custom properties from RegistrySetting if available
                        if (registrySettingProperty != null)
                        {
                            var registrySetting = registrySettingProperty.GetValue(item);
                            if (registrySetting != null)
                            {
                                // Get the CustomProperties property from RegistrySetting
                                var customPropertiesProperty = registrySetting.GetType().GetProperty("CustomProperties");
                                if (customPropertiesProperty != null)
                                {
                                    var customProperties = customPropertiesProperty.GetValue(registrySetting) as Dictionary<string, object>;
                                    if (customProperties != null && customProperties.Count > 0)
                                    {
                                        // Copy custom properties to the configuration item
                                        foreach (var kvp in customProperties)
                                        {
                                            configItem.CustomProperties[kvp.Key] = kvp.Value;
                                        }
                                    }
                                }
                            }
                        }
                        
                        // Ensure SelectedValue is set for ComboBox controls
                        configItem.EnsureSelectedValueIsSet();
                        
                        // Add the configuration item to the file
                        configFile.Items.Add(configItem);
                    }
                }

                // Serialize the configuration file to JSON
                var json = JsonConvert.SerializeObject(configFile, Formatting.Indented);

                // Write the JSON to the file
                await File.WriteAllTextAsync(saveFileDialog.FileName, json);
                
                _logService.Log(LogLevel.Info, $"Successfully saved {configType} configuration with {configFile.Items.Count} items to {saveFileDialog.FileName}");
                
                // Log details about ComboBox items if any
                var comboBoxItems = configFile.Items.Where(i => i.ControlType == ControlType.ComboBox).ToList();
                if (comboBoxItems.Any())
                {
                    _logService.Log(LogLevel.Info, $"Saved {comboBoxItems.Count} ComboBox items:");
                    foreach (var item in comboBoxItems)
                    {
                        _logService.Log(LogLevel.Info, $"  - {item.Name}: SelectedValue={item.SelectedValue}, CustomProperties={string.Join(", ", item.CustomProperties.Select(kv => $"{kv.Key}={kv.Value}"))}");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error saving configuration: {ex.Message}");
                MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Loads a configuration file and returns the configuration file.
        /// </summary>
        /// <param name="configType">The type of configuration being loaded.</param>
        /// <returns>A task representing the asynchronous operation. Returns the configuration file if successful, null otherwise.</returns>
        public async Task<ConfigurationFile> LoadConfigurationAsync(string configType)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Starting to load {configType} configuration");
                // Create an open file dialog
                var openFileDialog = new OpenFileDialog
                {
                    Filter = FileFilter,
                    DefaultExt = FileExtension,
                    Title = "Open Configuration"
                };

                // Show the open file dialog
                if (openFileDialog.ShowDialog() != true)
                {
                    _logService.Log(LogLevel.Info, "Load configuration canceled by user");
                    return null;
                }
                
                _logService.Log(LogLevel.Info, $"Loading configuration from {openFileDialog.FileName}");

                // Read the JSON from the file
                var json = await File.ReadAllTextAsync(openFileDialog.FileName);

                // Deserialize the JSON to a configuration file
                var configFile = JsonConvert.DeserializeObject<ConfigurationFile>(json);
                
                // Ensure SelectedValue is set for all items
                foreach (var item in configFile.Items)
                {
                    item.EnsureSelectedValueIsSet();
                }
                
                // Process the configuration items to ensure SelectedValue is set for ComboBox and ThreeStateSlider items
                foreach (var item in configFile.Items)
                {
                    // Handle ComboBox items
                    if (item.ControlType == ControlType.ComboBox && string.IsNullOrEmpty(item.SelectedValue))
                    {
                        // Try to get the SelectedTheme from CustomProperties
                        if (item.CustomProperties.TryGetValue("SelectedTheme", out var selectedTheme) && selectedTheme != null)
                        {
                            item.SelectedValue = selectedTheme.ToString();
                            _logService.Log(LogLevel.Info, $"Set SelectedValue for ComboBox {item.Name} from CustomProperties: {item.SelectedValue}");
                        }
                        // If not available, try to derive it from SliderValue
                        else if (item.CustomProperties.TryGetValue("SliderValue", out var sliderValue))
                        {
                            int sliderValueInt = Convert.ToInt32(sliderValue);
                            item.SelectedValue = sliderValueInt == 1 ? "Dark Mode" : "Light Mode";
                            _logService.Log(LogLevel.Info, $"Derived SelectedValue for ComboBox {item.Name} from SliderValue: {sliderValueInt} -> {item.SelectedValue}");
                        }
                    }
                    
                    // Handle ThreeStateSlider items
                    if (item.ControlType == ControlType.ThreeStateSlider)
                    {
                        // Ensure SliderValue is available in CustomProperties
                        if (item.CustomProperties.TryGetValue("SliderValue", out var sliderValue))
                        {
                            int sliderValueInt = Convert.ToInt32(sliderValue);
                            
                            // Try to derive SelectedValue from SliderLabels if available
                            if (item.CustomProperties.TryGetValue("SliderLabels", out var sliderLabelsString) &&
                                sliderLabelsString != null)
                            {
                                var labels = sliderLabelsString.ToString().Split(',');
                                if (sliderValueInt >= 0 && sliderValueInt < labels.Length)
                                {
                                    item.SelectedValue = labels[sliderValueInt];
                                    _logService.Log(LogLevel.Info, $"Derived SelectedValue for ThreeStateSlider {item.Name} from SliderLabels: {sliderValueInt} -> {item.SelectedValue}");
                                }
                            }
                            // If no labels available, use a generic approach
                            else if (string.IsNullOrEmpty(item.SelectedValue))
                            {
                                // For UAC slider
                                if (item.Name.Contains("User Account Control") || item.Name.Contains("UAC"))
                                {
                                    item.SelectedValue = sliderValueInt switch
                                    {
                                        0 => "Low",
                                        1 => "Moderate",
                                        2 => "High",
                                        _ => $"Level {sliderValueInt}"
                                    };
                                }
                                // For Power Plan slider
                                else if (item.Name.Contains("Power Plan"))
                                {
                                    // First try to get the value from PowerPlanOptions if available
                                    if (item.CustomProperties.TryGetValue("PowerPlanOptions", out var powerPlanOptions))
                                    {
                                        // Handle different types of PowerPlanOptions
                                        if (powerPlanOptions is List<string> options && sliderValueInt >= 0 && sliderValueInt < options.Count)
                                        {
                                            item.SelectedValue = options[sliderValueInt];
                                            _logService.Log(LogLevel.Info, $"Set SelectedValue for Power Plan from PowerPlanOptions (List<string>): {item.SelectedValue}");
                                        }
                                        else if (powerPlanOptions is Newtonsoft.Json.Linq.JArray jArray && sliderValueInt >= 0 && sliderValueInt < jArray.Count)
                                        {
                                            item.SelectedValue = jArray[sliderValueInt]?.ToString();
                                            _logService.Log(LogLevel.Info, $"Set SelectedValue for Power Plan from PowerPlanOptions (JArray): {item.SelectedValue}");
                                        }
                                        else
                                        {
                                            // If PowerPlanOptions exists but we can't use it, log the issue
                                            _logService.Log(LogLevel.Warning, $"PowerPlanOptions exists but couldn't be used. Type: {powerPlanOptions?.GetType().Name}, SliderValue: {sliderValueInt}");
                                            
                                            // Fall back to default values
                                            item.SelectedValue = sliderValueInt switch
                                            {
                                                0 => "Balanced",
                                                1 => "High Performance",
                                                2 => "Ultimate Performance",
                                                _ => $"Plan {sliderValueInt}"
                                            };
                                            _logService.Log(LogLevel.Info, $"Set SelectedValue for Power Plan from default mapping: {item.SelectedValue}");
                                            
                                            // Add PowerPlanOptions if it doesn't exist in the right format
                                            string[] defaultOptions = { "Balanced", "High Performance", "Ultimate Performance" };
                                            item.CustomProperties["PowerPlanOptions"] = new List<string>(defaultOptions);
                                            _logService.Log(LogLevel.Info, $"Added default PowerPlanOptions to CustomProperties");
                                        }
                                    }
                                    else
                                    {
                                        // Fall back to default values
                                        item.SelectedValue = sliderValueInt switch
                                        {
                                            0 => "Balanced",
                                            1 => "High Performance",
                                            2 => "Ultimate Performance",
                                            _ => $"Plan {sliderValueInt}"
                                        };
                                        _logService.Log(LogLevel.Info, $"Set SelectedValue for Power Plan from default mapping: {item.SelectedValue}");
                                        
                                        // Add PowerPlanOptions if it doesn't exist
                                        string[] defaultOptions = { "Balanced", "High Performance", "Ultimate Performance" };
                                        item.CustomProperties["PowerPlanOptions"] = new List<string>(defaultOptions);
                                        _logService.Log(LogLevel.Info, $"Added default PowerPlanOptions to CustomProperties");
                                    }
                                }
                                // Generic approach for other sliders
                                else
                                {
                                    item.SelectedValue = $"Level {sliderValueInt}";
                                }
                                
                                _logService.Log(LogLevel.Info, $"Set generic SelectedValue for ThreeStateSlider {item.Name}: {item.SelectedValue}");
                            }
                        }
                    }
                }

                // Verify the configuration type
                if (string.IsNullOrEmpty(configFile.ConfigType))
                {
                    _logService.Log(LogLevel.Warning, $"Configuration type is empty, setting it to {configType}");
                    configFile.ConfigType = configType;
                }
                else if (configFile.ConfigType != configType)
                {
                    _logService.Log(LogLevel.Warning, $"Configuration type mismatch. Expected {configType}, but found {configFile.ConfigType}. Proceeding anyway.");
                    // We'll proceed anyway, as this might be a unified configuration file
                }

                _logService.Log(LogLevel.Info, $"Successfully loaded {configType} configuration with {configFile.Items.Count} items from {openFileDialog.FileName}");
                
                // Log details about ComboBox items if any
                var comboBoxItems = configFile.Items.Where(i => i.ControlType == ControlType.ComboBox).ToList();
                if (comboBoxItems.Any())
                {
                    _logService.Log(LogLevel.Info, $"Loaded {comboBoxItems.Count} ComboBox items:");
                    foreach (var item in comboBoxItems)
                    {
                        _logService.Log(LogLevel.Info, $"  - {item.Name}: SelectedValue={item.SelectedValue}, CustomProperties={string.Join(", ", item.CustomProperties.Select(kv => $"{kv.Key}={kv.Value}"))}");
                    }
                }

                // Return the configuration file directly
                // The ViewModel will handle matching these with existing items
                return configFile;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading configuration: {ex.Message}");
                MessageBox.Show($"Error loading configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// Saves a unified configuration file containing settings for multiple parts of the application.
        /// </summary>
        /// <param name="unifiedConfig">The unified configuration to save.</param>
        /// <returns>A task representing the asynchronous operation. Returns true if successful, false otherwise.</returns>
        public async Task<bool> SaveUnifiedConfigurationAsync(UnifiedConfigurationFile unifiedConfig)
        {
                            try
                            {
                                _logService.Log(LogLevel.Info, "Starting to save unified configuration");
                                
                                // Create a save file dialog
                                var saveFileDialog = new SaveFileDialog
                                {
                                    Filter = UnifiedFileFilter,
                                    DefaultExt = FileExtension,
                                    Title = "Save Unified Configuration",
                                    FileName = $"Winhance_Unified_Config_{DateTime.Now:yyyyMMdd}{FileExtension}"
                                };
                
                                // Show the save file dialog
                                if (saveFileDialog.ShowDialog() != true)
                                {
                                    _logService.Log(LogLevel.Info, "Save unified configuration canceled by user");
                                    return false;
                                }
                                
                                _logService.Log(LogLevel.Info, $"Saving unified configuration to {saveFileDialog.FileName}");
                
                                // Ensure all sections are included by default
                                unifiedConfig.WindowsApps.IsIncluded = true;
                                unifiedConfig.ExternalApps.IsIncluded = true;
                                unifiedConfig.Customize.IsIncluded = true;
                                unifiedConfig.Optimize.IsIncluded = true;
                                
                                if (unifiedConfig.WindowsApps.Items == null)
                                {
                                    unifiedConfig.WindowsApps.Items = new List<ConfigurationItem>();
                                }
                                
                                // Serialize the configuration file to JSON
                                var json = JsonConvert.SerializeObject(unifiedConfig, Formatting.Indented);
                
                                // Write the JSON to the file
                                await File.WriteAllTextAsync(saveFileDialog.FileName, json);
                                
                                _logService.Log(LogLevel.Info, "Successfully saved unified configuration");
                                
                                // Log details about included sections
                                var includedSections = new List<string> { "WindowsApps", "ExternalApps", "Customize", "Optimize" };
                                _logService.Log(LogLevel.Info, $"Included sections: {string.Join(", ", includedSections)}");
                
                                return true;
                            }
                            catch (Exception ex)
                            {
                                _logService.Log(LogLevel.Error, $"Error saving unified configuration: {ex.Message}");
                                MessageBox.Show($"Error saving unified configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                return false;
                            }
                        }

        /// <summary>
        /// Loads a unified configuration file.
        /// </summary>
        /// <returns>A task representing the asynchronous operation. Returns the unified configuration file if successful, null otherwise.</returns>
        public async Task<UnifiedConfigurationFile> LoadUnifiedConfigurationAsync()
        {
                            try
                            {
                                _logService.Log(LogLevel.Info, "Starting to load unified configuration");
                                
                                // Create an open file dialog
                                var openFileDialog = new OpenFileDialog
                                {
                                    Filter = UnifiedFileFilter,
                                    DefaultExt = FileExtension,
                                    Title = "Open Unified Configuration"
                                };
                
                                // Show the open file dialog
                                if (openFileDialog.ShowDialog() != true)
                                {
                                    _logService.Log(LogLevel.Info, "Load unified configuration canceled by user");
                                    return null;
                                }
                                
                                _logService.Log(LogLevel.Info, $"Loading unified configuration from {openFileDialog.FileName}");
                
                                // Read the JSON from the file
                                var json = await File.ReadAllTextAsync(openFileDialog.FileName);
                
                                return DeserializeUnifiedConfiguration(json);
                            }
                            catch (Exception ex)
                            {
                                _logService.Log(LogLevel.Error, $"Error loading unified configuration: {ex.Message}");
                                MessageBox.Show($"Error loading unified configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                return null;
                            }
                        }
                        
        /// <summary>
        /// Downloads and loads the recommended configuration file from GitHub.
        /// </summary>
        /// <returns>A task representing the asynchronous operation. Returns the unified configuration file if successful, null otherwise.</returns>
        public async Task<UnifiedConfigurationFile> LoadRecommendedConfigurationAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Starting to download recommended configuration");
                
                // URL of the recommended configuration file
                const string recommendedConfigUrl = "https://github.com/memstechtips/Winhance/blob/main/Winhance_Recommended_Config.winhance";
                
                // Use the raw content URL for direct download
                string rawUrl = recommendedConfigUrl.Replace("github.com", "raw.githubusercontent.com").Replace("/blob/", "/");
                
                _logService.Log(LogLevel.Info, $"Downloading configuration from {rawUrl}");
                
                // Create HTTP client
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Winhance");
                
                // Download the file
                var response = await client.GetAsync(rawUrl);
                
                // Check if the download was successful
                if (!response.IsSuccessStatusCode)
                {
                    string errorMessage = $"Failed to download recommended configuration. Status code: {response.StatusCode}";
                    _logService.Log(LogLevel.Error, errorMessage);
                    MessageBox.Show(errorMessage, "Download Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
                
                // Read the JSON content
                var json = await response.Content.ReadAsStringAsync();
                
                _logService.Log(LogLevel.Info, "Successfully downloaded recommended configuration");
                
                return DeserializeUnifiedConfiguration(json);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error downloading recommended configuration: {ex.Message}");
                MessageBox.Show($"Error downloading recommended configuration: {ex.Message}", "Download Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
        
        /// <summary>
        /// Deserializes a JSON string into a UnifiedConfigurationFile object.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>The deserialized UnifiedConfigurationFile object.</returns>
        private UnifiedConfigurationFile DeserializeUnifiedConfiguration(string json)
        {
            try
            {
                // Deserialize the JSON to a unified configuration file
                var unifiedConfig = JsonConvert.DeserializeObject<UnifiedConfigurationFile>(json);
                
                // Always ensure WindowsApps are included
                unifiedConfig.WindowsApps.IsIncluded = true;
                if (unifiedConfig.WindowsApps.Items == null)
                {
                    unifiedConfig.WindowsApps.Items = new List<ConfigurationItem>();
                }
                
                // Log details about included sections
                var includedSections = new List<string>();
                if (unifiedConfig.WindowsApps.IsIncluded) includedSections.Add("WindowsApps");
                if (unifiedConfig.ExternalApps.IsIncluded) includedSections.Add("ExternalApps");
                if (unifiedConfig.Customize.IsIncluded) includedSections.Add("Customize");
                if (unifiedConfig.Optimize.IsIncluded) includedSections.Add("Optimize");
                
                _logService.Log(LogLevel.Info, $"Loaded unified configuration with sections: {string.Join(", ", includedSections)}");
                
                return unifiedConfig;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error deserializing unified configuration: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates a unified configuration file from individual configuration sections.
        /// </summary>
        /// <param name="sections">Dictionary of section names and their corresponding configuration items.</param>
        /// <param name="includedSections">List of section names to include in the unified configuration.</param>
        /// <returns>A unified configuration file.</returns>
        public UnifiedConfigurationFile CreateUnifiedConfiguration(Dictionary<string, IEnumerable<ISettingItem>> sections, IEnumerable<string> includedSections)
        {
                            try
                            {
                                _logService.Log(LogLevel.Info, "Creating unified configuration");
                                
                                var unifiedConfig = new UnifiedConfigurationFile
                                {
                                    CreatedAt = DateTime.UtcNow
                                };
                
                                // Convert each section to ConfigurationItems and add to the appropriate section
                                foreach (var sectionName in includedSections)
                                {
                                    if (!sections.TryGetValue(sectionName, out var items) || items == null)
                                    {
                                        _logService.Log(LogLevel.Warning, $"Section {sectionName} not found or has no items");
                                        continue;
                                    }
                
                                    var configItems = ConvertToConfigurationItems(items);
                                    
                                    switch (sectionName)
                                    {
                                        case "WindowsApps":
                                            unifiedConfig.WindowsApps.IsIncluded = true;
                                            unifiedConfig.WindowsApps.Items = configItems;
                                            unifiedConfig.WindowsApps.Description = "Windows built-in applications";
                                            break;
                                        case "ExternalApps":
                                            unifiedConfig.ExternalApps.IsIncluded = true;
                                            unifiedConfig.ExternalApps.Items = configItems;
                                            unifiedConfig.ExternalApps.Description = "Third-party applications";
                                            break;
                                        case "Customize":
                                            unifiedConfig.Customize.IsIncluded = true;
                                            unifiedConfig.Customize.Items = configItems;
                                            unifiedConfig.Customize.Description = "Windows UI customization settings";
                                            break;
                                        case "Optimize":
                                            unifiedConfig.Optimize.IsIncluded = true;
                                            unifiedConfig.Optimize.Items = configItems;
                                            unifiedConfig.Optimize.Description = "Windows optimization settings";
                                            break;
                                        default:
                                            _logService.Log(LogLevel.Warning, $"Unknown section name: {sectionName}");
                                            break;
                                    }
                                }
                                
                                // Always ensure WindowsApps are included, even if they weren't in the sections dictionary
                                // or if they didn't have any items
                                unifiedConfig.WindowsApps.IsIncluded = true;
                                if (unifiedConfig.WindowsApps.Items == null)
                                {
                                    unifiedConfig.WindowsApps.Items = new List<ConfigurationItem>();
                                }
                                if (string.IsNullOrEmpty(unifiedConfig.WindowsApps.Description))
                                {
                                    unifiedConfig.WindowsApps.Description = "Windows built-in applications";
                                }
                                
                                _logService.Log(LogLevel.Info, "Successfully created unified configuration");
                                return unifiedConfig;
                            }
                            catch (Exception ex)
                            {
                                _logService.Log(LogLevel.Error, $"Error creating unified configuration: {ex.Message}");
                                throw;
                            }
                        }

        /// <summary>
        /// Extracts a specific section from a unified configuration file.
        /// </summary>
        /// <param name="unifiedConfig">The unified configuration file.</param>
        /// <param name="sectionName">The name of the section to extract.</param>
        /// <returns>A configuration file containing only the specified section.</returns>
        public ConfigurationFile ExtractSectionFromUnifiedConfiguration(UnifiedConfigurationFile unifiedConfig, string sectionName)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Extracting section {sectionName} from unified configuration");
                
                // Validate inputs
                if (unifiedConfig == null)
                {
                    _logService.Log(LogLevel.Error, "Unified configuration is null");
                    throw new ArgumentNullException(nameof(unifiedConfig));
                }
                
                if (string.IsNullOrEmpty(sectionName))
                {
                    _logService.Log(LogLevel.Error, "Section name is null or empty");
                    throw new ArgumentException("Section name cannot be null or empty", nameof(sectionName));
                }
                
                var configFile = new ConfigurationFile
                {
                    ConfigType = sectionName,
                    CreatedAt = DateTime.UtcNow,
                    Items = new List<ConfigurationItem>()
                };
                
                // Get the items from the appropriate section
                switch (sectionName)
                {
                    case "WindowsApps":
                        // Always include WindowsApps, regardless of IsIncluded flag
                        configFile.Items = unifiedConfig.WindowsApps?.Items ?? new List<ConfigurationItem>();
                        break;
                    case "ExternalApps":
                        if (unifiedConfig.ExternalApps?.IsIncluded == true)
                        {
                            configFile.Items = unifiedConfig.ExternalApps.Items ?? new List<ConfigurationItem>();
                        }
                        break;
                    case "Customize":
                        if (unifiedConfig.Customize?.IsIncluded == true)
                        {
                            configFile.Items = unifiedConfig.Customize.Items ?? new List<ConfigurationItem>();
                        }
                        break;
                    case "Optimize":
                        if (unifiedConfig.Optimize?.IsIncluded == true)
                        {
                            configFile.Items = unifiedConfig.Optimize.Items ?? new List<ConfigurationItem>();
                        }
                        break;
                    default:
                        _logService.Log(LogLevel.Warning, $"Unknown section name: {sectionName}");
                        break;
                }
                
                // Ensure Items is not null
                if (configFile.Items == null)
                {
                    configFile.Items = new List<ConfigurationItem>();
                }
                
                _logService.Log(LogLevel.Info, $"Successfully extracted section {sectionName} with {configFile.Items.Count} items");
                return configFile;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error extracting section from unified configuration: {ex.Message}");
                
                // Return an empty configuration file instead of throwing
                var emptyConfigFile = new ConfigurationFile
                {
                    ConfigType = sectionName,
                    CreatedAt = DateTime.UtcNow,
                    Items = new List<ConfigurationItem>()
                };
                
                _logService.Log(LogLevel.Info, $"Returning empty configuration file for section {sectionName}");
                return emptyConfigFile;
            }
        }

        /// <summary>
        /// Converts a collection of ISettingItem objects to a list of ConfigurationItem objects.
        /// </summary>
        /// <param name="items">The collection of ISettingItem objects to convert.</param>
        /// <returns>A list of ConfigurationItem objects.</returns>
        private List<ConfigurationItem> ConvertToConfigurationItems(IEnumerable<ISettingItem> items)
        {
            var configItems = new List<ConfigurationItem>();
            
            foreach (var item in items)
            {
                var configItem = new ConfigurationItem
                {
                    Name = item.Name,
                    IsSelected = item.IsSelected,
                    ControlType = item.ControlType,
                    CustomProperties = new Dictionary<string, object>()
                };
                
                // Add Id to custom properties
                if (!string.IsNullOrEmpty(item.Id))
                {
                    configItem.CustomProperties["Id"] = item.Id;
                }
                
                // Add GroupName to custom properties
                if (!string.IsNullOrEmpty(item.GroupName))
                {
                    configItem.CustomProperties["GroupName"] = item.GroupName;
                }
                
                // Add Description to custom properties
                if (!string.IsNullOrEmpty(item.Description))
                {
                    configItem.CustomProperties["Description"] = item.Description;
                }
                
                // Handle specific properties based on the item's type
                var itemType = item.GetType();
                var properties = itemType.GetProperties();
                
                // Check for PackageName property
                var packageNameProperty = properties.FirstOrDefault(p => p.Name == "PackageName");
                if (packageNameProperty != null)
                {
                    configItem.PackageName = packageNameProperty.GetValue(item)?.ToString();
                }
                
                // Check for SelectedValue property
                var selectedValueProperty = properties.FirstOrDefault(p => p.Name == "SelectedValue");
                if (selectedValueProperty != null)
                {
                    configItem.SelectedValue = selectedValueProperty.GetValue(item)?.ToString();
                }
                
                // Check for SliderValue property
                var sliderValueProperty = properties.FirstOrDefault(p => p.Name == "SliderValue");
                if (sliderValueProperty != null)
                {
                    var sliderValue = sliderValueProperty.GetValue(item);
                    if (sliderValue != null)
                    {
                        configItem.CustomProperties["SliderValue"] = sliderValue;
                    }
                }
                
                // Check for SliderLabels property
                var sliderLabelsProperty = properties.FirstOrDefault(p => p.Name == "SliderLabels");
                if (sliderLabelsProperty != null)
                {
                    var sliderLabels = sliderLabelsProperty.GetValue(item) as System.Collections.IList;
                    if (sliderLabels != null && sliderLabels.Count > 0)
                    {
                        // Store the labels as a comma-separated string
                        var labelsString = string.Join(",", sliderLabels.Cast<object>().Select(l => l.ToString()));
                        configItem.CustomProperties["SliderLabels"] = labelsString;
                        
                        // For Power Plan, also store the labels as PowerPlanOptions
                        if (item.Name.Contains("Power Plan") || item.Id == "PowerPlanComboBox")
                        {
                            // Store the actual list of options
                            configItem.CustomProperties["PowerPlanOptions"] = sliderLabels.Cast<object>().Select(l => l.ToString()).ToList();
                        }
                    }
                }
                
                // Check for RegistrySetting property
                var registrySettingProperty = properties.FirstOrDefault(p => p.Name == "RegistrySetting");
                if (registrySettingProperty != null)
                {
                    var registrySetting = registrySettingProperty.GetValue(item);
                    if (registrySetting != null)
                    {
                        // Get the CustomProperties property from RegistrySetting
                        var customPropertiesProperty = registrySetting.GetType().GetProperty("CustomProperties");
                        if (customPropertiesProperty != null)
                        {
                            var customProperties = customPropertiesProperty.GetValue(registrySetting) as Dictionary<string, object>;
                            if (customProperties != null && customProperties.Count > 0)
                            {
                                // Copy custom properties to the configuration item
                                foreach (var kvp in customProperties)
                                {
                                    configItem.CustomProperties[kvp.Key] = kvp.Value;
                                }
                            }
                        }
                    }
                }
                
                // Ensure SelectedValue is set for ComboBox controls
                configItem.EnsureSelectedValueIsSet();
                
                configItems.Add(configItem);
            }
            
            return configItems;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Services.Configuration
{
    /// <summary>
    /// Service for updating properties based on configuration settings.
    /// </summary>
    public class PropertyUpdater : IPropertyUpdater
    {
        private readonly ILogService _logService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyUpdater"/> class.
        /// </summary>
        /// <param name="logService">The log service.</param>
        public PropertyUpdater(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Updates items in a collection based on configuration settings.
        /// </summary>
        /// <param name="items">The collection of items to update.</param>
        /// <param name="configFile">The configuration file containing settings to apply.</param>
        /// <returns>The number of items that were updated.</returns>
        public async Task<int> UpdateItemsAsync(IEnumerable<object> items, ConfigurationFile configFile)
        {
            if (items == null)
            {
                _logService.Log(LogLevel.Error, "Cannot update null items collection");
                return 0;
            }

            if (configFile == null || configFile.Items == null || !configFile.Items.Any())
            {
                _logService.Log(LogLevel.Warning, "Config file has no items to apply");
                return 0;
            }

            int updatedCount = 0;
            var itemsList = items.ToList();

            foreach (var configItem in configFile.Items)
            {
                try
                {
                    // Find matching item in the collection by ID or name
                    var matchingItem = FindMatchingItem(itemsList, configItem);
                    if (matchingItem == null)
                    {
                        continue;
                    }

                    // Update properties based on configuration
                    if (UpdateItemProperties(matchingItem, configItem))
                    {
                        updatedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error, $"Error updating item {configItem.Name}: {ex.Message}");
                }
            }

            return updatedCount;
        }

        private object FindMatchingItem(IEnumerable<object> items, ConfigurationItem configItem)
        {
            // Try to find by ID first
            if (configItem.CustomProperties.TryGetValue("Id", out var id) && id != null)
            {
                var idString = id.ToString();
                var matchById = items.FirstOrDefault(item => 
                {
                    var idProperty = item.GetType().GetProperty("Id");
                    return idProperty != null && idProperty.GetValue(item)?.ToString() == idString;
                });

                if (matchById != null)
                {
                    return matchById;
                }
            }

            // Then try by name
            if (!string.IsNullOrEmpty(configItem.Name))
            {
                var matchByName = items.FirstOrDefault(item => 
                {
                    var nameProperty = item.GetType().GetProperty("Name");
                    return nameProperty != null && nameProperty.GetValue(item)?.ToString() == configItem.Name;
                });

                if (matchByName != null)
                {
                    return matchByName;
                }
            }

            return null;
        }

        private bool UpdateItemProperties(object item, ConfigurationItem configItem)
        {
            bool updated = false;
            var itemType = item.GetType();

            // Update IsSelected property if it exists
            var isSelectedProperty = itemType.GetProperty("IsSelected");
            if (isSelectedProperty != null && isSelectedProperty.CanWrite)
            {
                isSelectedProperty.SetValue(item, configItem.IsSelected);
                updated = true;
            }

            // Update SelectedValue property if it exists and has a value
            if (!string.IsNullOrEmpty(configItem.SelectedValue))
            {
                var selectedValueProperty = itemType.GetProperty("SelectedValue");
                if (selectedValueProperty != null && selectedValueProperty.CanWrite)
                {
                    selectedValueProperty.SetValue(item, configItem.SelectedValue);
                    updated = true;
                }
            }

            // Update custom properties
            foreach (var customProp in configItem.CustomProperties)
            {
                var property = itemType.GetProperty(customProp.Key);
                if (property != null && property.CanWrite)
                {
                    try
                    {
                        var convertedValue = Convert.ChangeType(customProp.Value, property.PropertyType);
                        property.SetValue(item, convertedValue);
                        updated = true;
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Warning, 
                            $"Could not convert value for property {customProp.Key}: {ex.Message}");
                    }
                }
            }

            return updated;
        }
    }
}

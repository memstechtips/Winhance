using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Service for setting up ComboBox options from SettingDefinitions.
    /// Handles ComboBoxDisplayNames + ValueMappings pattern only.
    /// Follows DRY and SRP principles with single source of truth.
    /// </summary>
    public class ComboBoxSetupService : IComboBoxSetupService
    {
        private readonly ILogService _logService;

        public ComboBoxSetupService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public ComboBoxSetupResult SetupComboBoxOptions(SettingDefinition setting, object? currentValue)
        {
            var result = new ComboBoxSetupResult();

            try
            {
                if (setting.InputType != SettingInputType.Selection)
                {
                    result.ErrorMessage = $"Setting '{setting.Id}' is not a ComboBox control";
                    return result;
                }

                // All settings now use ComboBoxDisplayNames + ValueMappings pattern
                if (SetupFromComboBoxDisplayNames(setting, currentValue, result))
                {
                    result.Success = true;
                    _logService.Log(LogLevel.Info, $"ComboBoxSetupService: Successfully setup ComboBox for '{setting.Id}' using ComboBoxDisplayNames pattern");
                    return result;
                }

                result.ErrorMessage = $"Invalid ComboBox metadata for setting '{setting.Id}'";
                _logService.Log(LogLevel.Warning, result.ErrorMessage);
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error setting up ComboBox for '{setting.Id}': {ex.Message}";
                _logService.Log(LogLevel.Error, result.ErrorMessage);
                return result;
            }
        }

        /// <summary>
        /// Sets up ComboBox using ComboBoxDisplayNames + ValueMappings pattern.
        /// This is now the only supported pattern for all ComboBox settings.
        /// </summary>
        private bool SetupFromComboBoxDisplayNames(SettingDefinition setting, object? currentValue, ComboBoxSetupResult result)
        {
            if (!setting.CustomProperties?.ContainsKey(CustomPropertyKeys.ComboBoxDisplayNames) == true ||
                !setting.CustomProperties?.ContainsKey(CustomPropertyKeys.ValueMappings) == true)
                return false;

            var displayNames = setting.CustomProperties[CustomPropertyKeys.ComboBoxDisplayNames] as string[];
            var valueMappings = setting.CustomProperties[CustomPropertyKeys.ValueMappings] as Dictionary<int, Dictionary<string, int>>;

            if (displayNames == null || valueMappings == null)
                return false;

            // Populate options using display names and their corresponding indices
            for (int i = 0; i < displayNames.Length; i++)
            {
                result.Options.Add(new ComboBoxOption
                {
                    DisplayText = displayNames[i],
                    Value = i // Use index as value for ValueMappings pattern
                });
            }

            // Use the already-resolved currentValue from ComboBoxResolver
            result.SelectedValue = currentValue;

            _logService.Log(LogLevel.Debug, $"ComboBoxSetupService: Populated {displayNames.Length} options from ComboBoxDisplayNames for '{setting.Id}'");
            return true;
        }

    }
}

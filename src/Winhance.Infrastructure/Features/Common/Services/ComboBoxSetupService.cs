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

        private bool SetupFromComboBoxDisplayNames(SettingDefinition setting, object? currentValue, ComboBoxSetupResult result)
        {
            if (!setting.CustomProperties?.ContainsKey(CustomPropertyKeys.ComboBoxDisplayNames) == true ||
                !setting.CustomProperties?.ContainsKey(CustomPropertyKeys.ValueMappings) == true)
                return false;

            var displayNames = setting.CustomProperties[CustomPropertyKeys.ComboBoxDisplayNames] as string[];
            var valueMappings = setting.CustomProperties[CustomPropertyKeys.ValueMappings] as Dictionary<int, Dictionary<string, int>>;

            if (displayNames == null || valueMappings == null)
                return false;

            var supportsCustomState = setting.CustomProperties?.TryGetValue(CustomPropertyKeys.SupportsCustomState, out var supports) == true && (bool)supports;
            var isCustomState = currentValue?.Equals(ComboBoxResolver.CUSTOM_STATE_INDEX) == true;
            
            string[] finalDisplayNames = displayNames;
            
            if (supportsCustomState && isCustomState)
            {
                var customDisplayName = setting.CustomProperties?.TryGetValue(CustomPropertyKeys.CustomStateDisplayName, out var customName) == true && customName is string customStr 
                    ? customStr 
                    : "Custom (User Defined)";
                    
                finalDisplayNames = displayNames.Append(customDisplayName).ToArray();
            }

            for (int i = 0; i < finalDisplayNames.Length; i++)
            {
                result.Options.Add(new ComboBoxOption
                {
                    DisplayText = finalDisplayNames[i],
                    Value = i < displayNames.Length ? i : ComboBoxResolver.CUSTOM_STATE_INDEX
                });
            }

            result.SelectedValue = isCustomState ? ComboBoxResolver.CUSTOM_STATE_INDEX : currentValue;

            _logService.Log(LogLevel.Debug, $"ComboBoxSetupService: Populated {finalDisplayNames.Length} options from ComboBoxDisplayNames for '{setting.Id}'");
            return true;
        }

    }
}

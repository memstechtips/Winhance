using System.Linq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

public class ConfigurationApplicationBridgeService
{
    private readonly ISettingApplicationService _settingApplicationService;
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly ILogService _logService;

    public ConfigurationApplicationBridgeService(
        ISettingApplicationService settingApplicationService,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        ILogService logService)
    {
        _settingApplicationService = settingApplicationService;
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _logService = logService;
    }

    public async Task<bool> ApplyConfigurationSectionAsync(
        ConfigSection section,
        string sectionName,
        Func<string, object?, SettingDefinition, Task<(bool confirmed, bool checkboxResult)>>? confirmationHandler = null)
    {
        if (section?.Items == null || !section.Items.Any())
        {
            _logService.Log(LogLevel.Warning, $"Section '{sectionName}' is empty or null");
            return false;
        }

        _logService.Log(LogLevel.Info, $"Applying {section.Items.Count} settings from {sectionName} section");

        int appliedCount = 0;
        int skippedOsCount = 0;
        int failCount = 0;

        foreach (var item in section.Items)
        {
            try
            {
                if (string.IsNullOrEmpty(item.Id))
                {
                    _logService.Log(LogLevel.Warning, $"Skipping item '{item.Name}' - no ID");
                    failCount++;
                    continue;
                }

                var allSettings = _compatibleSettingsRegistry.GetAllFilteredSettings();
                SettingDefinition setting = null;

                foreach (var featureSettings in allSettings.Values)
                {
                    setting = featureSettings.FirstOrDefault(s => s.Id == item.Id);
                    if (setting != null) break;
                }

                if (setting == null)
                {
                    _logService.Log(LogLevel.Debug,
                        $"Setting '{item.Id}' skipped (not compatible with this Windows version)");
                    skippedOsCount++;
                    continue;
                }

                bool checkboxResult = false;
                if (setting.RequiresConfirmation && confirmationHandler != null)
                {
                    var value = setting.InputType == InputType.Selection
                        ? (object)ResolveSelectionValue(setting, item)
                        : (object)(item.IsSelected ?? false);

                    var (confirmed, checkbox) = await confirmationHandler(item.Id, value, setting);

                    if (!confirmed)
                    {
                        _logService.Log(LogLevel.Info, $"User skipped setting '{item.Id}' during config import");
                        appliedCount++;
                        continue;
                    }

                    checkboxResult = checkbox;
                }

                object valueToApply = null;

                if (setting.InputType == InputType.Selection)
                {
                    valueToApply = ResolveSelectionValue(setting, item);
                }
                else if (setting.InputType == InputType.NumericRange)
                {
                    valueToApply = ResolveNumericRangeValue(item);
                }

                if (setting.InputType == InputType.Action && !string.IsNullOrEmpty(setting.ActionCommand))
                {
                    await _settingApplicationService.ApplySettingAsync(
                        item.Id,
                        false,
                        null,
                        checkboxResult,
                        setting.ActionCommand);
                }
                else
                {
                    await _settingApplicationService.ApplySettingAsync(
                        item.Id,
                        item.IsSelected ?? false,
                        valueToApply,
                        checkboxResult);
                }

                appliedCount++;
                _logService.Log(LogLevel.Debug, $"Applied setting: {item.Name}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Failed to apply setting '{item.Name}': {ex.Message}");
                failCount++;
            }
        }

        if (skippedOsCount > 0)
        {
            _logService.Log(LogLevel.Info,
                $"Section '{sectionName}': {appliedCount} applied, {skippedOsCount} skipped (OS incompatible), {failCount} failed");
        }
        else
        {
            _logService.Log(LogLevel.Info,
                $"Section '{sectionName}': {appliedCount} applied, {failCount} failed");
        }

        return failCount == 0;
    }

    private object ResolveSelectionValue(SettingDefinition setting, ConfigurationItem item)
    {
        if (setting.Id == "power-plan-selection")
        {
            return ResolvePowerPlanValue(setting, item);
        }

        if (item.CustomStateValues != null && item.CustomStateValues.Count > 0)
        {
            return item.CustomStateValues;
        }

        if (item.SelectedIndex.HasValue)
        {
            return item.SelectedIndex.Value;
        }

        return 0;
    }

    private object ResolvePowerPlanValue(SettingDefinition setting, ConfigurationItem item)
    {
        if (!string.IsNullOrEmpty(item.PowerPlanGuid))
        {
            return new Dictionary<string, object>
            {
                ["Guid"] = item.PowerPlanGuid,
                ["Name"] = item.PowerPlanName ?? "Unknown"
            };
        }

        _logService.Log(LogLevel.Error, "Config file is missing PowerPlanGuid for power-plan-selection.");
        throw new InvalidOperationException("Configuration file is invalid or corrupted.");
    }

    private object ResolveNumericRangeValue(ConfigurationItem item)
    {
        if (item.PowerSettings == null || item.PowerSettings.Count == 0)
            return null;

        var hasAcValue = item.PowerSettings.TryGetValue("ACValue", out var acVal);
        var hasDcValue = item.PowerSettings.TryGetValue("DCValue", out var dcVal);

        if (hasAcValue || hasDcValue)
        {
            return new Dictionary<string, object?>
            {
                ["ACValue"] = acVal,
                ["DCValue"] = dcVal ?? acVal
            };
        }

        if (item.PowerSettings.TryGetValue("Value", out var singleVal))
        {
            return singleVal;
        }

        return null;
    }

}

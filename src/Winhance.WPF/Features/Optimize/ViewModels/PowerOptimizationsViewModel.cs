using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    public partial class PowerOptimizationsViewModel(
        IDomainServiceRouter domainServiceRouter,
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        IEventBus eventBus,
        IPowerPlanComboBoxService powerPlanComboBoxService,
        IComboBoxResolver comboBoxResolver,
        IHardwareDetectionService hardwareDetectionService,
        IPowerCfgQueryService powerCfgQueryService)
        : BaseSettingsFeatureViewModel(domainServiceRouter, settingsLoadingService, logService)
    {
        private ISubscriptionToken? _powerPlanChangedSubscription;

        public override string ModuleId => FeatureIds.Power;
        public override string DisplayName => "Power";

        public override async Task LoadSettingsAsync()
        {
            await base.LoadSettingsAsync();

            HasBattery = await hardwareDetectionService.HasBatteryAsync();

            _powerPlanChangedSubscription?.Dispose();
            _powerPlanChangedSubscription = eventBus.Subscribe<PowerPlanChangedEvent>(HandlePowerPlanChanged);
        }

        private async void HandlePowerPlanChanged(PowerPlanChangedEvent evt)
        {
            try
            {
                await Task.Delay(200);
                await RefreshPowerSettingsBatch();
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error handling power plan change: {ex.Message}");
            }
        }

        private async Task RefreshPowerSettingsBatch()
        {
            try
            {
                var powerService = domainServiceRouter.GetDomainService(ModuleId);
                if (powerService == null) return;

                var powerPlanSetting = Settings.FirstOrDefault(s =>
                    s.SettingDefinition?.CustomProperties?.ContainsKey("LoadDynamicOptions") == true);
                var settingsToUpdate = Settings.Where(s =>
                    s.SettingDefinition?.PowerCfgSettings?.Any() == true &&
                    s.SettingDefinition?.CustomProperties?.ContainsKey("LoadDynamicOptions") != true).ToList();

                if (!settingsToUpdate.Any() && powerPlanSetting == null) return;

                var allPowerSettingsACDC = await powerCfgQueryService.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT");

                foreach (var setting in settingsToUpdate)
                {
                    var powerCfgSetting = setting.SettingDefinition.PowerCfgSettings[0];
                    var settingKey = powerCfgSetting.SettingGuid;

                    if (!allPowerSettingsACDC.TryGetValue(settingKey, out var values)) continue;

                    var (acValue, dcValue) = values;

                    if (powerCfgSetting.PowerModeSupport == PowerModeSupport.Separate)
                    {
                        if (setting.InputType == InputType.NumericRange)
                        {
                            if (acValue.HasValue)
                            {
                                var acDisplayValue = ConvertSystemValueToDisplayValue(setting.SettingDefinition, acValue.Value);
                                await UpdateSettingAsync(setting, () =>
                                {
                                    setting.ACNumericValue = acDisplayValue;
                                    if (!HasBattery) setting.NumericValue = acDisplayValue;
                                });
                            }
                            if (dcValue.HasValue)
                            {
                                var dcDisplayValue = ConvertSystemValueToDisplayValue(setting.SettingDefinition, dcValue.Value);
                                await UpdateSettingAsync(setting, () => setting.DCNumericValue = dcDisplayValue);
                            }
                        }
                        else if (setting.InputType == InputType.Selection)
                        {
                            if (acValue.HasValue)
                            {
                                var acRawValues = new Dictionary<string, object?> { ["PowerCfgValue"] = acValue.Value };
                                var acResolvedIndex = await comboBoxResolver.ResolveCurrentValueAsync(setting.SettingDefinition, acRawValues);
                                await UpdateSettingAsync(setting, () =>
                                {
                                    setting.ACValue = acResolvedIndex;
                                    if (!HasBattery) setting.SelectedValue = acResolvedIndex;
                                });
                            }
                            if (dcValue.HasValue)
                            {
                                var dcRawValues = new Dictionary<string, object?> { ["PowerCfgValue"] = dcValue.Value };
                                var dcResolvedIndex = await comboBoxResolver.ResolveCurrentValueAsync(setting.SettingDefinition, dcRawValues);
                                await UpdateSettingAsync(setting, () => setting.DCValue = dcResolvedIndex);
                            }
                        }
                    }
                    else if (powerCfgSetting.PowerModeSupport == PowerModeSupport.Both)
                    {
                        if (setting.InputType == InputType.Toggle)
                        {
                            var isSelected = acValue.HasValue && acValue.Value != 0;
                            await UpdateSettingAsync(setting, () => setting.IsSelected = isSelected);
                        }
                        else if (setting.InputType == InputType.Selection && acValue.HasValue)
                        {
                            var rawValues = new Dictionary<string, object?> { ["PowerCfgValue"] = acValue.Value };
                            var resolvedIndex = await comboBoxResolver.ResolveCurrentValueAsync(setting.SettingDefinition, rawValues);
                            await UpdateSettingAsync(setting, () => setting.SelectedValue = resolvedIndex);
                        }
                        else if (setting.InputType == InputType.NumericRange && acValue.HasValue)
                        {
                            var displayValue = ConvertSystemValueToDisplayValue(setting.SettingDefinition, acValue.Value);
                            await UpdateSettingAsync(setting, () => setting.NumericValue = displayValue);
                        }
                    }
                }

                if (powerPlanSetting != null)
                {
                    await RefreshPowerPlanComboBox(powerPlanSetting);
                }
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error in power settings refresh: {ex.Message}");
            }
        }

        private async Task UpdateSettingAsync(SettingItemViewModel setting, Action updateAction)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                setting._isInitializing = true;
                updateAction();
                setting._isInitializing = false;
            });
        }

        private int ConvertSystemValueToDisplayValue(SettingDefinition setting, int systemValue)
        {
            if (setting.PowerCfgSettings?.Count > 0)
            {
                var powerCfgSetting = setting.PowerCfgSettings.First();
                var systemUnits = powerCfgSetting.Units ?? "";

                if (systemUnits.Equals("Seconds", StringComparison.OrdinalIgnoreCase))
                {
                    return systemValue / 60;
                }
            }

            return systemValue;
        }

        private async Task RefreshPowerPlanComboBox(SettingItemViewModel powerPlanSetting)
        {
            try
            {
                var options = await powerPlanComboBoxService.GetPowerPlanOptionsAsync();

                var powerService = domainServiceRouter.GetDomainService(ModuleId) as Winhance.Core.Features.Optimize.Interfaces.IPowerService;
                var activePlan = await powerService?.GetActivePowerPlanAsync();

                int currentIndex = 0;
                if (activePlan != null)
                {
                    for (int i = 0; i < options.Count; i++)
                    {
                        if (options[i].ExistsOnSystem && options[i].SystemPlan != null &&
                            string.Equals(options[i].SystemPlan.Guid, activePlan.Guid, StringComparison.OrdinalIgnoreCase))
                        {
                            currentIndex = i;
                            break;
                        }
                    }
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    powerPlanSetting._isInitializing = true;

                    var currentSelectedValue = powerPlanSetting.SelectedValue;

                    if (powerPlanSetting.ComboBoxOptions.Count == options.Count)
                    {
                        for (int i = 0; i < options.Count; i++)
                        {
                            powerPlanSetting.ComboBoxOptions[i].DisplayText = options[i].DisplayName;
                        }
                    }
                    else
                    {
                        powerPlanSetting.ComboBoxOptions.Clear();
                        for (int i = 0; i < options.Count; i++)
                        {
                            powerPlanSetting.ComboBoxOptions.Add(new Winhance.Core.Features.Common.Interfaces.ComboBoxOption
                            {
                                DisplayText = options[i].DisplayName,
                                Value = i
                            });
                        }
                    }

                    if (!Equals(currentSelectedValue, currentIndex))
                    {
                        powerPlanSetting.SelectedValue = currentIndex;
                    }

                    powerPlanSetting._isInitializing = false;
                });
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Failed to refresh power plan combo box: {ex.Message}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _powerPlanChangedSubscription?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

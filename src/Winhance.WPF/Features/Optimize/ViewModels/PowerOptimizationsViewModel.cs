using System.Windows;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
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
      IComboBoxResolver comboBoxResolver)
      : BaseSettingsFeatureViewModel(domainServiceRouter, settingsLoadingService, logService)
    {
        private readonly IEventBus _eventBus = eventBus;
        private ISubscriptionToken? _powerPlanChangedSubscription;

        public override string ModuleId => FeatureIds.Power;
        public override string DisplayName => "Power";

        public override async Task LoadSettingsAsync()
        {
            await base.LoadSettingsAsync();

            _powerPlanChangedSubscription?.Dispose();
            _powerPlanChangedSubscription = _eventBus.Subscribe<PowerPlanChangedEvent>(HandlePowerPlanChanged);
        }

        public override async Task<bool> HandleDomainContextSettingAsync(SettingDefinition setting, object? value, bool additionalContext = false)
        {
            try
            {
                logService.Log(LogLevel.Info, $"[PowerOptimizationsViewModel] Handling domain context setting '{setting.Id}'");

                if (setting.InputType == InputType.Selection && setting.CustomProperties?.ContainsKey("LoadDynamicOptions") == true)
                {
                    var options = await powerPlanComboBoxService.GetPowerPlanOptionsAsync();
                    if (value is not int index || index < 0 || index >= options.Count)
                    {
                        logService.Log(LogLevel.Error, $"Invalid index {value} for setting '{setting.Id}'");
                        return false;
                    }

                    var powerPlanGuid = options[index].SystemPlan?.Guid ?? options[index].PredefinedPlan?.Guid;
                    if (string.IsNullOrEmpty(powerPlanGuid))
                    {
                        logService.Log(LogLevel.Error, $"No GUID found for power plan at index {index}");
                        return false;
                    }

                    var powerService = domainServiceRouter.GetDomainService(ModuleId);
                    if (powerService == null)
                    {
                        logService.Log(LogLevel.Error, $"PowerService not available for '{setting.Id}'");
                        return false;
                    }

                    var context = new SettingOperationContext
                    {
                        SettingId = setting.Id,
                        Enable = true,
                        Value = powerPlanGuid,
                        AdditionalParameters = new Dictionary<string, object>
                        {
                            ["PlanIndex"] = index,
                            ["PlanName"] = options[index].DisplayName
                        }
                    };

                    dynamic service = powerService;
                    await service.ApplySettingWithContextAsync(setting.Id, true, powerPlanGuid, context);

                    logService.Log(LogLevel.Info, $"[PowerOptimizationsViewModel] Successfully applied power plan: {powerPlanGuid}");
                    return true;
                }

                logService.Log(LogLevel.Warning, $"[PowerOptimizationsViewModel] Unknown domain context setting type: '{setting.Id}'");
                return false;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"[PowerOptimizationsViewModel] Error handling setting '{setting.Id}': {ex.Message}");
                return false;
            }
        }

        private async void HandlePowerPlanChanged(PowerPlanChangedEvent evt)
        {
            try
            {
                logService.Log(LogLevel.Info, $"[PowerOptimizationsViewModel] HandlePowerPlanChanged called - NewPlanGuid: {evt.NewPlanGuid}, NewPlanName: '{evt.NewPlanName}', NewPlanIndex: {evt.NewPlanIndex}");
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

                var refreshedValues = await ((dynamic)powerService).RefreshCompatiblePowerSettingsAsync();

                var settingsToUpdate = Settings.Where(s => s.SettingDefinition?.PowerCfgSettings?.Any() == true);

                foreach (var setting in settingsToUpdate)
                {
                    var settingGuid = setting.SettingDefinition.PowerCfgSettings[0].SettingGuid;
                    if (refreshedValues.TryGetValue(settingGuid, out int? newValue))
                    {
                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            setting._isInitializing = true;

                            if (setting.InputType == InputType.NumericRange && newValue.HasValue)
                            {
                                var displayValue = ConvertSystemValueToDisplayValue(setting.SettingDefinition, newValue.Value);
                                setting.NumericValue = displayValue;
                                setting.SelectedValue = newValue.Value;
                            }
                            else if (setting.InputType == InputType.Selection && newValue.HasValue)
                            {
                                var rawValues = new Dictionary<string, object?> { ["PowerCfgValue"] = newValue.Value };
                                var resolvedIndex = await comboBoxResolver.ResolveCurrentValueAsync(setting.SettingDefinition, rawValues);
                                setting.SelectedValue = resolvedIndex;
                            }

                            setting._isInitializing = false;
                        });
                    }
                }

                logService.Log(LogLevel.Info, $"Bulk refreshed {refreshedValues.Count} power settings");
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error in bulk power settings refresh: {ex.Message}");
            }
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
                var currentIndex = await GetCurrentPowerPlanIndex(options);

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

        private async Task<int> GetCurrentPowerPlanIndex(List<PowerPlanComboBoxOption> options)
        {
            try
            {
                var powerService = domainServiceRouter.GetDomainService(ModuleId) as Winhance.Core.Features.Optimize.Interfaces.IPowerService;
                if (powerService == null) return 0;

                var activePlan = await powerService.GetActivePowerPlanAsync();
                if (activePlan == null) return 0;

                for (int i = 0; i < options.Count; i++)
                {
                    if (options[i].ExistsOnSystem && options[i].SystemPlan != null)
                    {
                        if (string.Equals(options[i].SystemPlan.Guid, activePlan.Guid, StringComparison.OrdinalIgnoreCase))
                        {
                            return i;
                        }
                    }
                }

                return 0;
            }
            catch
            {
                return 0;
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
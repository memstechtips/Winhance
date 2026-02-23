using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.Interfaces;
namespace Winhance.UI.Features.Optimize.ViewModels;

public partial class PowerOptimizationsViewModel : BaseSettingsFeatureViewModel, IOptimizationFeatureViewModel
{
    private readonly IDialogService _dialogService;
    private readonly IPowerPlanComboBoxService _powerPlanComboBoxService;
    private ISubscriptionToken? _powerPlanChangedSubscription;

    public override string ModuleId => FeatureIds.Power;

    protected override string GetDisplayNameKey() => "Feature_Power_Name";

    public IRelayCommand<PowerPlanComboBoxOption> DeletePowerPlanCommand { get; }

    public PowerOptimizationsViewModel(
        IDomainServiceRouter domainServiceRouter,
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IDialogService dialogService,
        IEventBus eventBus,
        IPowerPlanComboBoxService powerPlanComboBoxService)
        : base(domainServiceRouter, settingsLoadingService, logService, localizationService, dispatcherService, eventBus)
    {
        _dialogService = dialogService;
        _powerPlanComboBoxService = powerPlanComboBoxService;

        DeletePowerPlanCommand = new RelayCommand<PowerPlanComboBoxOption>(async plan => await DeletePowerPlanAsync(plan));
    }

    public override async Task LoadSettingsAsync()
    {
        await base.LoadSettingsAsync();

        _powerPlanChangedSubscription?.Dispose();
        _powerPlanChangedSubscription = _eventBus.SubscribeAsync<PowerPlanChangedEvent>(HandlePowerPlanChangedAsync);
    }

    private async Task HandlePowerPlanChangedAsync(PowerPlanChangedEvent evt)
    {
        try
        {
            await Task.Delay(200);
            await RefreshPowerPlanComboBoxAsync();

            // Refresh all setting states to pick up the new plan's PowerCfg values
            // (display timeout, sleep timeout, etc. differ between plans)
            await Task.Delay(500);
            await RefreshSettingStatesAsync();
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error handling power plan change: {ex.Message}");
        }
    }

    public async Task RefreshPowerPlanComboBoxAsync()
    {
        try
        {
            var powerPlanSetting = Settings.FirstOrDefault(s =>
                s.SettingDefinition?.CustomProperties?.ContainsKey("LoadDynamicOptions") == true);

            if (powerPlanSetting == null) return;

            // Invalidate the cache to ensure we get fresh data from the OS
            _powerPlanComboBoxService.InvalidateCache();

            var options = await _powerPlanComboBoxService.GetPowerPlanOptionsAsync();
            var powerService = _domainServiceRouter.GetDomainService(ModuleId) as IPowerService;
            var activePlan = await powerService?.GetActivePowerPlanAsync()!;

            int currentIndex = 0;
            if (activePlan != null)
            {
                for (int i = 0; i < options.Count; i++)
                {
                    if (options[i].ExistsOnSystem && options[i].SystemPlan != null &&
                        string.Equals(options[i].SystemPlan!.Guid, activePlan.Guid, StringComparison.OrdinalIgnoreCase))
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }

            // Build the new ComboBoxOption list before touching the UI
            var newItems = new List<ComboBoxOption>(options.Count);
            for (int i = 0; i < options.Count; i++)
            {
                var displayName = options[i].DisplayName;
                if (displayName.StartsWith("PowerPlan_"))
                {
                    displayName = _localizationService.GetString(displayName);
                }

                newItems.Add(new ComboBoxOption
                {
                    DisplayText = displayName,
                    Value = options[i].Index,
                    Description = options[i].ExistsOnSystem ? "Installed on system" : "Not installed",
                    Tag = options[i]
                });
            }

            // Await the UI update to ensure it completes before returning
            await _dispatcherService.RunOnUIThreadAsync(() =>
            {
                _logService.LogDebug($"[RefreshPowerPlanComboBox] Starting refresh, currentIndex={currentIndex}, current SelectedValue={powerPlanSetting.SelectedValue}");

                powerPlanSetting.ComboBoxOptions.Clear();

                foreach (var item in newItems)
                {
                    powerPlanSetting.ComboBoxOptions.Add(item);
                }

                _logService.LogDebug($"[RefreshPowerPlanComboBox] After repopulate ({newItems.Count} items), setting SelectedValue to {currentIndex}");
                powerPlanSetting.SelectedValue = currentIndex;

                return Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Failed to refresh power plan combo box: {ex.Message}");
        }
    }

    public async Task DeletePowerPlanAsync(PowerPlanComboBoxOption? planToDelete)
    {
        try
        {
            if (planToDelete == null) return;

            if (planToDelete.IsActive)
            {
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString("Dialog_CannotDeleteActivePlan_Message"),
                    _localizationService.GetString("Dialog_CannotDeleteActivePlan_Title"));
                return;
            }

            if (!planToDelete.ExistsOnSystem || planToDelete.SystemPlan == null)
            {
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString("Dialog_CannotDeletePlan_Message"),
                    _localizationService.GetString("Dialog_CannotDeletePlan_Title"));
                return;
            }

            var displayName = planToDelete.DisplayName;
            if (displayName.StartsWith("PowerPlan_"))
                displayName = _localizationService.GetString(displayName);

            var message = string.Format(_localizationService.GetString("Dialog_DeletePowerPlan_Message"), displayName);
            var title = _localizationService.GetString("Dialog_DeletePowerPlan_Title");
            var confirmText = _localizationService.GetString("Button_Delete");
            var cancelText = _localizationService.GetString("Button_Cancel");

            var confirmed = await _dialogService.ShowConfirmationAsync(message, title, confirmText, cancelText);
            if (!confirmed) return;

            var powerService = _domainServiceRouter.GetDomainService(ModuleId) as IPowerService;
            if (powerService == null) return;

            var success = await powerService.DeletePowerPlanAsync(planToDelete.SystemPlan.Guid);

            if (success)
            {
                await RefreshPowerPlanComboBoxAsync();
                _logService.Log(LogLevel.Info, $"Successfully deleted power plan: {displayName}");
            }
            else
            {
                var failMessage = string.Format(
                    _localizationService.GetString("Dialog_DeleteFailed_Message"),
                    displayName);
                await _dialogService.ShowInformationAsync(
                    failMessage,
                    _localizationService.GetString("Dialog_DeleteFailed_Title"));
                _logService.Log(LogLevel.Error, $"Failed to delete power plan: {displayName}");
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error deleting power plan: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                $"An error occurred while deleting the power plan: {ex.Message}",
                "Error");
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

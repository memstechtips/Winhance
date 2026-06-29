using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
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
    private readonly IPowerService _powerService;
    private ISubscriptionToken? _powerPlanChangedSubscription;

    public override string ModuleId => FeatureIds.Power;

    protected override string GetDisplayNameKey() => "Feature_Power_Name";

    public IRelayCommand<PowerPlanComboBoxOption> DeletePowerPlanCommand { get; }

    public PowerOptimizationsViewModel(
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IDialogService dialogService,
        IEventBus eventBus,
        IPowerPlanComboBoxService powerPlanComboBoxService,
        IPowerService powerService,
        IApplicationModeService applicationModeService)
        : base(settingsLoadingService, logService, localizationService, dispatcherService, eventBus, applicationModeService)
    {
        _dialogService = dialogService;
        _powerPlanComboBoxService = powerPlanComboBoxService;
        _powerService = powerService;

        DeletePowerPlanCommand = new RelayCommand<PowerPlanComboBoxOption>(async plan => await DeletePowerPlanAsync(plan));
    }

    public override async Task LoadSettingsAsync()
    {
        await base.LoadSettingsAsync();

        _powerPlanChangedSubscription?.Dispose();
        _powerPlanChangedSubscription = _eventBus.SubscribeAsync<SettingAppliedEvent>(HandleSettingAppliedAsync);
    }

    // Phase 6.7 Slice 8b-2b (D2): power-plan apply now flows through the apply funnel, which publishes the generic
    // SettingAppliedEvent (the old PowerService special handler's PowerPlanChangedEvent is retired at 8c teardown).
    // React only to the power-plan setting, so an unrelated setting apply does not trigger a power refresh. The base
    // BaseSettingsFeatureViewModel also subscribes to SettingAppliedEvent, but only to update the applied setting's own
    // VM state - this handler adds the power-plan-specific dropdown + dependent-state refresh.
    private async Task HandleSettingAppliedAsync(SettingAppliedEvent evt)
    {
        if (evt.SettingId != SettingIds.PowerPlanSelection)
            return;

        try
        {
            await Task.Delay(200).ConfigureAwait(false);
            await RefreshPowerPlanComboBoxAsync();

            // Refresh all setting states to pick up the new plan's PowerCfg values
            // (display timeout, sleep timeout, etc. differ between plans)
            await Task.Delay(500).ConfigureAwait(false);
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
                s.SettingDefinition?.Recommendation?.LoadDynamicOptions == true);

            if (powerPlanSetting == null) return;

            // Invalidate the cache to ensure we get fresh data from the OS
            _powerPlanComboBoxService.InvalidateCache();

            var options = await _powerPlanComboBoxService.GetPowerPlanOptionsAsync();
            var activePlan = await _powerService.GetActivePowerPlanAsync();

            // Phase 6.7 Slice 7b-ui-3b: runtime uses the new scheme-GUID model (Value = GUID, no index round-trip;
            // mirrors the SettingViewModelFactory gate + the GUID apply path). Builder mode stays on the OLD int-index
            // model so config-export's index-based BuilderEdit serialization (ConfigExportService, 6.8) is unchanged -
            // keep this gate in lockstep with the factory's. This refresh is reachable in builder mode (delete + an
            // external PowerPlanChangedEvent), so it MUST honour the mode too, not just the factory. The rich
            // PowerPlanComboBoxOption stays the Tag (status dot / [Active] badge / delete-by-GUID), unchanged in both.
            var isBuilderMode = _applicationModeService.CurrentMode == WinhanceMode.Builder;

            int matchedIndex = -1;
            if (activePlan != null)
            {
                for (int i = 0; i < options.Count; i++)
                {
                    if (options[i].ExistsOnSystem && options[i].SystemPlan != null &&
                        string.Equals(options[i].SystemPlan!.Guid, activePlan.Guid, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedIndex = i;
                        break;
                    }
                }
            }
            // Default to the first option when the active plan is unreadable (mirrors the old index-0 default).
            if (matchedIndex < 0 && options.Count > 0)
                matchedIndex = 0;

            object? currentSelection;
            if (isBuilderMode)
                currentSelection = matchedIndex >= 0 ? matchedIndex : 0;
            else
                currentSelection = matchedIndex >= 0
                    ? (options[matchedIndex].SystemPlan?.Guid ?? options[matchedIndex].PredefinedPlan?.Guid)
                    : null;

            // Build the new ComboBoxDisplayOption list before touching the UI
            var newItems = new List<ComboBoxDisplayOption>(options.Count);
            for (int i = 0; i < options.Count; i++)
            {
                var displayName = options[i].DisplayName;
                if (displayName.StartsWith("PowerPlan_"))
                {
                    displayName = _localizationService.GetString(displayName);
                }

                object optionValue;
                if (isBuilderMode)
                    optionValue = options[i].Index;
                else
                    optionValue = options[i].SystemPlan?.Guid ?? options[i].PredefinedPlan?.Guid ?? string.Empty;

                newItems.Add(new ComboBoxDisplayOption(
                    displayName,
                    optionValue,
                    options[i].ExistsOnSystem ? "Installed on system" : "Not installed",
                    options[i]));
            }

            // Await the UI update to ensure it completes before returning
            await _dispatcherService.RunOnUIThreadAsync(() =>
            {
                _logService.LogDebug($"[RefreshPowerPlanComboBox] Starting refresh, currentSelection={currentSelection}, current SelectedValue={powerPlanSetting.SelectedValue}");

                powerPlanSetting.ComboBoxOptions.Clear();

                foreach (var item in newItems)
                {
                    powerPlanSetting.ComboBoxOptions.Add(item);
                }

                _logService.LogDebug($"[RefreshPowerPlanComboBox] After repopulate ({newItems.Count} items), setting SelectedValue to {currentSelection}");
                powerPlanSetting.SelectedValue = currentSelection;

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

            var confirmed = (await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
            {
                Message = message,
                Title = title,
                ConfirmButtonText = confirmText,
                CancelButtonText = cancelText,
            })).Confirmed;
            if (!confirmed) return;

            var success = await _powerService.DeletePowerPlanAsync(planToDelete.SystemPlan.Guid);

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

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
        IPowerService powerService,
        IApplicationModeService applicationModeService)
        : base(settingsLoadingService, logService, localizationService, dispatcherService, eventBus, applicationModeService)
    {
        _dialogService = dialogService;
        _powerService = powerService;

        DeletePowerPlanCommand = new RelayCommand<PowerPlanComboBoxOption>(async plan => await DeletePowerPlanAsync(plan));
    }

    public override async Task LoadSettingsAsync()
    {
        // Self-heal a corrupt/ghost "Winhance Power Plan" (a scheme with the Winhance GUID but a wrong name) BEFORE the
        // base load runs detection and builds the dropdown, so a corrupt plan is never shown. Scoped to the power page's
        // interactive load - a no-op unless a corrupt plan exists - so the mutation never runs during config
        // review/export/autounattend or apply-relationship detection (which share the state provider but not this VM).
        await _powerService.CleanupCorruptWinhancePlanAsync();

        await base.LoadSettingsAsync();

        _powerPlanChangedSubscription?.Dispose();
        _powerPlanChangedSubscription = _eventBus.SubscribeAsync<SettingAppliedEvent>(HandleSettingAppliedAsync);
    }

    // Power-plan apply flows through the apply funnel, which publishes the generic
    // SettingAppliedEvent. React only to the power-plan setting, so an unrelated setting apply does not trigger
    // a power refresh. The base BaseSettingsFeatureViewModel also subscribes to SettingAppliedEvent, but only to
    // update the applied setting's own VM state - this handler adds the power-plan-specific dropdown +
    // dependent-state refresh.
    private async Task HandleSettingAppliedAsync(SettingAppliedEvent evt)
    {
        if (evt.SettingId != SettingIds.PowerPlanSelection)
            return;

        try
        {
            // Re-detect after the apply settles. RefreshSettingStatesAsync re-runs detection and feeds each setting's
            // UpdateStateFromSystemState, which for the power-plan setting rebuilds the dropdown from the fresh
            // DynamicOptions/DynamicSelection (TryApplyDynamicPowerPlanOptions) AND refreshes the dependent power
            // states (display/sleep timeouts differ per plan). The delay lets the OS report the newly-active scheme + its powercfg values.
            await Task.Delay(700).ConfigureAwait(false);
            await RefreshSettingStatesAsync();
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error handling power plan change: {ex.Message}");
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
                // Re-detect so the dropdown rebuilds from the fresh plan list (DeletePowerPlanAsync already invalidated
                // the power-plan cache, so the re-read excludes the deleted plan). No-op in Builder mode, which keeps
                // authored values until Builder exit reloads from live state - consistent with RefreshSettingStatesAsync.
                await RefreshSettingStatesAsync();
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

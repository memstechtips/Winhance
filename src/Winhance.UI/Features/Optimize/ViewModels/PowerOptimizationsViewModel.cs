using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
namespace Winhance.UI.Features.Optimize.ViewModels;

public partial class PowerOptimizationsViewModel : BaseSettingsFeatureViewModel
{
    private readonly IDialogService _dialogService;
    private readonly IPowerService _powerService;
    private ISubscriptionToken? _powerPlanChangedSubscription;
    private IDebounceTimer? _planRefreshTimer;

    public override string ModuleId => FeatureIds.Power;

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
        _powerPlanChangedSubscription = _eventBus.Subscribe<SettingAppliedEvent>(OnSettingApplied);
    }

    // Power-plan apply flows through the apply funnel, which publishes the generic
    // SettingAppliedEvent. React only to the power-plan setting, so an unrelated setting apply does not trigger
    // a power refresh. The base BaseSettingsFeatureViewModel also subscribes to SettingAppliedEvent, but only to
    // update the applied setting's own VM state - this handler adds the power-plan-specific dropdown +
    // dependent-state refresh.
    private void OnSettingApplied(SettingAppliedEvent evt)
    {
        if (evt.SettingId == "power-plan-selection")
            _dispatcherService.RunOnUIThread(ArmPlanRefreshTimer);
    }

    // UI thread. Re-detect after the apply settles. RefreshSettingStatesAsync re-runs detection and feeds each
    // setting's UpdateStateFromSystemState, which for the power-plan setting rebuilds the dropdown from the fresh
    // DynamicOptions/DynamicSelection (TryApplyKeyedOptions) AND refreshes the dependent power states (display/sleep
    // timeouts differ per plan). The wait lets the OS report the newly-active scheme + its powercfg values.
    private void ArmPlanRefreshTimer()
    {
        _planRefreshTimer ??= _dispatcherService.CreateDebounceTimer(TimeSpan.FromMilliseconds(700), () => RefreshSettingStatesAsync().FireAndForget(_logService));
        _planRefreshTimer.Restart();
    }

    [RelayCommand]
    public Task DeletePowerPlanAsync(DynamicOption? planToDelete) =>
        _dialogService.RunOrShowErrorAsync(_logService, async () =>
        {
            if (planToDelete == null) return;

            if (!planToDelete.ExistsOnSystem)
            {
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString("Dialog_CannotDeletePlan_Message"),
                    _localizationService.GetString("Dialog_CannotDeletePlan_Title"));
                return;
            }

            // Installed but undeletable means it is the active scheme, which Windows refuses to remove.
            if (!planToDelete.CanDelete)
            {
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString("Dialog_CannotDeleteActivePlan_Message"),
                    _localizationService.GetString("Dialog_CannotDeleteActivePlan_Title"));
                return;
            }

            var displayName = planToDelete.Label;

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

            var success = await _powerService.DeletePowerPlanAsync(planToDelete.Value);

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
        }, ex => _localizationService.GetString("PowerPlan_Delete_Error", ex.Message));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _powerPlanChangedSubscription?.Dispose();
            _planRefreshTimer?.Stop();
            _planRefreshTimer = null;
        }
        base.Dispose(disposing);
    }
}

using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.ViewModels;

namespace Winhance.UI.Features.Optimize.ViewModels;

public partial class PowerOptimizationsViewModel : BaseSettingsFeatureViewModel
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
        IPowerPlanComboBoxService powerPlanComboBoxService,
        MainWindowViewModel mainWindowViewModel)
        : base(domainServiceRouter, settingsLoadingService, logService, localizationService, dispatcherService, eventBus, mainWindowViewModel)
    {
        _dialogService = dialogService;
        _powerPlanComboBoxService = powerPlanComboBoxService;

        DeletePowerPlanCommand = new RelayCommand<PowerPlanComboBoxOption>(async plan => await DeletePowerPlanAsync(plan));
    }

    public override async Task LoadSettingsAsync()
    {
        await base.LoadSettingsAsync();

        _powerPlanChangedSubscription?.Dispose();
        _powerPlanChangedSubscription = _eventBus.Subscribe<PowerPlanChangedEvent>(HandlePowerPlanChanged);
    }

    private async void HandlePowerPlanChanged(PowerPlanChangedEvent evt)
    {
        try
        {
            await Task.Delay(200);
            await RefreshPowerPlanComboBoxAsync();
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

            _dispatcherService.RunOnUIThread(() =>
            {
                LogToFile($"[RefreshPowerPlanComboBox] Starting refresh, currentIndex={currentIndex}, current SelectedValue={powerPlanSetting.SelectedValue}");

                powerPlanSetting.ComboBoxOptions.Clear();
                LogToFile($"[RefreshPowerPlanComboBox] After Clear, SelectedValue={powerPlanSetting.SelectedValue}");

                for (int i = 0; i < options.Count; i++)
                {
                    var displayName = options[i].DisplayName;
                    if (displayName.StartsWith("PowerPlan_"))
                    {
                        displayName = _localizationService.GetString(displayName);
                    }

                    powerPlanSetting.ComboBoxOptions.Add(new ComboBoxOption
                    {
                        DisplayText = displayName,
                        Value = options[i].Index,
                        Description = options[i].ExistsOnSystem ? "Installed on system" : "Not installed",
                        Tag = options[i]
                    });
                }

                LogToFile($"[RefreshPowerPlanComboBox] After repopulate, SelectedValue={powerPlanSetting.SelectedValue}, setting to {currentIndex}");
                powerPlanSetting.SelectedValue = currentIndex;
                LogToFile($"[RefreshPowerPlanComboBox] After setting SelectedValue={powerPlanSetting.SelectedValue}");
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

    private static void LogToFile(string message)
    {
        try
        {
            var logPath = @"C:\Winhance-UI\src\startup-debug.log";
            System.IO.File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch { }
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

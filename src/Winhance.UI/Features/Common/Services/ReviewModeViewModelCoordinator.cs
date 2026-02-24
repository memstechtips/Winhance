using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Customize.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// Coordinates ViewModel interactions for review mode operations.
/// Wraps the concrete singleton ViewModels so that services like
/// <see cref="ConfigReviewOrchestrationService"/> and
/// <see cref="ConfigApplicationExecutionService"/> can be unit-tested
/// without depending on concrete ViewModel types.
/// </summary>
public class ReviewModeViewModelCoordinator : IReviewModeViewModelCoordinator
{
    private readonly SoftwareAppsViewModel _softwareAppsVM;
    private readonly WindowsAppsViewModel _windowsAppsVM;
    private readonly ExternalAppsViewModel _externalAppsVM;
    private readonly OptimizeViewModel _optimizeVM;
    private readonly CustomizeViewModel _customizeVM;
    private readonly ISettingReviewDiffApplier _reviewDiffApplier;

    public ReviewModeViewModelCoordinator(
        SoftwareAppsViewModel softwareAppsVM,
        WindowsAppsViewModel windowsAppsVM,
        ExternalAppsViewModel externalAppsVM,
        OptimizeViewModel optimizeVM,
        CustomizeViewModel customizeVM,
        ISettingReviewDiffApplier reviewDiffApplier)
    {
        _softwareAppsVM = softwareAppsVM;
        _windowsAppsVM = windowsAppsVM;
        _externalAppsVM = externalAppsVM;
        _optimizeVM = optimizeVM;
        _customizeVM = customizeVM;
        _reviewDiffApplier = reviewDiffApplier;
    }

    public bool HasSelectedWindowsApps =>
        _windowsAppsVM.Items?.Any(a => a.IsSelected) == true;

    public bool HasSelectedExternalApps =>
        _externalAppsVM.Items?.Any(a => a.IsSelected) == true;

    public bool IsWindowsAppsInstallAction => _softwareAppsVM.IsWindowsAppsInstallAction;
    public bool IsWindowsAppsRemoveAction => _softwareAppsVM.IsWindowsAppsRemoveAction;
    public bool IsExternalAppsInstallAction => _softwareAppsVM.IsExternalAppsInstallAction;
    public bool IsExternalAppsRemoveAction => _softwareAppsVM.IsExternalAppsRemoveAction;

    public List<string> GetSelectedExternalAppIds()
    {
        if (_externalAppsVM.Items == null)
            return new List<string>();

        return _externalAppsVM.Items
            .Where(a => a.IsSelected)
            .Select(a => a.Id ?? a.Name)
            .ToList();
    }

    public void ClearExternalAppSelections()
    {
        foreach (var item in _externalAppsVM.Items)
            item.IsSelected = false;
    }

    public void ReapplyReviewDiffsToExistingSettings()
    {
        void ReapplyToFeature(ISettingsFeatureViewModel featureVm)
        {
            foreach (var setting in featureVm.Settings)
            {
                // Clear any stale review state first
                setting.ClearReviewState();
                // Build currentState from the VM's actual displayed values
                // so the fallback ComputeDiff sees accurate state, not defaults
                var currentState = new SettingStateResult
                {
                    IsEnabled = setting.IsSelected,
                    CurrentValue = setting.SelectedValue
                };
                // Re-apply the new diff
                _reviewDiffApplier.ApplyReviewDiffToViewModel(setting, currentState);
            }
        }

        ReapplyToFeature(_optimizeVM.SoundViewModel);
        ReapplyToFeature(_optimizeVM.UpdateViewModel);
        ReapplyToFeature(_optimizeVM.NotificationViewModel);
        ReapplyToFeature(_optimizeVM.PrivacyViewModel);
        ReapplyToFeature(_optimizeVM.PowerViewModel);
        ReapplyToFeature(_optimizeVM.GamingViewModel);

        ReapplyToFeature(_customizeVM.ExplorerViewModel);
        ReapplyToFeature(_customizeVM.StartMenuViewModel);
        ReapplyToFeature(_customizeVM.TaskbarViewModel);
        ReapplyToFeature(_customizeVM.WindowsThemeViewModel);
    }

    public Task RemoveWindowsAppsAsync(bool skipConfirmation, bool saveRemovalScripts)
    {
        return _windowsAppsVM.RemoveApps(skipConfirmation: skipConfirmation, saveRemovalScripts: saveRemovalScripts);
    }

    public Task InstallWindowsAppsAsync()
    {
        return _windowsAppsVM.InstallAppsAsync();
    }
}

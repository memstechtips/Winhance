using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Customize.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.UI.Features.SoftwareApps.ViewModels;

namespace Winhance.UI.Features.Common.Services;

public class ReviewModeViewModelCoordinator : IReviewModeViewModelCoordinator
{
    private readonly SoftwareAppsViewModel _softwareAppsVM;
    private readonly WindowsAppsViewModel _windowsAppsVM;
    private readonly ExternalAppsViewModel _externalAppsVM;
    private readonly OptimizeViewModel _optimizeVM;
    private readonly CustomizeViewModel _customizeVM;
    private readonly ISettingReviewDiffApplier _reviewDiffApplier;
    private readonly IConfigReviewDiffService _configReviewDiffService;
    private readonly IDispatcherService _dispatcherService;

    public ReviewModeViewModelCoordinator(
        SoftwareAppsViewModel softwareAppsVM,
        WindowsAppsViewModel windowsAppsVM,
        ExternalAppsViewModel externalAppsVM,
        OptimizeViewModel optimizeVM,
        CustomizeViewModel customizeVM,
        ISettingReviewDiffApplier reviewDiffApplier,
        IConfigReviewDiffService configReviewDiffService,
        IDispatcherService dispatcherService)
    {
        _softwareAppsVM = softwareAppsVM;
        _windowsAppsVM = windowsAppsVM;
        _externalAppsVM = externalAppsVM;
        _optimizeVM = optimizeVM;
        _customizeVM = customizeVM;
        _reviewDiffApplier = reviewDiffApplier;
        _configReviewDiffService = configReviewDiffService;
        _dispatcherService = dispatcherService;
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

    public void ReapplyReviewDiffsToExistingSettings()
    {
        // Must run on UI thread because this modifies ObservableProperty values
        // on SettingItemViewModels that are bound to the UI. The ReviewModeChanged
        // event can fire from a background thread due to ConfigureAwait(false) in
        // ConfigReviewService.ComputeEagerDiffsAsync.
        _dispatcherService.RunOnUIThread(() =>
        {
            void ReapplyToFeature(ISettingsFeatureViewModel featureVm)
            {
                foreach (var setting in featureVm.Settings)
                {
                    setting.ClearReviewState();
                    // Build currentState from the VM's actual displayed values
                    // so the fallback ComputeDiff sees accurate state, not defaults
                    var currentState = new SettingStateResult
                    {
                        IsEnabled = setting.IsSelected,
                        CurrentValue = setting.SelectedValue
                    };
                    _reviewDiffApplier.ApplyReviewDiffToViewModel(setting, currentState);
                }
            }

            foreach (var featureVm in FeatureViewModels())
                ReapplyToFeature(featureVm);
        });
    }

    // Windows apps default to Remove and external apps to Install, the same defaults Import applies without review.
    public void AcceptAllRemaining()
    {
        _dispatcherService.RunOnUIThread(() =>
        {
            var accepted = _configReviewDiffService.ApproveAllPending().ToHashSet();
            foreach (var setting in FeatureViewModels().SelectMany(f => f.Settings).Where(s => accepted.Contains(s.SettingId)))
            {
                if (setting.HasReviewDiff && !setting.IsReviewDecisionMade)
                    setting.IsReviewApproved = true;
                if (setting.HasReviewAction && !setting.IsReviewActionApproved && !setting.IsReviewActionRejected)
                    setting.IsReviewActionApproved = true;
            }

            if (HasSelectedWindowsApps && !_softwareAppsVM.IsWindowsAppsActionChosen)
                _softwareAppsVM.IsWindowsAppsRemoveAction = true;
            if (HasSelectedExternalApps && !_softwareAppsVM.IsExternalAppsActionChosen)
                _softwareAppsVM.IsExternalAppsInstallAction = true;
        });
    }

    private IEnumerable<ISettingsFeatureViewModel> FeatureViewModels() =>
        OptimizeViewModel.Sections.Select(s => _optimizeVM.GetSectionViewModel(s.Key))
            .Concat(CustomizeViewModel.Sections.Select(s => _customizeVM.GetSectionViewModel(s.Key)))
            .OfType<ISettingsFeatureViewModel>();

    public Task RemoveWindowsAppsAsync(bool skipConfirmation, bool saveRemovalScripts)
    {
        return _windowsAppsVM.RemoveApps(skipConfirmation: skipConfirmation, saveRemovalScripts: saveRemovalScripts);
    }

    public Task InstallWindowsAppsAsync()
    {
        return _windowsAppsVM.InstallAppsAsync();
    }
}

using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.ViewModels;

namespace Winhance.UI.Features.Optimize.ViewModels;

/// <summary>
/// ViewModel for Sound optimization settings.
/// </summary>
public partial class SoundOptimizationsViewModel : BaseSettingsFeatureViewModel
{
    public override string ModuleId => FeatureIds.Sound;

    protected override string GetDisplayNameKey() => "Feature_Sound_Name";

    public SoundOptimizationsViewModel(
        IDomainServiceRouter domainServiceRouter,
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        MainWindowViewModel mainWindowViewModel)
        : base(domainServiceRouter, settingsLoadingService, logService, localizationService, dispatcherService, mainWindowViewModel)
    {
    }
}

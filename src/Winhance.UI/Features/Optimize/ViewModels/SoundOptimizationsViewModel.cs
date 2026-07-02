using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.Interfaces;
namespace Winhance.UI.Features.Optimize.ViewModels;

public partial class SoundOptimizationsViewModel : BaseSettingsFeatureViewModel, IOptimizationFeatureViewModel
{
    public override string ModuleId => FeatureIds.Sound;

    protected override string GetDisplayNameKey() => "Feature_Sound_Name";

    public SoundOptimizationsViewModel(
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IEventBus eventBus,
        IApplicationModeService applicationModeService)
        : base(settingsLoadingService, logService, localizationService, dispatcherService, eventBus, applicationModeService)
    {
    }
}

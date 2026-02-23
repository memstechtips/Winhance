using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.Interfaces;
using Winhance.UI.ViewModels;

namespace Winhance.UI.Features.Optimize.ViewModels;

public partial class GamingOptimizationsViewModel : BaseSettingsFeatureViewModel, IOptimizationFeatureViewModel
{
    public override string ModuleId => FeatureIds.GamingPerformance;

    protected override string GetDisplayNameKey() => "Feature_GamingPerformance_Name";

    public GamingOptimizationsViewModel(
        IDomainServiceRouter domainServiceRouter,
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IEventBus eventBus,
        MainWindowViewModel mainWindowViewModel)
        : base(domainServiceRouter, settingsLoadingService, logService, localizationService, dispatcherService, eventBus, mainWindowViewModel)
    {
    }
}

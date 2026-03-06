using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.Interfaces;
namespace Winhance.UI.Features.Optimize.ViewModels;

public partial class UpdateOptimizationsViewModel : BaseSettingsFeatureViewModel, IOptimizationFeatureViewModel
{
    public override string ModuleId => FeatureIds.Update;

    protected override string GetDisplayNameKey() => "Feature_Update_Name";

    public UpdateOptimizationsViewModel(
        IDomainServiceRouter domainServiceRouter,
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IEventBus eventBus)
        : base(domainServiceRouter, settingsLoadingService, logService, localizationService, dispatcherService, eventBus)
    {
    }
}

using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Customize.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;
using ISettingsLoadingService = Winhance.UI.Features.Common.Interfaces.ISettingsLoadingService;

namespace Winhance.UI.Features.Customize.ViewModels;

public partial class StartMenuCustomizationsViewModel : BaseSettingsFeatureViewModel, ICustomizationFeatureViewModel
{
    public StartMenuCustomizationsViewModel(
        IDomainServiceRouter domainServiceRouter,
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IEventBus eventBus)
        : base(domainServiceRouter, settingsLoadingService, logService, localizationService, dispatcherService, eventBus)
    {
    }

    public override string ModuleId => FeatureIds.StartMenu;

    protected override string GetDisplayNameKey() => "Feature_StartMenu_Name";
}

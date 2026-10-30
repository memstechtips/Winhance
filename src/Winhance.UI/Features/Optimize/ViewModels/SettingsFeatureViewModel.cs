using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using ISettingsLoadingService = Winhance.UI.Features.Common.Interfaces.ISettingsLoadingService;

namespace Winhance.UI.Features.Optimize.ViewModels;

public partial class SettingsFeatureViewModel(
    string moduleId,
    ISettingsLoadingService settingsLoadingService,
    ILogService logService,
    ILocalizationService localizationService,
    IDispatcherService dispatcherService,
    IEventBus eventBus,
    IApplicationModeService applicationModeService)
    : BaseSettingsFeatureViewModel(settingsLoadingService, logService, localizationService, dispatcherService, eventBus, applicationModeService)
{
    public override string ModuleId { get; } = moduleId;
}

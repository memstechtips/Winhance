using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.Interfaces;
using Winhance.UI.ViewModels;

namespace Winhance.UI.Features.Optimize.ViewModels;

public partial class NotificationOptimizationsViewModel : BaseSettingsFeatureViewModel, IOptimizationFeatureViewModel
{
    public override string ModuleId => FeatureIds.Notifications;

    protected override string GetDisplayNameKey() => "Feature_Notifications_Name";

    public NotificationOptimizationsViewModel(
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

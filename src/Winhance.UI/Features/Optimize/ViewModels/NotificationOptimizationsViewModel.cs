using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Optimize.ViewModels;

/// <summary>
/// ViewModel for Notification optimization settings.
/// </summary>
public partial class NotificationOptimizationsViewModel : BaseSettingsFeatureViewModel
{
    public override string ModuleId => FeatureIds.Notifications;

    protected override string GetDisplayNameKey() => "Feature_Notifications_Name";

    public NotificationOptimizationsViewModel(
        IDomainServiceRouter domainServiceRouter,
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService)
        : base(domainServiceRouter, settingsLoadingService, logService, localizationService)
    {
    }
}

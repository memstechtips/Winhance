using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;
using ISettingsLoadingService = Winhance.UI.Features.Common.Interfaces.ISettingsLoadingService;

namespace Winhance.UI.Features.Customize.ViewModels;

/// <summary>
/// ViewModel for Taskbar customization settings.
/// </summary>
public partial class TaskbarCustomizationsViewModel : BaseSettingsFeatureViewModel
{
    public TaskbarCustomizationsViewModel(
        IDomainServiceRouter domainServiceRouter,
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService)
        : base(domainServiceRouter, settingsLoadingService, logService, localizationService)
    {
    }

    public override string ModuleId => FeatureIds.Taskbar;

    protected override string GetDisplayNameKey() => "Feature_Taskbar_Name";
}

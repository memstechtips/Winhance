using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.ViewModels;

namespace Winhance.UI.Features.Optimize.ViewModels;

/// <summary>
/// ViewModel for Update optimization settings.
/// </summary>
public partial class UpdateOptimizationsViewModel : BaseSettingsFeatureViewModel
{
    public override string ModuleId => FeatureIds.Update;

    protected override string GetDisplayNameKey() => "Feature_Update_Name";

    public UpdateOptimizationsViewModel(
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

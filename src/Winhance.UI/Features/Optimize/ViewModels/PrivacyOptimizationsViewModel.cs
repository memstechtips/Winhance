using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Optimize.ViewModels;

/// <summary>
/// ViewModel for Privacy optimization settings.
/// </summary>
public partial class PrivacyOptimizationsViewModel : BaseSettingsFeatureViewModel
{
    public override string ModuleId => FeatureIds.Privacy;

    protected override string GetDisplayNameKey() => "Feature_Privacy_Name";

    public PrivacyOptimizationsViewModel(
        IDomainServiceRouter domainServiceRouter,
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService)
        : base(domainServiceRouter, settingsLoadingService, logService, localizationService)
    {
    }
}

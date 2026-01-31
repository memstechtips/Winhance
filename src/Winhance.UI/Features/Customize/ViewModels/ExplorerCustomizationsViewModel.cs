using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;
using ISettingsLoadingService = Winhance.UI.Features.Common.Interfaces.ISettingsLoadingService;

namespace Winhance.UI.Features.Customize.ViewModels;

/// <summary>
/// ViewModel for Explorer customization settings.
/// </summary>
public partial class ExplorerCustomizationsViewModel : BaseSettingsFeatureViewModel
{
    public ExplorerCustomizationsViewModel(
        IDomainServiceRouter domainServiceRouter,
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService)
        : base(domainServiceRouter, settingsLoadingService, logService, localizationService)
    {
    }

    public override string ModuleId => FeatureIds.ExplorerCustomization;

    protected override string GetDisplayNameKey() => "Feature_Explorer_Name";
}

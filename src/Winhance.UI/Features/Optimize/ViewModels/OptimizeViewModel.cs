using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.ViewModels;

namespace Winhance.UI.Features.Optimize.ViewModels;

public partial class OptimizeViewModel : SectionPageViewModel<SectionInfo>
{
    protected override string PageTitleKey => "Category_Optimize_Title";
    protected override string PageDescriptionKey => "Category_Optimize_StatusText";
    protected override string BreadcrumbRootFallback => "Optimizations";
    protected override string LogPrefix => "OptimizeViewModel";
    protected override IReadOnlyList<SectionInfo> SectionDefinitions => Sections;

    public static readonly IReadOnlyList<SectionInfo> Sections = new List<SectionInfo>()
    {
        new("Privacy", FeatureIds.Privacy),
        new("Power", FeatureIds.Power),
        new("Gaming", FeatureIds.GamingPerformance),
        new("Update", FeatureIds.Update),
        new("Notification", FeatureIds.Notifications),
        new("Sound", FeatureIds.Sound),
    };

    public OptimizeViewModel(
        ILogService logService,
        ILocalizationService localizationService,
        IEnumerable<ISettingsFeatureViewModel> featureViewModels,
        IConfigReviewBadgeService badgeService,
        IConfigReviewModeService reviewModeService,
        ICatalogSettingsRegistry registry,
        ICatalogScopeProvider scope,
        IApplicationModeService modeService)
        : base(logService, localizationService, featureViewModels.Where(vm => Sections.Any(s => s.ModuleId == vm.ModuleId)),
               badgeService, reviewModeService, registry, scope, modeService)
    {
        InitializeSectionMappings();
    }
}

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

    // Per-feature handles for ReviewModeViewModelCoordinator, which re-applies review state feature by feature.
    public ISettingsFeatureViewModel SoundViewModel { get; }
    public ISettingsFeatureViewModel UpdateViewModel { get; }
    public ISettingsFeatureViewModel NotificationViewModel { get; }
    public ISettingsFeatureViewModel PrivacyViewModel { get; }
    public ISettingsFeatureViewModel PowerViewModel { get; }
    public ISettingsFeatureViewModel GamingViewModel { get; }

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

        SoundViewModel = GetFeatureByModuleId(FeatureIds.Sound);
        UpdateViewModel = GetFeatureByModuleId(FeatureIds.Update);
        NotificationViewModel = GetFeatureByModuleId(FeatureIds.Notifications);
        PrivacyViewModel = GetFeatureByModuleId(FeatureIds.Privacy);
        PowerViewModel = GetFeatureByModuleId(FeatureIds.Power);
        GamingViewModel = GetFeatureByModuleId(FeatureIds.GamingPerformance);
    }
}

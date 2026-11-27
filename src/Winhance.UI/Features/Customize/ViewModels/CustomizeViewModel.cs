using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.ViewModels;

namespace Winhance.UI.Features.Customize.ViewModels;

public partial class CustomizeViewModel : SectionPageViewModel<SectionInfo>
{
    protected override string PageTitleKey => "Category_Customize_Title";
    protected override string PageDescriptionKey => "Category_Customize_StatusText";
    protected override string BreadcrumbRootFallback => "Customizations";
    protected override string LogPrefix => "CustomizeViewModel";
    protected override IReadOnlyList<SectionInfo> SectionDefinitions => Sections;

    // This list is the display order: the overview cards and the breadcrumb flyout both render it
    // as written. The order is the one the page has always shipped with and is a product decision, not
    // alphabetical and not derivable — so do not "tidy" it. Sections_AreInTheOrderTheUserSees pins it.
    public static readonly IReadOnlyList<SectionInfo> Sections = new List<SectionInfo>()
    {
        new("WindowsTheme", FeatureIds.WindowsTheme),
        new("Taskbar", FeatureIds.Taskbar),
        new("StartMenu", FeatureIds.StartMenu),
        new("Explorer", FeatureIds.Explorer),
        new("ContextMenu", FeatureIds.ContextMenu),
        new("TimeRegionLanguage", FeatureIds.TimeRegionLanguage),
    };

    public CustomizeViewModel(
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

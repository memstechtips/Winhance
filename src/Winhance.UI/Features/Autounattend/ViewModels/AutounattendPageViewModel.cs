using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Autounattend.ViewModels;

// One section, which AutounattendSettingsView draws in the shell's content slot.
public sealed class AutounattendPageViewModel : SectionPageViewModel<SectionInfo>
{
    private static readonly IReadOnlyList<SectionInfo> Sections = [new("Autounattend", FeatureIds.Autounattend)];

    private readonly AutounattendViewModel _autounattend;

    protected override string PageTitleKey => "Autounattend_Page_Title";
    protected override string PageDescriptionKey => "Autounattend_Page_Description";
    protected override string BreadcrumbRootFallback => "Autounattend.xml Settings";
    protected override string LogPrefix => "AutounattendPageViewModel";
    protected override IReadOnlyList<SectionInfo> SectionDefinitions => Sections;

    public AutounattendPageViewModel(
        ILogService logService,
        ILocalizationService localizationService,
        AutounattendViewModel autounattend,
        IConfigReviewBadgeService badgeService,
        IConfigReviewModeService reviewModeService,
        ICatalogSettingsRegistry registry,
        ICatalogScopeProvider scope,
        IApplicationModeService modeService)
        : base(logService, localizationService, [autounattend], badgeService, reviewModeService, registry, scope, modeService)
    {
        _autounattend = autounattend;
        InitializeSectionMappings();
    }

    // The view loads the cards, on every visit and in both of its hosts. A load started here as well would hold
    // the feature's load lock, and the view's own load would return before the cards exist.
    public override Task InitializeAsync()
    {
        IsLoading = false;
        return Task.CompletedTask;
    }

    public override bool TakesQuickActions(SettingItemViewModel setting) => _autounattend.TakesQuickActions(setting);

    protected override ISettingsFeatureViewModel SectionOnScreen => _autounattend;
}

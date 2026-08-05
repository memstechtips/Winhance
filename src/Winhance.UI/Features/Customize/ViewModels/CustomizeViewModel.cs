using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.Customize.Interfaces;
using Winhance.UI.Features.Customize.Models;

namespace Winhance.UI.Features.Customize.ViewModels;

/// <summary>
/// ViewModel for the Customize page, coordinating all customization feature ViewModels.
/// </summary>
public partial class CustomizeViewModel : SectionPageViewModel<CustomizeSectionInfo>
{
    protected override string PageTitleKey => "Category_Customize_Title";
    protected override string PageDescriptionKey => "Category_Customize_StatusText";
    protected override string BreadcrumbRootFallback => "Customizations";
    protected override string LogPrefix => "CustomizeViewModel";
    protected override IReadOnlyList<CustomizeSectionInfo> SectionDefinitions => Sections;

    /// <summary>
    /// Section definitions for navigation, in the order they are presented to the user.
    /// </summary>
    // This list is the display order: the overview cards and the breadcrumb flyout both render it
    // as written. The order is the one the page has always shipped with and is Marco's call, not
    // alphabetical and not derivable — so do not "tidy" it. Sections_AreInTheOrderTheUserSees pins it.
    //
    // Icon keys are the PathIcon resources the overview cards and breadcrumb resolve. They used to
    // be the "…IconGlyph" font-glyph keys, which no surface ever rendered — CustomizePage.xaml.cs
    // kept its own second table of "…IconPath" keys for the icons actually shown, and the two were
    // free to disagree. One table now.
    public static readonly IReadOnlyList<CustomizeSectionInfo> Sections = new List<CustomizeSectionInfo>()
    {
        new("WindowsTheme", "WindowsThemeIconPath", "Windows Theme", FeatureIds.WindowsTheme),
        new("Taskbar", "TaskbarIconPath", "Taskbar", FeatureIds.Taskbar),
        new("StartMenu", "StartMenuIconPath", "Start Menu", FeatureIds.StartMenu),
        new("Explorer", "ExplorerIconPath", "Explorer", FeatureIds.ExplorerCustomization),
    };

    // Named properties for XAML binding (typed as interface, not concrete)
    public ISettingsFeatureViewModel ExplorerViewModel { get; }
    public ISettingsFeatureViewModel StartMenuViewModel { get; }
    public ISettingsFeatureViewModel TaskbarViewModel { get; }
    public ISettingsFeatureViewModel WindowsThemeViewModel { get; }

    public CustomizeViewModel(
        ILogService logService,
        ILocalizationService localizationService,
        IEnumerable<ICustomizationFeatureViewModel> featureViewModels,
        IConfigReviewBadgeService badgeService,
        IConfigReviewModeService reviewModeService)
        : base(logService, localizationService, featureViewModels.Cast<ISettingsFeatureViewModel>(),
               badgeService, reviewModeService)
    {
        InitializeSectionMappings();

        ExplorerViewModel = GetFeatureByModuleId(FeatureIds.ExplorerCustomization);
        StartMenuViewModel = GetFeatureByModuleId(FeatureIds.StartMenu);
        TaskbarViewModel = GetFeatureByModuleId(FeatureIds.Taskbar);
        WindowsThemeViewModel = GetFeatureByModuleId(FeatureIds.WindowsTheme);
    }
}

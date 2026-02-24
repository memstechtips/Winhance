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
    /// Section definitions for navigation.
    /// </summary>
    public static readonly List<CustomizeSectionInfo> Sections = new()
    {
        new("Explorer", "ExplorerIconGlyph", "Explorer", FeatureIds.ExplorerCustomization),
        new("StartMenu", "StartMenuIconGlyph", "Start Menu", FeatureIds.StartMenu),
        new("Taskbar", "TaskbarIconGlyph", "Taskbar", FeatureIds.Taskbar),
        new("WindowsTheme", "WindowsThemeIconGlyph", "Windows Theme", FeatureIds.WindowsTheme),
    };

    // Named properties for XAML binding (typed as interface, not concrete)
    public ISettingsFeatureViewModel ExplorerViewModel { get; }
    public ISettingsFeatureViewModel StartMenuViewModel { get; }
    public ISettingsFeatureViewModel TaskbarViewModel { get; }
    public ISettingsFeatureViewModel WindowsThemeViewModel { get; }

    public CustomizeViewModel(
        ILogService logService,
        ILocalizationService localizationService,
        IEnumerable<ICustomizationFeatureViewModel> featureViewModels)
        : base(logService, localizationService, featureViewModels.Cast<ISettingsFeatureViewModel>())
    {
        InitializeSectionMappings();

        ExplorerViewModel = GetFeatureByModuleId(FeatureIds.ExplorerCustomization);
        StartMenuViewModel = GetFeatureByModuleId(FeatureIds.StartMenu);
        TaskbarViewModel = GetFeatureByModuleId(FeatureIds.Taskbar);
        WindowsThemeViewModel = GetFeatureByModuleId(FeatureIds.WindowsTheme);
    }
}

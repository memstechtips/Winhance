using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.Optimize.Interfaces;
using Winhance.UI.Features.Optimize.Models;

namespace Winhance.UI.Features.Optimize.ViewModels;

/// <summary>
/// ViewModel for the Optimize page, coordinating all optimization feature ViewModels.
/// </summary>
public partial class OptimizeViewModel : SectionPageViewModel<OptimizeSectionInfo>
{
    protected override string PageTitleKey => "Category_Optimize_Title";
    protected override string PageDescriptionKey => "Category_Optimize_StatusText";
    protected override string BreadcrumbRootFallback => "Optimizations";
    protected override string LogPrefix => "OptimizeViewModel";
    protected override IReadOnlyList<OptimizeSectionInfo> SectionDefinitions => Sections;

    /// <summary>
    /// Section definitions for navigation.
    /// </summary>
    public static readonly IReadOnlyList<OptimizeSectionInfo> Sections = new List<OptimizeSectionInfo>()
    {
        new("Privacy", "PrivacyIconPath", "Privacy & Security", FeatureIds.Privacy),
        new("Power", "PowerIconPath", "Power", FeatureIds.Power),
        new("Gaming", "GamingIconPath", "Gaming and Performance", FeatureIds.GamingPerformance),
        new("Update", "UpdateIconSymbol", "Updates", FeatureIds.Update),
        new("Notification", "NotificationIconPath", "Notifications", FeatureIds.Notifications),
        new("Sound", "SoundIconSymbol", "Sound", FeatureIds.Sound),
    };

    // Named properties for XAML binding (typed as interface, not concrete)
    public ISettingsFeatureViewModel SoundViewModel { get; }
    public ISettingsFeatureViewModel UpdateViewModel { get; }
    public ISettingsFeatureViewModel NotificationViewModel { get; }
    public ISettingsFeatureViewModel PrivacyViewModel { get; }
    public ISettingsFeatureViewModel PowerViewModel { get; }
    public ISettingsFeatureViewModel GamingViewModel { get; }

    public OptimizeViewModel(
        ILogService logService,
        ILocalizationService localizationService,
        IEnumerable<IOptimizationFeatureViewModel> featureViewModels)
        : base(logService, localizationService, featureViewModels.Cast<ISettingsFeatureViewModel>())
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

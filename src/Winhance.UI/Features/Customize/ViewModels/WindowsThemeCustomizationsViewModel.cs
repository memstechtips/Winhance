using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.UI.ViewModels;
using ISettingsLoadingService = Winhance.UI.Features.Common.Interfaces.ISettingsLoadingService;

namespace Winhance.UI.Features.Customize.ViewModels;

/// <summary>
/// ViewModel for Windows Theme customization settings.
/// </summary>
public partial class WindowsThemeCustomizationsViewModel : BaseSettingsFeatureViewModel
{
    public WindowsThemeCustomizationsViewModel(
        IDomainServiceRouter domainServiceRouter,
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        MainWindowViewModel mainWindowViewModel)
        : base(domainServiceRouter, settingsLoadingService, logService, localizationService, dispatcherService, mainWindowViewModel)
    {
    }

    public override string ModuleId => FeatureIds.WindowsTheme;

    protected override string GetDisplayNameKey() => "Feature_WindowsTheme_Name";
}

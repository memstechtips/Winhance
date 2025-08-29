using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    public partial class WindowsThemeCustomizationsViewModel(
      IDomainServiceRouter domainServiceRouter,
      ISettingsLoadingService settingsLoadingService,
      ILogService logService)
      : BaseSettingsFeatureViewModel(domainServiceRouter, settingsLoadingService, logService)
    {
        public override string ModuleId => FeatureIds.WindowsTheme;
        public override string DisplayName => "Windows Theme";
        public override string Category => "Customize";
        public override string Description => "Customize Windows theme settings";
        public override int SortOrder => 1;
    }
}

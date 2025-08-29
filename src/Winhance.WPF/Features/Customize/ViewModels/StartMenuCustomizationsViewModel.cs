using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    public partial class StartMenuCustomizationsViewModel(
      IDomainServiceRouter domainServiceRouter,
      ISettingsLoadingService settingsLoadingService,
      ILogService logService)
      : BaseSettingsFeatureViewModel(domainServiceRouter, settingsLoadingService, logService)
    {
        public override string ModuleId => FeatureIds.StartMenu;
        public override string DisplayName => "Start Menu";
        public override string Category => "Customize";
        public override string Description => "Customize Windows Start Menu settings";
        public override int SortOrder => 3;
    }
}

using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    public partial class ExplorerOptimizationsViewModel(
      IDomainServiceRouter domainServiceRouter,
      ISettingsLoadingService settingsLoadingService,
      ILogService logService)
      : BaseSettingsFeatureViewModel(domainServiceRouter, settingsLoadingService, logService)
    {
        public override string ModuleId => FeatureIds.ExplorerOptimization;
        public override string DisplayName => "Explorer";
        public override string Category => "Optimize";
        public override string Description => "Optimize Windows Explorer settings";
        public override int SortOrder => 6;
    }
}

using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    public partial class GamingandPerformanceOptimizationsViewModel(
      IDomainServiceRouter domainServiceRouter,
      ISettingsLoadingService settingsLoadingService,
      ILogService logService)
      : BaseSettingsFeatureViewModel(domainServiceRouter, settingsLoadingService, logService)
    {
        public override string ModuleId => FeatureIds.GamingPerformance;
        public override string DisplayName => "Gaming and Performance";
        public override string Category => "Optimize";
        public override string Description => "Optimize Windows for gaming and performance";
        public override int SortOrder => 1;
    }
}

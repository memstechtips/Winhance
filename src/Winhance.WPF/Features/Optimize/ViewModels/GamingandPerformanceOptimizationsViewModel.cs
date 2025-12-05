using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    public partial class GamingandPerformanceOptimizationsViewModel(
      IDomainServiceRouter domainServiceRouter,
      ISettingsLoadingService settingsLoadingService,
      ILogService logService,
      ILocalizationService localizationService)
      : BaseSettingsFeatureViewModel(domainServiceRouter, settingsLoadingService, logService, localizationService)
    {
        public override string ModuleId => FeatureIds.GamingPerformance;

        protected override string GetDisplayNameKey() => "Feature_GamingPerformance_Name";
    }
}

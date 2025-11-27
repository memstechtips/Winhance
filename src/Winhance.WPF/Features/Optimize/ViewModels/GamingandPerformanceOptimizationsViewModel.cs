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
        public override string DisplayName => "Gaming & Performance";
        
    }
}

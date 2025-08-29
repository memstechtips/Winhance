using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    public partial class WindowsSecurityOptimizationsViewModel(
      IDomainServiceRouter domainServiceRouter,
      ISettingsLoadingService settingsLoadingService,
      ILogService logService)
      : BaseSettingsFeatureViewModel(domainServiceRouter, settingsLoadingService, logService)
    {
        public override string ModuleId => FeatureIds.Security;
        public override string DisplayName => "Windows Security";
        public override string Category => "Optimize";
        public override string Description => "Optimize Windows security settings";
        public override int SortOrder => 5;
    }
}

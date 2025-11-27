using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    public partial class UpdateOptimizationsViewModel(
        IDomainServiceRouter domainServiceRouter,
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService)
        : BaseSettingsFeatureViewModel(domainServiceRouter, settingsLoadingService, logService, localizationService)
    {
        public override string ModuleId => FeatureIds.Update;
        public override string DisplayName => "Update";
    }
}

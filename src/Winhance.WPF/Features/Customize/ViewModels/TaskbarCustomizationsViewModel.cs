using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    public partial class TaskbarCustomizationsViewModel(
      IDomainServiceRouter domainServiceRouter,
      ISettingsLoadingService settingsLoadingService,
      ILogService logService,
      ILocalizationService localizationService)
      : BaseSettingsFeatureViewModel(domainServiceRouter, settingsLoadingService, logService, localizationService)
    {
        public override string ModuleId => FeatureIds.Taskbar;

        protected override string GetDisplayNameKey() => "Feature_Taskbar_Name";
    }
}

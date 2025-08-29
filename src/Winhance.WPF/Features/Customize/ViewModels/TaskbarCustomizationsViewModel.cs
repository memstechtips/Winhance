using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    public partial class TaskbarCustomizationsViewModel(
      IDomainServiceRouter domainServiceRouter,
      ISettingsLoadingService settingsLoadingService,
      ILogService logService)
      : BaseSettingsFeatureViewModel(domainServiceRouter, settingsLoadingService, logService)
    {
        public override string ModuleId => FeatureIds.Taskbar;
        public override string DisplayName => "Taskbar";
        public override string Category => "Customize";
        public override string Description => "Customize Windows Taskbar settings";
        public override int SortOrder => 2;
    }
}

using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Infrastructure.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Customize.Descriptors
{
    /// <summary>
    /// Feature descriptor for Taskbar customizations.
    /// </summary>
    public class TaskbarFeatureDescriptor : BaseFeatureDescriptor
    {
        public TaskbarFeatureDescriptor() 
            : base(
                moduleId: "taskbar",
                displayName: "Taskbar",
                category: "Customization",
                sortOrder: 3,
                domainServiceType: typeof(ITaskbarService),
                description: "Customize Taskbar appearance, behavior, and system tray")
        {
        }
    }
}

using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Infrastructure.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Optimize.Descriptors
{
    /// <summary>
    /// Feature descriptor for Notification optimizations.
    /// </summary>
    public class NotificationFeatureDescriptor : BaseFeatureDescriptor
    {
        public NotificationFeatureDescriptor() 
            : base(
                moduleId: "notifications",
                displayName: "Notifications",
                category: "Optimization",
                sortOrder: 7,
                domainServiceType: typeof(INotificationService),
                description: "Control Windows notifications and focus assist settings")
        {
        }
    }
}

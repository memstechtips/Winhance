using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Infrastructure.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Optimize.Descriptors
{
    /// <summary>
    /// Feature descriptor for Power management optimizations.
    /// </summary>
    public class PowerFeatureDescriptor : BaseFeatureDescriptor
    {
        public PowerFeatureDescriptor() 
            : base(
                moduleId: "power",
                displayName: "Power Management",
                category: "Optimization",
                sortOrder: 4,
                domainServiceType: typeof(IPowerService),
                description: "Optimize power settings for performance or battery life")
        {
        }
    }
}

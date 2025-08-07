using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Infrastructure.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Optimize.Descriptors
{
    /// <summary>
    /// Feature descriptor for Gaming and Performance optimizations.
    /// </summary>
    public class GamingPerformanceFeatureDescriptor : BaseFeatureDescriptor
    {
        public GamingPerformanceFeatureDescriptor() 
            : base(
                moduleId: "gaming-performance",
                displayName: "Gaming & Performance",
                category: "Optimization",
                sortOrder: 1,
                domainServiceType: typeof(IGamingPerformanceService),
                description: "Optimize Windows for gaming performance and system responsiveness")
        {
        }
    }
}

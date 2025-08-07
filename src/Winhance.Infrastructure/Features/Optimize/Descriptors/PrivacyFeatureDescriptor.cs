using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Infrastructure.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Optimize.Descriptors
{
    /// <summary>
    /// Feature descriptor for Privacy optimizations.
    /// </summary>
    public class PrivacyFeatureDescriptor : BaseFeatureDescriptor
    {
        public PrivacyFeatureDescriptor() 
            : base(
                moduleId: "privacy",
                displayName: "Privacy",
                category: "Optimization",
                sortOrder: 2,
                domainServiceType: typeof(IPrivacyService),
                description: "Enhance your privacy by disabling telemetry and data collection")
        {
        }
    }
}

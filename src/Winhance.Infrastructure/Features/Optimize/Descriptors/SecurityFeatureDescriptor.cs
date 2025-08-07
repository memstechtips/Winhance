using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Infrastructure.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Optimize.Descriptors
{
    /// <summary>
    /// Feature descriptor for Windows Security optimizations.
    /// </summary>
    public class SecurityFeatureDescriptor : BaseFeatureDescriptor
    {
        public SecurityFeatureDescriptor() 
            : base(
                moduleId: "security",
                displayName: "Windows Security",
                category: "Optimization",
                sortOrder: 5,
                domainServiceType: typeof(ISecurityService),
                description: "Configure Windows Security and antivirus settings")
        {
        }
    }
}

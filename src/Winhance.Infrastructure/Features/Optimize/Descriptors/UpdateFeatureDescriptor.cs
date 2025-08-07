using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Infrastructure.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Optimize.Descriptors
{
    /// <summary>
    /// Feature descriptor for Windows Update optimizations.
    /// </summary>
    public class UpdateFeatureDescriptor : BaseFeatureDescriptor
    {
        public UpdateFeatureDescriptor() 
            : base(
                moduleId: "updates",
                displayName: "Windows Updates",
                category: "Optimization",
                sortOrder: 3,
                domainServiceType: typeof(IUpdateService),
                description: "Control Windows Update behavior and automatic updates")
        {
        }
    }
}

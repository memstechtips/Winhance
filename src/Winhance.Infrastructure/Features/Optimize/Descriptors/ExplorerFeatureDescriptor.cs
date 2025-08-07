using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Infrastructure.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Optimize.Descriptors
{
    /// <summary>
    /// Feature descriptor for File Explorer optimizations.
    /// </summary>
    public class ExplorerFeatureDescriptor : BaseFeatureDescriptor
    {
        public ExplorerFeatureDescriptor() 
            : base(
                moduleId: "explorer-optimization",
                displayName: "File Explorer",
                category: "Optimization",
                sortOrder: 6,
                domainServiceType: typeof(IExplorerOptimizationService),
                description: "Optimize File Explorer performance and behavior")
        {
        }
    }
}

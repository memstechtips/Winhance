using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Infrastructure.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Customize.Descriptors
{
    /// <summary>
    /// Feature descriptor for File Explorer customizations.
    /// </summary>
    public class ExplorerCustomizationFeatureDescriptor : BaseFeatureDescriptor
    {
        public ExplorerCustomizationFeatureDescriptor() 
            : base(
                moduleId: "explorer-customization",
                displayName: "File Explorer",
                category: "Customization",
                sortOrder: 4,
                domainServiceType: typeof(IExplorerCustomizationService),
                description: "Customize File Explorer appearance and navigation options")
        {
        }
    }
}

using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Infrastructure.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Customize.Descriptors
{
    /// <summary>
    /// Feature descriptor for Start Menu customizations.
    /// </summary>
    public class StartMenuFeatureDescriptor : BaseFeatureDescriptor
    {
        public StartMenuFeatureDescriptor() 
            : base(
                moduleId: "start-menu",
                displayName: "Start Menu",
                category: "Customization",
                sortOrder: 2,
                domainServiceType: typeof(IStartMenuService),
                description: "Customize Start Menu layout, suggestions, and behavior")
        {
        }
    }
}

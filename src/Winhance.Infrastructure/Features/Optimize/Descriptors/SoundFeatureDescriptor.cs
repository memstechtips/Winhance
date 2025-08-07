using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Infrastructure.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Optimize.Descriptors
{
    /// <summary>
    /// Feature descriptor for Sound optimizations.
    /// </summary>
    public class SoundFeatureDescriptor : BaseFeatureDescriptor
    {
        public SoundFeatureDescriptor() 
            : base(
                moduleId: "sound",
                displayName: "Sound & Audio",
                category: "Optimization",
                sortOrder: 8,
                domainServiceType: typeof(ISoundService),
                description: "Optimize audio settings and sound enhancements")
        {
        }
    }
}

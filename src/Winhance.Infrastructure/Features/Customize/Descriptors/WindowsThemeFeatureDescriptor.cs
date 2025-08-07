using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Infrastructure.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Customize.Descriptors
{
    /// <summary>
    /// Feature descriptor for Windows Theme customizations.
    /// </summary>
    public class WindowsThemeFeatureDescriptor : BaseFeatureDescriptor
    {
        public WindowsThemeFeatureDescriptor() 
            : base(
                moduleId: "windows-theme",
                displayName: "Windows Theme",
                category: "Customization",
                sortOrder: 1,
                domainServiceType: typeof(IWindowsThemeService),
                description: "Customize Windows appearance, dark/light mode, and visual effects")
        {
        }
    }
}

using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    public static partial class ExternalAppDefinitions
    {
        public static class CustomizationUtilities
        {
            public static ItemGroup GetCustomizationUtilities()
            {
                return new ItemGroup
                {
                    Name = "Customization Utilities",
                    FeatureId = FeatureIds.ExternalApps,
                    Items = new List<ItemDefinition>
                    {
                        // TODO: Add customization utilities from the original catalog
                        // This is a placeholder - can be expanded with apps like:
                        // - PowerToys
                        // - Rainmeter
                        // - TranslucentTB
                        // - etc.
                    }
                };
            }
        }
    }
}
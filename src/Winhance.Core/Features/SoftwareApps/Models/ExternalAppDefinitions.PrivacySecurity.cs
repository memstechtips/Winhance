using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    public static partial class ExternalAppDefinitions
    {
        public static class PrivacySecurity
        {
            public static ItemGroup GetPrivacySecurity()
            {
                return new ItemGroup
                {
                    Name = "Privacy & Security",
                    FeatureId = FeatureIds.ExternalApps,
                    Items = new List<ItemDefinition>
                    {
                        new ItemDefinition
                        {
                            Id = "external-app-malwarebytes",
                            Name = "Malwarebytes",
                            Description = "Anti-malware software for Windows",
                            GroupName = "Privacy & Security",
                            WinGetPackageId = "Malwarebytes.Malwarebytes",
                            Category = "Privacy & Security"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-malwarebytes-adwcleaner",
                            Name = "Malwarebytes AdwCleaner",
                            Description = "Adware removal tool for Windows",
                            GroupName = "Privacy & Security",
                            WinGetPackageId = "Malwarebytes.AdwCleaner",
                            Category = "Privacy & Security"
                        }
                    }
                };
            }
        }
    }
}
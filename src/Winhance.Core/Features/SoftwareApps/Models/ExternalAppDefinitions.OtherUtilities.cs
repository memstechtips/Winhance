using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    public static partial class ExternalAppDefinitions
    {
        public static class OtherUtilities
        {
            public static ItemGroup GetOtherUtilities()
            {
                return new ItemGroup
                {
                    Name = "Other Utilities",
                    FeatureId = FeatureIds.ExternalApps,
                    Items = new List<ItemDefinition>
                    {
                        new ItemDefinition
                        {
                            Id = "external-app-snappy-driver-installer",
                            Name = "Snappy Driver Installer Origin",
                            Description = "Driver installer and updater",
                            GroupName = "Other Utilities",
                            WinGetPackageId = "GlennDelahoy.SnappyDriverInstallerOrigin",
                            Category = "Other Utilities"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-wise-registry-cleaner",
                            Name = "Wise Registry Cleaner",
                            Description = "Registry cleaning and optimization tool",
                            GroupName = "Other Utilities",
                            WinGetPackageId = "XPDLS1XBTXVPP4",
                            Category = "Other Utilities"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-unigetui",
                            Name = "UniGetUI",
                            Description = "Universal package manager interface supporting WinGet, Chocolatey, and more",
                            GroupName = "Other Utilities",
                            WinGetPackageId = "MartiCliment.UniGetUI",
                            Category = "Other Utilities"
                        }
                    }
                };
            }
        }
    }
}
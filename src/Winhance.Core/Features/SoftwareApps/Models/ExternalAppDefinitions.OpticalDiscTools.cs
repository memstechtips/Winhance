using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    public static partial class ExternalAppDefinitions
    {
        public static class OpticalDiscTools
        {
            public static ItemGroup GetOpticalDiscTools()
            {
                return new ItemGroup
                {
                    Name = "Optical Disc Tools",
                    FeatureId = FeatureIds.ExternalApps,
                    Items = new List<ItemDefinition>
                    {
                        new ItemDefinition
                        {
                            Id = "external-app-imgburn",
                            Name = "ImgBurn",
                            Description = "Lightweight CD / DVD / HD DVD / Blu-ray burning application",
                            GroupName = "Optical Disc Tools",
                            WinGetPackageId = "LIGHTNINGUK.ImgBurn",
                            Category = "Optical Disc Tools"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-anyburn",
                            Name = "AnyBurn",
                            Description = "Lightweight CD/DVD/Blu-ray burning software",
                            GroupName = "Optical Disc Tools",
                            WinGetPackageId = "PowerSoftware.AnyBurn",
                            Category = "Optical Disc Tools"
                        }
                    }
                };
            }
        }
    }
}
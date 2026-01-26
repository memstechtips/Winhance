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
                            WinGetPackageId = ["LIGHTNINGUK.ImgBurn"],
                            WebsiteUrl = "https://www.imgburn.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-anyburn",
                            Name = "AnyBurn",
                            Description = "Lightweight CD/DVD/Blu-ray burning software",
                            GroupName = "Optical Disc Tools",
                            WinGetPackageId = ["PowerSoftware.AnyBurn"],
                            WebsiteUrl = "http://www.anyburn.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-makemkv",
                            Name = "MakeMKV",
                            Description = "DVD and Blu-ray to MKV converter and streaming tool",
                            GroupName = "Optical Disc Tools",
                            WinGetPackageId = ["GuinpinSoft.MakeMKV"],
                            WebsiteUrl = "https://www.makemkv.com/"
                        }
                    }
                };
            }
        }
    }
}
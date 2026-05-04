using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Models;

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
                        ChocoPackageId = "imgburn",
                        WebsiteUrl = "https://www.imgburn.com/",
                        IconSources = [
                            "https://www.imgburn.com/images/logo.png",
                            "https://www.imgburn.com/favicon.ico",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-anyburn",
                        Name = "AnyBurn",
                        Description = "Lightweight CD/DVD/Blu-ray burning software",
                        GroupName = "Optical Disc Tools",
                        WinGetPackageId = ["PowerSoftware.AnyBurn"],
                        WebsiteUrl = "http://www.anyburn.com/",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://www.anyburn.com/anyburn_setup.exe",
                        },
                        IconSources = [
                            "https://www.anyburn.com/images/anyburn_logo.png",
                            "https://www.anyburn.com/favicon.ico",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-cdburnerxp",
                        Name = "CDBurnerXP",
                        Description = "Free CD/DVD/Blu-ray burning software",
                        RegistryDisplayName = "CDBurnerXP",
                        GroupName = "Optical Disc Tools",
                        ChocoPackageId = "cdburnerxp",
                        WebsiteUrl = "https://cdburnerxp.se/",
                        IconSources = [
                            // Wikimedia file is 132x132 native — below the usual 256 minimum,
                            // but accepted here because the vendor site blocks programmatic
                            // fetches and no other trusted source exists.
                            "https://upload.wikimedia.org/wikipedia/commons/e/e7/CDBurnerXP_logo.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-makemkv",
                        Name = "MakeMKV",
                        Description = "DVD and Blu-ray to MKV converter and streaming tool",
                        RegistryDisplayName = "MakeMKV {version}",
                        GroupName = "Optical Disc Tools",
                        WinGetPackageId = ["GuinpinSoft.MakeMKV"],
                        ChocoPackageId = "makemkv",
                        WebsiteUrl = "https://www.makemkv.com/",
                        IconSources = [
                            "https://www.makemkv.com/images/mkv_icon.png",
                            "https://www.makemkv.com/favicon.ico",
                        ],
                    }
                }
            };
        }
    }
}

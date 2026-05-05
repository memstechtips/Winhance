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
                            "https://www.imgburn.com/images/logo_imgburn.png",
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
                            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABwAAAAcCAMAAABF0y+mAAAAt1BMVEVHcEzsZQL0gRT3khz1hBflgSn9oyj2ihr1hBb3ihr7nSToaAbkcw7uih3wbwjhXwHthhfydAvxcwrudgvoZAL8oCXxcgntYgD8oif+qCrxdArzhxb6nyb9oyfrki3+pyr0gBL8/Pz39/jz8/Pv7+7j4+Lr6+vn5+fd3d3S0MzX19fqzKzmlkrv48+/v7+QkJDmr3nloFigoKCysrLctpnHx8eVlZXi1snj3tbqxZ3ovozfoWq4uLiRrh8lAAAAIXRSTlMAsSu0CPw8IRcPVu3+5zj9+kZjw9Vqe1OGsIrE0+7znJMENytTAAAB3ElEQVQokWWT57arIBCFIRbEHtPLKYqI2EtMff/3uqBZWbk580v51pQ9bAB4hYqBigBWwd8wDMPE+ko1ETY+EAJoq+9WPztzp69m4vc9dFM/Ko7z/e04ylE3zbdmugG+FL9obnl+awpf+cJYf7VGu61f5CW9pkkcl3nhb5XZE8108OO3JYkZFZBEYdnaCnCfePXlt2EYSSqZ+Gxtfz1pQPqhKEeYsImRsljMkVSEtkc/HzOimDIiGYlz2/kVdVVDJo69kqHvBxZFhKVcpBoqwKbiNFcmaFKd6/rcU0IzPjTWIRB18cq5X1OWEH7uUtqfK5pmGT9pa8GQqWinLKWMVTUPQ1r3qaQnbRGgCfIsTQUcojCreyr0PqEs2/BMzJl1XTV052HcxsmSZeVArRwoIryr666K5TauD2shBhJSFlouhg1DklUVj8d1XLSllALQ/mA3Qp08TBIybePuLfezcX3zhXafmJh53FUuEtFkiMC3tVIyJhTJBrHmWevnlbng17PvZZJQqYixm+bB15UBBPee3d4uF84vl/xhe3vovmziGsCCnq0Vj0er2R60MHbfHDoP3CW0LMtbWnA532w+rbl2YQDhBrrrT2uOpt7geaBu/pp6bC2ew+y/5/APfX4/uuE2/A8AAAAASUVORK5CYII=",
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

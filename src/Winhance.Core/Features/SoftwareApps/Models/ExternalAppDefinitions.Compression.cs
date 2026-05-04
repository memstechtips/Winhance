using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models;

public static partial class ExternalAppDefinitions
{
    public static class Compression
    {
        public static ItemGroup GetCompression()
        {
            return new ItemGroup
            {
                Name = "Compression",
                FeatureId = FeatureIds.ExternalApps,
                Items = new List<ItemDefinition>
                {
                    new ItemDefinition
                    {
                        Id = "external-app-7zip",
                        Name = "7-Zip",
                        Description = "Open-source file archiver with a high compression ratio",
                        GroupName = "Compression",
                        WinGetPackageId = ["7zip.7zip"],
                        ChocoPackageId = "7zip",
                        WebsiteUrl = "https://www.7-zip.org/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/3/32/7-Zip_Icon.svg/256px-7-Zip_Icon.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-winrar",
                        Name = "WinRAR archiver",
                        Description = "File archiver with a high compression ratio",
                        GroupName = "Compression",
                        WinGetPackageId = ["RARLab.WinRAR"],
                        ChocoPackageId = "winrar",
                        WebsiteUrl = "https://www.win-rar.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/6/6a/WinRAR_icon.svg/256px-WinRAR_icon.svg.png",
                            "https://www.win-rar.com/fileadmin/templates/logo-winrar.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-peazip",
                        Name = "PeaZip",
                        Description = "Free file archiver utility. Open and extract RAR, TAR, ZIP files and more",
                        RegistryDisplayName = "PeaZip {version} ({arch})",
                        GroupName = "Compression",
                        WinGetPackageId = ["Giorgiotani.Peazip"],
                        ChocoPackageId = "peazip",
                        WebsiteUrl = "https://peazip.github.io/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/f/fe/Peazip_ico.svg/256px-Peazip_ico.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-nanazip",
                        Name = "NanaZip",
                        Description = "Open source fork of 7-zip intended for the modern Windows experience",
                        GroupName = "Compression",
                        AppxPackageName = ["40174MouriNaruto.NanaZip"],
                        WinGetPackageId = ["M2Team.NanaZip"],
                        ChocoPackageId = "nanazip",
                        WebsiteUrl = "https://github.com/M2Team/NanaZip",
                        IconSources = [
                            "https://raw.githubusercontent.com/M2Team/NanaZip/main/Assets/NanaZip.png",
                        ],
                    }
                }
            };
        }
    }
}

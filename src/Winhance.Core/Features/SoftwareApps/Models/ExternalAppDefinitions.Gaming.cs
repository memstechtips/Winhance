using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models;

public static partial class ExternalAppDefinitions
{
    public static class Gaming
    {
        public static ItemGroup GetGaming()
        {
            return new ItemGroup
            {
                Name = "Gaming",
                FeatureId = FeatureIds.ExternalApps,
                Items = new List<ItemDefinition>
                {
                    new ItemDefinition
                    {
                        Id = "external-app-steam",
                        Name = "Steam",
                        Description = "Digital distribution platform for PC gaming",
                        RegistryDisplayName = "Steam",
                        DetectionPaths = new[] { @"%ProgramFiles(x86)%\Steam\Steam.exe", @"%ProgramFiles%\Steam\Steam.exe" },
                        GroupName = "Gaming",
                        WinGetPackageId = ["Valve.Steam"],
                        ChocoPackageId = "steam",
                        WebsiteUrl = "https://store.steampowered.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/256px-Steam_icon_logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-epic-games",
                        Name = "Epic Games Launcher",
                        Description = "Digital distribution platform for PC gaming",
                        GroupName = "Gaming",
                        WinGetPackageId = ["EpicGames.EpicGamesLauncher"],
                        ChocoPackageId = "epicgameslauncher",
                        WebsiteUrl = "https://www.epicgames.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/3/31/Epic_Games_logo.svg/500px-Epic_Games_logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-good-old-games",
                        Name = "GOG GALAXY",
                        Description = "Digital distribution platform for PC gaming",
                        GroupName = "Gaming",
                        WinGetPackageId = ["GOG.Galaxy"],
                        ChocoPackageId = "goggalaxy",
                        MsStoreId = "XPFFXW40W60KCF",
                        WebsiteUrl = "https://www.gogalaxy.com/en/",
                        // Icon resolved via MS Store CDN (Layer 2a). Vendor only ships
                        // SVG and the Wikimedia render is the wide GOG.com wordmark.
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-playnite",
                        Name = "Playnite",
                        Description = "Open-source video game library manager with support for multiple game stores",
                        GroupName = "Gaming",
                        WinGetPackageId = ["Playnite.Playnite"],
                        ChocoPackageId = "playnite",
                        WebsiteUrl = "https://playnite.link/",
                        IconSources = [
                            "https://playnite.link/applogo.png",
                        ],
                    }
                }
            };
        }
    }
}

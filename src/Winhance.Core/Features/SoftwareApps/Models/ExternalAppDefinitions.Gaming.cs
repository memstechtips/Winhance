using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
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
                            GroupName = "Gaming",
                            WinGetPackageId = ["Valve.Steam"],
                            WebsiteUrl = "https://store.steampowered.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-epic-games",
                            Name = "Epic Games Launcher",
                            Description = "Digital distribution platform for PC gaming",
                            GroupName = "Gaming",
                            WinGetPackageId = ["EpicGames.EpicGamesLauncher"],
                            WebsiteUrl = "https://www.epicgames.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-good-old-games",
                            Name = "GOG Galaxy",
                            Description = "Digital distribution platform for PC gaming",
                            GroupName = "Gaming",
                            WinGetPackageId = ["GOG.Galaxy"],
                            WebsiteUrl = "https://www.gogalaxy.com/en/"
                        }
                    }
                };
            }
        }
    }
}
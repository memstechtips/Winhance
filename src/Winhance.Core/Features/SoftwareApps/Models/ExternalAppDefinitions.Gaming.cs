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
                            WinGetPackageId = "Valve.Steam",
                            Category = "Gaming"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-epic-games",
                            Name = "Epic Games Launcher",
                            Description = "Digital distribution platform for PC gaming",
                            GroupName = "Gaming",
                            WinGetPackageId = "EpicGames.EpicGamesLauncher",
                            Category = "Gaming"
                        }
                    }
                };
            }
        }
    }
}
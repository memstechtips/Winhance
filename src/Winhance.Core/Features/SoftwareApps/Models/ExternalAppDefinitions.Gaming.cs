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
                        Description = "Valve's PC gaming storefront, library, and social platform",
                        RegistryDisplayName = "Steam",
                        DetectionPaths = new[] { @"%ProgramFiles(x86)%\Steam\Steam.exe", @"%ProgramFiles%\Steam\Steam.exe" },
                        GroupName = "Gaming",
                        WinGetPackageId = ["Valve.Steam"],
                        ChocoPackageId = "steam",
                        WebsiteUrl = "https://store.steampowered.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Steam_icon_logo.svg/250px-Steam_icon_logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-epic-games",
                        Name = "Epic Games Launcher",
                        Description = "Epic's PC game store and launcher with weekly free games",
                        GroupName = "Gaming",
                        WinGetPackageId = ["EpicGames.EpicGamesLauncher"],
                        ChocoPackageId = "epicgameslauncher",
                        MsStoreId = "XP99VR1BPSBQJ2",
                        WebsiteUrl = "https://www.epicgames.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/3/31/Epic_Games_logo.svg/500px-Epic_Games_logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-good-old-games",
                        Name = "GOG GALAXY",
                        Description = "DRM-free PC game store and library manager from CD Projekt",
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
                        Id = "external-app-battle-net",
                        Name = "Battle.net",
                        Description = "Blizzard's game launcher and store for Warcraft, Diablo, Overwatch, and Call of Duty",
                        GroupName = "Gaming",
                        WinGetPackageId = ["Blizzard.BattleNet"],
                        ChocoPackageId = "battle.net",
                        MsStoreId = "xpdm5vsmtkqlbj",
                        WebsiteUrl = "https://www.blizzard.com/apps/battle.net/desktop",
                        RegistryDisplayName = "Battle.net",
                        RegistrySubKeyName = "Battle.net",
                        // Icon resolved via MS Store CDN (Layer 2a) from MsStoreId.
                        // MsStoreId is "XP..."-prefixed: a Win32 installer distributed via the
                        // Store, not an MSIX/UWP package, so it writes a classic Uninstall key
                        // (HKLM_WOW6432) and needs registry detection, not AppxPackageName.
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-ea-app",
                        Name = "EA app",
                        Description = "Electronic Arts' PC game launcher and store, successor to Origin",
                        GroupName = "Gaming",
                        WinGetPackageId = ["ElectronicArts.EADesktop"],
                        ChocoPackageId = "ea-app",
                        WebsiteUrl = "https://www.ea.com/ea-app",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/0/0d/Electronic-Arts-Logo.svg/250px-Electronic-Arts-Logo.svg.png",
                        ],
                        // Detect by DisplayName, not the MSI product code. EA app auto-updates
                        // and can change its product-code GUID, which would silently break
                        // GUID-pinned detection (the frequent path). The detection scanner
                        // excludes the hidden HKLM SystemComponent=1 "EA app" entry from
                        // DisplayNames, so this cleanly matches the single visible install.
                        // Registry uninstall is only a last-resort fallback (WinGet + Choco
                        // run first), so the hidden-entry edge case there is acceptable.
                        RegistryDisplayName = "EA app",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-rockstar-games-launcher",
                        Name = "Rockstar Games Launcher",
                        Description = "Rockstar's PC launcher and store for Grand Theft Auto and Red Dead Redemption",
                        GroupName = "Gaming",
                        WinGetPackageId = ["RockstarGames.Launcher"],
                        ChocoPackageId = "rockstar-launcher",
                        WebsiteUrl = "https://socialclub.rockstargames.com/rockstar-games-launcher",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/5/53/Rockstar_Games_Logo.svg/250px-Rockstar_Games_Logo.svg.png",
                        ],
                        RegistryDisplayName = "Rockstar Games Launcher",
                        RegistrySubKeyName = "Rockstar Games Launcher",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-ubisoft-connect",
                        Name = "Ubisoft Connect",
                        Description = "Ubisoft's PC game launcher, store, and rewards platform, successor to Uplay",
                        GroupName = "Gaming",
                        WinGetPackageId = ["Ubisoft.Connect"],
                        ChocoPackageId = "ubisoft-connect",
                        MsStoreId = "xpdp2qw12dfsfk",
                        WebsiteUrl = "https://www.ubisoft.com/ubisoft-connect",
                        RegistryDisplayName = "Ubisoft Connect",
                        RegistrySubKeyName = "Uplay",   // legacy subkey name; DisplayName is "Ubisoft Connect"
                        // Icon resolved via MS Store CDN (Layer 2a) from MsStoreId.
                        // "XP..."-prefixed Store ID = Win32-via-Store, so it writes a classic
                        // Uninstall key and needs registry detection, not AppxPackageName.
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

using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Models;

public static partial class ExternalAppDefinitions
{
    public static class CustomizationUtilities
    {
        public static ItemGroup GetCustomizationUtilities()
        {
            return new ItemGroup
            {
                Name = "Customization Utilities",
                FeatureId = FeatureIds.ExternalApps,
                Items = new List<ItemDefinition>
                {
                    new ItemDefinition
                    {
                        Id = "external-app-nilesoft-shell",
                        Name = "Nilesoft Shell",
                        Description = "Windows context menu customization tool",
                        GroupName = "Customization Utilities",
                        WinGetPackageId = ["Nilesoft.Shell"],
                        ChocoPackageId = "nilesoft-shell",
                        WebsiteUrl = "https://nilesoft.org/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-startallback",
                        Name = "StartAllBack (Win 11)",
                        Description = "Windows 11 Start menu and taskbar customization",
                        RegistryDisplayName = "StartAllBack",
                        GroupName = "Customization Utilities",
                        WinGetPackageId = ["StartIsBack.StartAllBack"],
                        ChocoPackageId = "startallback",
                        WebsiteUrl = "https://www.startallback.com/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-startisback",
                        Name = "StartIsBack++ (Win 10)",
                        Description = "Windows 10 Start menu and taskbar customization",
                        RegistrySubKeyName = "StartIsBack",
                        RegistryDisplayName = "StartIsBack++",
                        GroupName = "Customization Utilities",
                        WinGetPackageId = ["StartIsBack.StartIsBack"],
                        WebsiteUrl = "https://www.startisback.com/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-open-shell",
                        Name = "Open-Shell",
                        Description = "Classic style Start Menu for Windows",
                        GroupName = "Customization Utilities",
                        WinGetPackageId = ["Open-Shell.Open-Shell-Menu"],
                        ChocoPackageId = "open-shell",
                        WebsiteUrl = "https://open-shell.github.io/Open-Shell-Menu/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-windhawk",
                        Name = "Windhawk",
                        Description = "Customization platform for Windows",
                        GroupName = "Customization Utilities",
                        WinGetPackageId = ["RamenSoftware.Windhawk"],
                        ChocoPackageId = "windhawk",
                        WebsiteUrl = "https://windhawk.net/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-lively-wallpaper",
                        Name = "Lively Wallpaper",
                        Description = "Free and open-source animated desktop wallpaper application",
                        RegistryDisplayName = "Lively Wallpaper version {version}",
                        GroupName = "Customization Utilities",
                        WinGetPackageId = ["rocksdanister.LivelyWallpaper"],
                        ChocoPackageId = "lively",
                        WebsiteUrl = "https://www.rocksdanister.com/lively/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-sucrose-wallpaper",
                        Name = "Sucrose Wallpaper Engine",
                        Description = "Free and open-source animated desktop wallpaper application",
                        GroupName = "Customization Utilities",
                        WinGetPackageId = ["Taiizor.SucroseWallpaperEngine"],
                        ChocoPackageId = "sucrose",
                        WebsiteUrl = "https://github.com/Taiizor/Sucrose"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-rainmeter",
                        Name = "Rainmeter",
                        Description = "Desktop customization tool for Windows",
                        GroupName = "Customization Utilities",
                        WinGetPackageId = ["Rainmeter.Rainmeter"],
                        ChocoPackageId = "rainmeter",
                        WebsiteUrl = "https://www.rainmeter.net/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-explorerpatcher",
                        Name = "ExplorerPatcher",
                        Description = "Utility that enhances the Windows Explorer experience",
                        GroupName = "Customization Utilities",
                        WinGetPackageId = ["valinet.ExplorerPatcher"],
                        WebsiteUrl = "https://github.com/valinet/ExplorerPatcher",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://github.com/valinet/ExplorerPatcher/releases/latest/download/ep_setup.exe",
                        }
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-johns-background-switcher",
                        Name = "John's Background Switcher",
                        Description = "Automatically changes your desktop wallpaper at regular intervals",
                        RegistryDisplayName = "John's Background Switcher {version}",
                        GroupName = "Customization Utilities",
                        WinGetPackageId = ["johnsadventures.JohnsBackgroundSwitcher"],
                        ChocoPackageId = "jbs",
                        WebsiteUrl = "https://johnsad.ventures/software/backgroundswitcher/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-powertoys",
                        Name = "Microsoft PowerToys",
                        Description = "Set of utilities for power users to tune and streamline their Windows experience",
                        RegistryDisplayName = "PowerToys (Preview) {arch}",
                        GroupName = "Customization Utilities",
                        WinGetPackageId = ["Microsoft.PowerToys"],
                        ChocoPackageId = "powertoys",
                        MsStoreId = "XP89DCGQ3K6VLD",
                        WebsiteUrl = "https://github.com/microsoft/PowerToys"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-nexus-dock",
                        Name = "Nexus",
                        Description = "The advanced docking system for Windows",
                        GroupName = "Customization Utilities",
                        WinGetPackageId = ["WinStep.Nexus"],
                        WebsiteUrl = "https://www.winstep.net/nexus.asp",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://www.winstep.net/nexus.zip",
                        }
                    }
                }
            };
        }
    }
}

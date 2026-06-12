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
                        WebsiteUrl = "https://nilesoft.org/",
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
                        MsStoreId = "XPFMHKP3QHRQRH",
                        WebsiteUrl = "https://www.startallback.com/",
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
                        WebsiteUrl = "https://www.startisback.com/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-open-shell",
                        Name = "Open-Shell",
                        Description = "Classic style Start Menu for Windows",
                        GroupName = "Customization Utilities",
                        WinGetPackageId = ["Open-Shell.Open-Shell-Menu"],
                        ChocoPackageId = "open-shell",
                        WebsiteUrl = "https://open-shell.github.io/Open-Shell-Menu/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-windhawk",
                        Name = "Windhawk",
                        Description = "Customization platform for Windows",
                        GroupName = "Customization Utilities",
                        WinGetPackageId = ["RamenSoftware.Windhawk"],
                        ChocoPackageId = "windhawk",
                        WebsiteUrl = "https://windhawk.net/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-lively-wallpaper",
                        Name = "Lively Wallpaper",
                        Description = "Free and open-source animated desktop wallpaper application",
                        RegistryDisplayName = "Lively Wallpaper version {version}",
                        GroupName = "Customization Utilities",
                        WinGetPackageId = ["rocksdanister.LivelyWallpaper"],
                        MsStoreId = "9NTM2QC6QWS7",
                        ChocoPackageId = "lively",
                        WebsiteUrl = "https://www.rocksdanister.com/lively/",
                        // Icon resolved via MS Store CDN (Layer 2a). No trusted catalog URL.
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-sucrose-wallpaper",
                        Name = "Sucrose Wallpaper Engine",
                        Description = "Open-source alternative to Wallpaper Engine for animated desktop backgrounds",
                        GroupName = "Customization Utilities",
                        WinGetPackageId = ["Taiizor.SucroseWallpaperEngine"],
                        ChocoPackageId = "sucrose",
                        MsStoreId = "XP8JGPBHTJGLCQ",
                        WebsiteUrl = "https://github.com/Taiizor/Sucrose",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-rainmeter",
                        Name = "Rainmeter",
                        Description = "Desktop customization tool for Windows",
                        GroupName = "Customization Utilities",
                        WinGetPackageId = ["Rainmeter.Rainmeter"],
                        ChocoPackageId = "rainmeter",
                        WebsiteUrl = "https://www.rainmeter.net/",
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
                        },
                        // No vendor logo asset exists on GitHub or Wikimedia, and the
                        // explorerpatcher.net WordPress media is Cloudflare-protected.
                        // Reuse Windows' own explorer.exe icon — visually appropriate
                        // since the app patches explorer, and present on every machine.
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-johns-background-switcher",
                        Name = "John's Background Switcher",
                        Description = "Automatically changes your desktop wallpaper at regular intervals",
                        RegistryDisplayName = "John's Background Switcher {version}",
                        GroupName = "Customization Utilities",
                        AppxPackageName = ["32808ManuelKurtz.BackgroundSwitcher"],
                        WinGetPackageId = ["johnsadventures.JohnsBackgroundSwitcher"],
                        ChocoPackageId = "jbs",
                        MsStoreId = "9MWKCB9MH93K",
                        WebsiteUrl = "https://johnsad.ventures/software/backgroundswitcher/",
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
                        WebsiteUrl = "https://github.com/microsoft/PowerToys",
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
                        },
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-autohotkey-v2",
                        Name = "AutoHotkey v2",
                        Description = "Free macro-creation and automation scripting language (v2, current)",
                        // v1 and v2 share SubKeyName "AutoHotkey"; only DisplayName can
                        // distinguish them. v1 writes "AutoHotkey 1.x.y.z", v2 writes
                        // exactly "AutoHotkey". No {version} token here — exact match.
                        RegistryDisplayName = "AutoHotkey",
                        GroupName = "Customization Utilities",
                        WinGetPackageId = ["AutoHotkey.AutoHotkey"],
                        ChocoPackageId = "autohotkey",
                        MsStoreId = "9PLQFDG8HH9D",
                        WebsiteUrl = "https://www.autohotkey.com/",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://www.autohotkey.com/download/ahk-v2.exe",
                        },
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-autohotkey-v1",
                        Name = "AutoHotkey v1",
                        Description = "Legacy branch of AutoHotkey (v1.1) for older scripts",
                        RegistryDisplayName = "AutoHotkey 1.{version}",
                        GroupName = "Customization Utilities",
                        WebsiteUrl = "https://www.autohotkey.com/",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://www.autohotkey.com/download/ahk-install.exe",
                            RequiresDirectDownload = true,
                        },
                    }
                }
            };
        }
    }
}

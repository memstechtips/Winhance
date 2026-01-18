using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
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
                            WinGetPackageId = "Nilesoft.Shell",
                            WebsiteUrl = "https://nilesoft.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-startallback",
                            Name = "StartAllBack",
                            Description = "Windows 11 Start menu and taskbar customization",
                            GroupName = "Customization Utilities",
                            WinGetPackageId = "StartIsBack.StartAllBack",
                            WebsiteUrl = "https://www.startallback.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-open-shell",
                            Name = "Open-Shell",
                            Description = "Classic style Start Menu for Windows",
                            GroupName = "Customization Utilities",
                            WinGetPackageId = "Open-Shell.Open-Shell-Menu",
                            WebsiteUrl = "https://open-shell.github.io/Open-Shell-Menu/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-windhawk",
                            Name = "Windhawk",
                            Description = "Customization platform for Windows",
                            GroupName = "Customization Utilities",
                            WinGetPackageId = "RamenSoftware.Windhawk",
                            WebsiteUrl = "https://windhawk.net/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-lively-wallpaper",
                            Name = "Lively Wallpaper",
                            Description = "Free and open-source animated desktop wallpaper application",
                            GroupName = "Customization Utilities",
                            WinGetPackageId = "rocksdanister.LivelyWallpaper",
                            WebsiteUrl = "https://www.rocksdanister.com/lively/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-sucrose-wallpaper",
                            Name = "Sucrose Wallpaper Engine",
                            Description = "Free and open-source animated desktop wallpaper application",
                            GroupName = "Customization Utilities",
                            WinGetPackageId = "Taiizor.SucroseWallpaperEngine",
                            WebsiteUrl = "https://github.com/Taiizor/Sucrose"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-rainmeter",
                            Name = "Rainmeter",
                            Description = "Desktop customization tool for Windows",
                            GroupName = "Customization Utilities",
                            WinGetPackageId = "Rainmeter.Rainmeter",
                            WebsiteUrl = "https://www.rainmeter.net/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-explorerpatcher",
                            Name = "ExplorerPatcher",
                            Description = "Utility that enhances the Windows Explorer experience",
                            GroupName = "Customization Utilities",
                            WinGetPackageId = "valinet.ExplorerPatcher",
                            WebsiteUrl = "https://github.com/valinet/ExplorerPatcher"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-nexus-dock",
                            Name = "Nexus",
                            Description = "The advanced docking system for Windows",
                            GroupName = "Customization Utilities",
                            WinGetPackageId = "WinStep.Nexus",
                            WebsiteUrl = "https://www.winstep.net/nexus.asp"
                        }
                    }
                };
            }
        }
    }
}
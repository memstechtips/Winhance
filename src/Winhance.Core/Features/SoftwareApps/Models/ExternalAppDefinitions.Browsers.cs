using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Models;

public static partial class ExternalAppDefinitions
{
    public static class Browsers
    {
        public static ItemGroup GetBrowsers()
        {
            return new ItemGroup
            {
                Name = "Browsers",
                FeatureId = FeatureIds.ExternalApps,
                Items = new List<ItemDefinition>
                {
                    new ItemDefinition
                    {
                        Id = "external-app-edge-webview",
                        Name = "Microsoft EdgeWebView",
                        Description = "WebView2 runtime for Windows applications",
                        RegistrySubKeyName = "Microsoft EdgeWebView",
                        RegistryDisplayName = "Microsoft Edge WebView2 Runtime",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Microsoft.EdgeWebView2Runtime"],
                        ChocoPackageId = "webview2-runtime",
                        WebsiteUrl = "https://developer.microsoft.com/en-us/microsoft-edge/webview2/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-thorium",
                        Name = "Thorium",
                        Description = "Chromium-based browser with enhanced privacy features",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Alex313031.Thorium"],
                        WebsiteUrl = "https://thorium.rocks/",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://github.com/Alex313031/Thorium-Win/releases/latest/download/thorium_SSE3_mini_installer.exe",
                        }
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-mercury",
                        Name = "Mercury",
                        Description = "Compiler optimized, private Firefox fork",
                        RegistrySubKeyName = "Mercury {version} ({arch} {locale})",
                        RegistryDisplayName = "Mercury ({arch} {locale})",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Alex313031.Mercury"],
                        ChocoPackageId = "mercury",
                        WebsiteUrl = "https://thorium.rocks/mercury"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-firefox",
                        Name = "Mozilla Firefox",
                        Description = "Popular web browser known for privacy and customization",
                        RegistrySubKeyName = "Mozilla Firefox {version} ({arch} {locale})",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Mozilla.Firefox"],
                        ChocoPackageId = "firefox",
                        WebsiteUrl = "https://www.mozilla.org/firefox/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-chrome",
                        Name = "Google Chrome",
                        Description = "Google's web browser with sync and extension support",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Google.Chrome", "Google.Chrome.EXE"],
                        ChocoPackageId = "googlechrome",
                        WebsiteUrl = "https://www.google.com/chrome/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-ungoogled-chromium",
                        Name = "ungoogled-chromium",
                        Description = "Chromium-based browser with privacy enhancements",
                        RegistryDisplayName = "Chromium",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Eloston.Ungoogled-Chromium"],
                        ChocoPackageId = "ungoogled-chromium",
                        WebsiteUrl = "https://github.com/ungoogled-software/ungoogled-chromium-windows"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-brave",
                        Name = "Brave",
                        Description = "Privacy-focused browser with built-in ad blocking",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Brave.Brave"],
                        ChocoPackageId = "brave",
                        WebsiteUrl = "https://brave.com/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-opera",
                        Name = "Opera",
                        Description = "Feature-rich web browser with built-in VPN and ad blocker",
                        RegistrySubKeyName = "Opera {version}",
                        RegistryDisplayName = "Opera Stable {version}",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Opera.Opera"],
                        ChocoPackageId = "opera",
                        WebsiteUrl = "https://www.opera.com/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-opera-gx",
                        Name = "Opera GX",
                        Description = "Gaming-oriented version of Opera with unique features",
                        RegistrySubKeyName = "Opera GX {version}",
                        RegistryDisplayName = "Opera GX Stable {version}",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Opera.OperaGX"],
                        ChocoPackageId = "opera-gx",
                        WebsiteUrl = "https://www.opera.com/gx"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-arc",
                        Name = "Arc Browser",
                        Description = "Innovative browser with a focus on design and user experience",
                        GroupName = "Browsers",
                        AppxPackageName = ["TheBrowserCompany.Arc"],
                        WinGetPackageId = ["TheBrowserCompany.Arc"],
                        WebsiteUrl = "https://arc.net/",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://releases.arc.net/windows/ArcInstaller.exe",
                        }
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-tor",
                        Name = "Tor Browser",
                        Description = "Privacy-focused browser that routes traffic through the Tor network",
                        GroupName = "Browsers",
                        WinGetPackageId = ["TorProject.TorBrowser"],
                        ChocoPackageId = "tor-browser",
                        DetectionPaths = [@"%USERPROFILE%\Desktop\Tor Browser"],
                        WebsiteUrl = "https://www.torproject.org/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-vivaldi",
                        Name = "Vivaldi",
                        Description = "Highly customizable browser with a focus on user control",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Vivaldi.Vivaldi"],
                        ChocoPackageId = "vivaldi",
                        WebsiteUrl = "https://vivaldi.com/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-waterfox",
                        Name = "Waterfox",
                        Description = "Firefox-based browser with a focus on privacy and customization",
                        RegistrySubKeyName = "Waterfox {version} ({arch} {locale})",
                        RegistryDisplayName = "Waterfox ({arch} {locale})",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Waterfox.Waterfox"],
                        ChocoPackageId = "waterfox",
                        WebsiteUrl = "https://www.waterfox.net/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-zen",
                        Name = "Zen Browser",
                        Description = "Privacy-focused browser with built-in ad blocking",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Zen-Team.Zen-Browser"],
                        WebsiteUrl = "https://zen-browser.app/",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://github.com/zen-browser/desktop/releases/latest/download/zen.installer.exe",
                        }
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-mullvad",
                        Name = "Mullvad Browser",
                        Description = "Privacy-focused browser designed to minimize tracking and fingerprints",
                        GroupName = "Browsers",
                        WinGetPackageId = ["MullvadVPN.MullvadBrowser"],
                        WebsiteUrl = "https://mullvad.net/en/browser",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://mullvad.net/en/download/browser/win64/latest",
                        }
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-pale-moon",
                        Name = "Pale Moon Browser",
                        Description = "Open Source, Goanna-based web browser focusing on efficiency and customization",
                        RegistrySubKeyName = "Pale Moon {version} ({arch} {locale})",
                        RegistryDisplayName = "Pale Moon {version} ({arch} {locale})",
                        GroupName = "Browsers",
                        WinGetPackageId = ["MoonchildProductions.PaleMoon"],
                        ChocoPackageId = "palemoon",
                        WebsiteUrl = "https://www.palemoon.org/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-maxthon",
                        Name = "Maxthon",
                        Description = "Privacy focused browser with built-in ad blocking and VPN",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Maxthon.Maxthon"],
                        ChocoPackageId = "maxthon",
                        WebsiteUrl = "https://www.maxthon.com/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-floorp",
                        Name = "Ablaze Floorp",
                        Description = "Privacy focused browser with strong tracking protection",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Ablaze.Floorp"],
                        ChocoPackageId = "floorp",
                        WebsiteUrl = "https://floorp.app/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-duckduckgo",
                        Name = "DuckDuckGo",
                        Description = "Privacy-focused search engine with a browser extension",
                        GroupName = "Browsers",
                        AppxPackageName = ["DuckDuckGo.DesktopBrowser"],
                        WinGetPackageId = ["DuckDuckGo.DesktopBrowser"],
                        WebsiteUrl = "https://duckduckgo.com/",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://staticcdn.duckduckgo.com/windows-desktop-browser/installer/DuckDuckGo.Installer.exe",
                        }
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-librewolf",
                        Name = "LibreWolf",
                        Description = "A custom version of Firefox, focused on privacy, security and freedom",
                        GroupName = "Browsers",
                        WinGetPackageId = ["LibreWolf.LibreWolf"],
                        ChocoPackageId = "librewolf",
                        WebsiteUrl = "https://librewolf.net/"
                    }
                }
            };
        }
    }
}

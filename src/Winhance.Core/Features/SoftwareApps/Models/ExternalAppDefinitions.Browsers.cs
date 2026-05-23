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
                        WebsiteUrl = "https://developer.microsoft.com/en-us/microsoft-edge/webview2/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/9/98/Microsoft_Edge_logo_%282019%29.svg/500px-Microsoft_Edge_logo_%282019%29.svg.png",
                        ],
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
                        },
                        IconSources = [
                            "https://raw.githubusercontent.com/Alex313031/thorium/main/logos/NEW/product_logo_256.png",
                        ],
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
                        WebsiteUrl = "https://thorium.rocks/mercury",
                        IconSources = [
                            "https://raw.githubusercontent.com/Alex313031/Mercury/main/logos/Mercury_256.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-firefox",
                        Name = "Mozilla Firefox",
                        Description = "Popular web browser known for privacy and customization",
                        RegistrySubKeyName = "Mozilla Firefox {version} ({arch} {locale})",
                        GroupName = "Browsers",
                        AppxPackageName = ["Mozilla.Firefox"],
                        WinGetPackageId = ["Mozilla.Firefox"],
                        ChocoPackageId = "firefox",
                        MsStoreId = "9NZVDKPMR9RD",
                        WebsiteUrl = "https://www.mozilla.org/firefox/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a0/Firefox_logo%2C_2019.svg/500px-Firefox_logo%2C_2019.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-chrome",
                        Name = "Google Chrome",
                        Description = "Google's web browser with sync and extension support",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Google.Chrome", "Google.Chrome.EXE"],
                        ChocoPackageId = "googlechrome",
                        WebsiteUrl = "https://www.google.com/chrome/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e1/Google_Chrome_icon_%28February_2022%29.svg/500px-Google_Chrome_icon_%28February_2022%29.svg.png",
                        ],
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
                        WebsiteUrl = "https://github.com/ungoogled-software/ungoogled-chromium-windows",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/2/28/Chromium_Logo.svg/250px-Chromium_Logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-brave",
                        Name = "Brave",
                        Description = "Privacy-focused browser with built-in ad blocking",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Brave.Brave"],
                        ChocoPackageId = "brave",
                        MsStoreId = "XP8C9QZMS2PC1T",
                        WebsiteUrl = "https://brave.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/9/9d/Brave_lion_icon.svg/500px-Brave_lion_icon.svg.png",
                        ],
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
                        MsStoreId = "XP8CF6S8G2D5T6",
                        WebsiteUrl = "https://www.opera.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/4/49/Opera_2015_icon.svg/500px-Opera_2015_icon.svg.png",
                        ],
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
                        MsStoreId = "XPDBZ4MPRKNN30",
                        WebsiteUrl = "https://www.opera.com/gx",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e7/Opera_GX_Icon.svg/500px-Opera_GX_Icon.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-arc",
                        Name = "Arc Browser",
                        Description = "Browser with sidebar tabs and Spaces, focused on design",
                        GroupName = "Browsers",
                        AppxPackageName = ["TheBrowserCompany.Arc"],
                        WinGetPackageId = ["TheBrowserCompany.Arc"],
                        MsStoreId = "XPFMDW72VHTTX9",
                        WebsiteUrl = "https://arc.net/",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://releases.arc.net/windows/ArcInstaller.exe",
                        },
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/3/37/Arc_%28browser%29_logo.svg/500px-Arc_%28browser%29_logo.svg.png",
                        ],
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
                        WebsiteUrl = "https://www.torproject.org/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c9/Tor_Browser_icon.svg/250px-Tor_Browser_icon.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-vivaldi",
                        Name = "Vivaldi",
                        Description = "Highly customizable browser with a focus on user control",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Vivaldi.Vivaldi"],
                        ChocoPackageId = "vivaldi",
                        MsStoreId = "XP99GVQDX7JPR4",
                        WebsiteUrl = "https://vivaldi.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e4/Vivaldi_web_browser_logo.svg/500px-Vivaldi_web_browser_logo.svg.png",
                        ],
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
                        WebsiteUrl = "https://www.waterfox.net/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/2/24/Waterfox_logo_2020_%28vectorized%29.svg/500px-Waterfox_logo_2020_%28vectorized%29.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-zen",
                        Name = "Zen Browser",
                        Description = "Firefox-based browser with workspaces, split tabs, and a clean UI",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Zen-Team.Zen-Browser"],
                        WebsiteUrl = "https://zen-browser.app/",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://github.com/zen-browser/desktop/releases/latest/download/zen.installer.exe",
                        },
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3c/Zen_Browser_logo_%28red_circles%29.svg/500px-Zen_Browser_logo_%28red_circles%29.svg.png",
                        ],
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
                        },
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/7/70/Mullvad_Browser_logo.svg/500px-Mullvad_Browser_logo.svg.png",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/c/ca/Mullvad_logo.svg/500px-Mullvad_logo.svg.png",
                        ],
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
                        MsStoreId = "XPDKN9FQ75F2MZ",
                        WebsiteUrl = "https://www.palemoon.org/",
                        IconSources = [
                            "https://www.palemoon.org/images/branding/logo128.png",
                            "https://www.palemoon.org/favicon.ico",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-maxthon",
                        Name = "Maxthon",
                        Description = "Privacy focused browser with built-in ad blocking and VPN",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Maxthon.Maxthon"],
                        ChocoPackageId = "maxthon",
                        WebsiteUrl = "https://www.maxthon.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/0/0b/Maxthon_logo.svg/500px-Maxthon_logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-floorp",
                        Name = "Ablaze Floorp",
                        Description = "Privacy focused browser with strong tracking protection",
                        GroupName = "Browsers",
                        WinGetPackageId = ["Ablaze.Floorp"],
                        ChocoPackageId = "floorp",
                        WebsiteUrl = "https://floorp.app/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a9/Floorp_logo_without_text.svg/250px-Floorp_logo_without_text.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-duckduckgo",
                        Name = "DuckDuckGo",
                        Description = "Privacy-focused web browser with built-in tracker blocking",
                        GroupName = "Browsers",
                        AppxPackageName = ["DuckDuckGo.DesktopBrowser", "63909DuckDuckGoInc.DuckDuckGoPrivateBrowser"],
                        WinGetPackageId = ["DuckDuckGo.DesktopBrowser"],
                        MsStoreId = "9N74NHXCH1N6",
                        WebsiteUrl = "https://duckduckgo.com/",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://staticcdn.duckduckgo.com/windows-desktop-browser/installer/DuckDuckGo.Installer.exe",
                        },
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/en/9/90/The_DuckDuckGo_Duck.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-librewolf",
                        Name = "LibreWolf",
                        Description = "A custom version of Firefox, focused on privacy, security and freedom",
                        GroupName = "Browsers",
                        AppxPackageName = ["31856maltejur.LibreWolf"],
                        WinGetPackageId = ["LibreWolf.LibreWolf"],
                        ChocoPackageId = "librewolf",
                        MsStoreId = "9NVN9SZ8KFD7",
                        WebsiteUrl = "https://librewolf.net/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d0/LibreWolf_icon.svg/500px-LibreWolf_icon.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-helium",
                        Name = "Helium",
                        Description = "Open source Chromium-based browser built on ungoogled-chromium with uBlock Origin",
                        GroupName = "Browsers",
                        WinGetPackageId = ["ImputNet.Helium"],
                        ChocoPackageId = "helium",
                        WebsiteUrl = "https://helium.computer/",
                        IconSources = [
                            "https://raw.githubusercontent.com/imputnet/helium/main/resources/branding/app_icon/raw.png",
                        ],
                    }
                }
            };
        }
    }
}

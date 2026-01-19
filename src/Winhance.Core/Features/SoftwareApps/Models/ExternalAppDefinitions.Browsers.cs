using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
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
                            Name = "Microsoft Edge WebView",
                            Description = "WebView2 runtime for Windows applications",
                            GroupName = "Browsers",
                            WinGetPackageId = "Microsoft.EdgeWebView2Runtime",
                            WebsiteUrl = "https://developer.microsoft.com/en-us/microsoft-edge/webview2/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-thorium",
                            Name = "Thorium",
                            Description = "Chromium-based browser with enhanced privacy features",
                            GroupName = "Browsers",
                            WinGetPackageId = "Alex313031.Thorium",
                            WebsiteUrl = "https://thorium.rocks/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-thorium-avx2",
                            Name = "Thorium AVX2",
                            Description = "Chromium-based browser with enhanced privacy features",
                            GroupName = "Browsers",
                            WinGetPackageId = "Alex313031.Thorium.AVX2",
                            WebsiteUrl = "https://thorium.rocks/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-mercury",
                            Name = "Mercury",
                            Description = "Compiler optimized, private Firefox fork",
                            GroupName = "Browsers",
                            WinGetPackageId = "Alex313031.Mercury",
                            WebsiteUrl = "https://thorium.rocks/mercury"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-firefox",
                            Name = "Firefox",
                            Description = "Popular web browser known for privacy and customization",
                            GroupName = "Browsers",
                            WinGetPackageId = "Mozilla.Firefox",
                            WebsiteUrl = "https://www.mozilla.org/firefox/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-chrome",
                            Name = "Chrome",
                            Description = "Google's web browser with sync and extension support",
                            GroupName = "Browsers",
                            WinGetPackageId = "Google.Chrome",
                            WebsiteUrl = "https://www.google.com/chrome/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-ungoogled-chromium",
                            Name = "Ungoogled Chromium",
                            Description = "Chromium-based browser with privacy enhancements",
                            GroupName = "Browsers",
                            WinGetPackageId = "Eloston.Ungoogled-Chromium",
                            WebsiteUrl = "https://ungoogled-software.github.io/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-brave",
                            Name = "Brave",
                            Description = "Privacy-focused browser with built-in ad blocking",
                            GroupName = "Browsers",
                            WinGetPackageId = "Brave.Brave",
                            WebsiteUrl = "https://brave.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-opera",
                            Name = "Opera",
                            Description = "Feature-rich web browser with built-in VPN and ad blocker",
                            GroupName = "Browsers",
                            WinGetPackageId = "Opera.Opera",
                            WebsiteUrl = "https://www.opera.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-opera-gx",
                            Name = "Opera GX",
                            Description = "Gaming-oriented version of Opera with unique features",
                            GroupName = "Browsers",
                            WinGetPackageId = "Opera.OperaGX",
                            WebsiteUrl = "https://www.opera.com/gx"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-arc",
                            Name = "Arc Browser",
                            Description = "Innovative browser with a focus on design and user experience",
                            GroupName = "Browsers",
                            WinGetPackageId = "TheBrowserCompany.Arc",
                            WebsiteUrl = "https://arc.net/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-tor",
                            Name = "Tor Browser",
                            Description = "Privacy-focused browser that routes traffic through the Tor network",
                            GroupName = "Browsers",
                            WinGetPackageId = "TorProject.TorBrowser",
                            WebsiteUrl = "https://www.torproject.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vivaldi",
                            Name = "Vivaldi",
                            Description = "Highly customizable browser with a focus on user control",
                            GroupName = "Browsers",
                            WinGetPackageId = "Vivaldi.Vivaldi",
                            WebsiteUrl = "https://vivaldi.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-waterfox",
                            Name = "Waterfox",
                            Description = "Firefox-based browser with a focus on privacy and customization",
                            GroupName = "Browsers",
                            WinGetPackageId = "Waterfox.Waterfox",
                            WebsiteUrl = "https://www.waterfox.net/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-zen",
                            Name = "Zen Browser",
                            Description = "Privacy-focused browser with built-in ad blocking",
                            GroupName = "Browsers",
                            WinGetPackageId = "Zen-Team.Zen-Browser",
                            WebsiteUrl = "https://zen-browser.app/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-mullvad",
                            Name = "Mullvad Browser",
                            Description = "Privacy-focused browser designed to minimize tracking and fingerprints",
                            GroupName = "Browsers",
                            WinGetPackageId = "MullvadVPN.MullvadBrowser",
                            WebsiteUrl = "https://mullvad.net/en/browser"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-pale-moon",
                            Name = "Pale Moon Browser",
                            Description = "Open Source, Goanna-based web browser focusing on efficiency and customization",
                            GroupName = "Browsers",
                            WinGetPackageId = "MoonchildProductions.PaleMoon",
                            WebsiteUrl = "https://www.palemoon.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-maxthon",
                            Name = "Maxthon Browser",
                            Description = "Privacy focused browser with built-in ad blocking and VPN",
                            GroupName = "Browsers",
                            WinGetPackageId = "Maxthon.Maxthon",
                            WebsiteUrl = "https://www.maxthon.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-floorp",
                            Name = "Floorp",
                            Description = "Privacy focused browser with strong tracking protection",
                            GroupName = "Browsers",
                            WinGetPackageId = "Ablaze.Floorp",
                            WebsiteUrl = "https://floorp.app/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-duckduckgo",
                            Name = "DuckDuckGo",
                            Description = "Privacy-focused search engine with a browser extension",
                            GroupName = "Browsers",
                            WinGetPackageId = "DuckDuckGo.DesktopBrowser",
                            WebsiteUrl = "https://duckduckgo.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-librewolf",
                            Name = "LibreWolf",
                            Description = "A custom version of Firefox, focused on privacy, security and freedom",
                            GroupName = "Browsers",
                            WinGetPackageId = "LibreWolf.LibreWolf",
                            WebsiteUrl = "https://librewolf.net/"
                        }
                    }
                };
            }
        }
    }
}
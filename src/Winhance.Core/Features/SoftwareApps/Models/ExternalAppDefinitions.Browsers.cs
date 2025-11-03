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
                            Category = "Browsers",
                            WebsiteUrl = "https://developer.microsoft.com/en-us/microsoft-edge/webview2/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-thorium",
                            Name = "Thorium",
                            Description = "Chromium-based browser with enhanced privacy features",
                            GroupName = "Browsers",
                            WinGetPackageId = "Alex313031.Thorium",
                            Category = "Browsers",
                            WebsiteUrl = "https://thorium.rocks/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-thorium-avx2",
                            Name = "Thorium AVX2",
                            Description = "Chromium-based browser with enhanced privacy features",
                            GroupName = "Browsers",
                            WinGetPackageId = "Alex313031.Thorium.AVX2",
                            Category = "Browsers",
                            WebsiteUrl = "https://thorium.rocks/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-mercury",
                            Name = "Mercury",
                            Description = "Compiler optimized, private Firefox fork",
                            GroupName = "Browsers",
                            WinGetPackageId = "Alex313031.Mercury",
                            Category = "Browsers",
                            WebsiteUrl = "https://thorium.rocks/mercury"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-firefox",
                            Name = "Firefox",
                            Description = "Popular web browser known for privacy and customization",
                            GroupName = "Browsers",
                            WinGetPackageId = "Mozilla.Firefox",
                            Category = "Browsers",
                            WebsiteUrl = "https://www.mozilla.org/firefox/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-chrome",
                            Name = "Chrome",
                            Description = "Google's web browser with sync and extension support",
                            GroupName = "Browsers",
                            WinGetPackageId = "Google.Chrome",
                            Category = "Browsers",
                            WebsiteUrl = "https://www.google.com/chrome/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-ungoogled-chromium",
                            Name = "Ungoogled Chromium",
                            Description = "Chromium-based browser with privacy enhancements",
                            GroupName = "Browsers",
                            WinGetPackageId = "Eloston.Ungoogled-Chromium",
                            Category = "Browsers",
                            WebsiteUrl = "https://ungoogled-software.github.io/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-brave",
                            Name = "Brave",
                            Description = "Privacy-focused browser with built-in ad blocking",
                            GroupName = "Browsers",
                            WinGetPackageId = "Brave.Brave",
                            Category = "Browsers",
                            WebsiteUrl = "https://brave.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-opera",
                            Name = "Opera",
                            Description = "Feature-rich web browser with built-in VPN and ad blocker",
                            GroupName = "Browsers",
                            WinGetPackageId = "Opera.Opera",
                            Category = "Browsers",
                            WebsiteUrl = "https://www.opera.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-opera-gx",
                            Name = "Opera GX",
                            Description = "Gaming-oriented version of Opera with unique features",
                            GroupName = "Browsers",
                            WinGetPackageId = "Opera.OperaGX",
                            Category = "Browsers",
                            WebsiteUrl = "https://www.opera.com/gx"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-arc",
                            Name = "Arc Browser",
                            Description = "Innovative browser with a focus on design and user experience",
                            GroupName = "Browsers",
                            WinGetPackageId = "TheBrowserCompany.Arc",
                            Category = "Browsers",
                            WebsiteUrl = "https://arc.net/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-tor",
                            Name = "Tor Browser",
                            Description = "Privacy-focused browser that routes traffic through the Tor network",
                            GroupName = "Browsers",
                            WinGetPackageId = "TorProject.TorBrowser",
                            Category = "Browsers",
                            WebsiteUrl = "https://www.torproject.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vivaldi",
                            Name = "Vivaldi",
                            Description = "Highly customizable browser with a focus on user control",
                            GroupName = "Browsers",
                            WinGetPackageId = "Vivaldi.Vivaldi",
                            Category = "Browsers",
                            WebsiteUrl = "https://vivaldi.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-waterfox",
                            Name = "Waterfox",
                            Description = "Firefox-based browser with a focus on privacy and customization",
                            GroupName = "Browsers",
                            WinGetPackageId = "Waterfox.Waterfox",
                            Category = "Browsers",
                            WebsiteUrl = "https://www.waterfox.net/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-zen",
                            Name = "Zen Browser",
                            Description = "Privacy-focused browser with built-in ad blocking",
                            GroupName = "Browsers",
                            WinGetPackageId = "Zen-Team.Zen-Browser",
                            Category = "Browsers",
                            WebsiteUrl = "https://zen-browser.app/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-mullvad",
                            Name = "Mullvad Browser",
                            Description = "Privacy-focused browser designed to minimize tracking and fingerprints",
                            GroupName = "Browsers",
                            WinGetPackageId = "MullvadVPN.MullvadBrowser",
                            Category = "Browsers",
                            WebsiteUrl = "https://mullvad.net/en/browser"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-pale-moon",
                            Name = "Pale Moon Browser",
                            Description = "Open Source, Goanna-based web browser focusing on efficiency and customization",
                            GroupName = "Browsers",
                            WinGetPackageId = "MoonchildProductions.PaleMoon",
                            Category = "Browsers",
                            WebsiteUrl = "https://www.palemoon.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-maxthon",
                            Name = "Maxthon Browser",
                            Description = "Privacy focused browser with built-in ad blocking and VPN",
                            GroupName = "Browsers",
                            WinGetPackageId = "Maxthon.Maxthon",
                            Category = "Browsers",
                            WebsiteUrl = "https://www.maxthon.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-floorp",
                            Name = "Floorp",
                            Description = "Privacy focused browser with strong tracking protection",
                            GroupName = "Browsers",
                            WinGetPackageId = "Ablaze.Floorp",
                            Category = "Browsers",
                            WebsiteUrl = "https://floorp.app/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-duckduckgo",
                            Name = "DuckDuckGo",
                            Description = "Privacy-focused search engine with a browser extension",
                            GroupName = "Browsers",
                            WinGetPackageId = "DuckDuckGo.DesktopBrowser",
                            Category = "Browsers",
                            WebsiteUrl = "https://duckduckgo.com/"
                        }
                    }
                };
            }
        }
    }
}
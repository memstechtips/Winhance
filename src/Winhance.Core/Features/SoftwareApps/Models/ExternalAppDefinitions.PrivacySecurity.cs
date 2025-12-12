using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    public static partial class ExternalAppDefinitions
    {
        public static class PrivacySecurity
        {
            public static ItemGroup GetPrivacySecurity()
            {
                return new ItemGroup
                {
                    Name = "Privacy & Security",
                    FeatureId = FeatureIds.ExternalApps,
                    Items = new List<ItemDefinition>
                    {
                        new ItemDefinition
                        {
                            Id = "external-app-malwarebytes",
                            Name = "Malwarebytes",
                            Description = "Anti-malware software for Windows",
                            GroupName = "Privacy & Security",
                            WinGetPackageId = "Malwarebytes.Malwarebytes",
                            Category = "Privacy & Security",
                            WebsiteUrl = "https://www.malwarebytes.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-malwarebytes-adwcleaner",
                            Name = "Malwarebytes AdwCleaner",
                            Description = "Adware removal tool for Windows",
                            GroupName = "Privacy & Security",
                            WinGetPackageId = "Malwarebytes.AdwCleaner",
                            Category = "Privacy & Security",
                            WebsiteUrl = "https://www.malwarebytes.com/adwcleaner"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-malwarebytes-firewall-control",
                            Name = "Malwarebytes WFC",
                            Description = "Malwarebytes Windows Firewall Control application",
                            GroupName = "Privacy & Security",
                            WinGetPackageId = "BiniSoft.WindowsFirewallControl",
                            Category = "Privacy & Security",
                            WebsiteUrl = "https://www.binisoft.org/wfc"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-onionshare",
                            Name = "OnionShare",
                            Description = "Securely and anonymously share files, host websites, and chat via Tor network",
                            GroupName = "Privacy & Security",
                            WinGetPackageId = "OnionShare.OnionShare",
                            Category = "Privacy & Security",
                            WebsiteUrl = "https://onionshare.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-sniffnet",
                            Name = "Sniffnet",
                            Description = "Network monitoring tool to analyze your internet traffic",
                            GroupName = "Privacy & Security",
                            Category = "Privacy & Security",
                            WebsiteUrl = "https://sniffnet.net/",
                            CustomProperties = new Dictionary<string, object>
                            {
                                { "DownloadUrl_arm64", "https://github.com/GyulyVGC/sniffnet/releases/latest/download/Sniffnet_Windows_arm64.msi" },
                                { "DownloadUrl_x64", "https://github.com/GyulyVGC/sniffnet/releases/latest/download/Sniffnet_Windows_x64.msi" },
                                { "DownloadUrl_x86", "https://github.com/GyulyVGC/sniffnet/releases/latest/download/Sniffnet_Windows_x86.msi" },
                                { "RequiresDirectDownload", true }
                            }
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-teleguard",
                            Name = "Teleguard",
                            Description = "Secure messaging app with end-to-end encryption",
                            GroupName = "Privacy & Security",
                            Category = "Privacy & Security",
                            WebsiteUrl = "https://teleguard.com/en",
                            CustomProperties = new Dictionary<string, object>
                            {
                                { "DownloadUrl", "https://pub.teleguard.com/teleguard-desktop-latest.exe" },
                                { "RequiresDirectDownload", true }
                            }
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-shutup10",
                            Name = "O&O ShutUp10++",
                            Description = "Free antispy tool for Windows 10 and 11",
                            GroupName = "Privacy & Security",
                            WinGetPackageId = "OO-Software.ShutUp10",
                            Category = "Privacy & Security",
                            WebsiteUrl = "https://www.oo-software.com/en/shutup10"
                        }
                    }
                };
            }
        }
    }
}
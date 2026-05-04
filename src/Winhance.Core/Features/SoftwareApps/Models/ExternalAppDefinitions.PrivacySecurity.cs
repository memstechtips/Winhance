using System.Collections.Generic;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models;

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
                        RegistryDisplayName = "Malwarebytes version {version}",
                        GroupName = "Privacy & Security",
                        WinGetPackageId = ["Malwarebytes.Malwarebytes"],
                        ChocoPackageId = "malwarebytes",
                        WebsiteUrl = "https://www.malwarebytes.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/4/47/Malwarebytes_Logo_Symbol.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-malwarebytes-adwcleaner",
                        Name = "Malwarebytes AdwCleaner",
                        Description = "Adware removal tool for Windows",
                        GroupName = "Privacy & Security",
                        WinGetPackageId = ["Malwarebytes.AdwCleaner"],
                        ChocoPackageId = "adwcleaner",
                        WebsiteUrl = "https://www.malwarebytes.com/adwcleaner",
                        IconSources = [
                            // AdwCleaner has no separate logo asset — uses parent Malwarebytes brand mark.
                            "https://upload.wikimedia.org/wikipedia/commons/4/47/Malwarebytes_Logo_Symbol.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-malwarebytes-firewall-control",
                        Name = "Windows Firewall Control",
                        Description = "Malwarebytes Windows Firewall Control application",
                        GroupName = "Privacy & Security",
                        WinGetPackageId = ["BiniSoft.WindowsFirewallControl"],
                        ChocoPackageId = "windowsfirewallcontrol",
                        // Upstream winget manifest passes `-run -close -install -update`, which causes the
                        // installer to dump files into a `-update` subfolder of CWD when no prior install exists.
                        WinGetInstallerOverride = "-run -close -install",
                        WebsiteUrl = "https://www.binisoft.org/wfc",
                        IconSources = [
                            // Malwarebytes acquired BiniSoft — use the parent-brand symbol
                            // to match the other Malwarebytes products in this category.
                            "https://upload.wikimedia.org/wikipedia/commons/4/47/Malwarebytes_Logo_Symbol.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-onionshare",
                        Name = "OnionShare",
                        Description = "Securely and anonymously share files, host websites, and chat via Tor network",
                        GroupName = "Privacy & Security",
                        WinGetPackageId = ["OnionShare.OnionShare"],
                        ChocoPackageId = "onionshare",
                        WebsiteUrl = "https://onionshare.org/",
                        IconSources = [
                            "https://raw.githubusercontent.com/onionshare/onionshare/main/docs/source/_static/logo.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-sniffnet",
                        Name = "Sniffnet",
                        Description = "Network monitoring tool to analyze your internet traffic",
                        GroupName = "Privacy & Security",
                        WebsiteUrl = "https://sniffnet.net/",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrlArm64 = "https://github.com/GyulyVGC/sniffnet/releases/latest/download/Sniffnet_Windows_arm64.msi",
                            DownloadUrlX64 = "https://github.com/GyulyVGC/sniffnet/releases/latest/download/Sniffnet_Windows_x64.msi",
                            DownloadUrlX86 = "https://github.com/GyulyVGC/sniffnet/releases/latest/download/Sniffnet_Windows_x86.msi",
                            RequiresDirectDownload = true,
                        },
                        IconSources = [
                            "https://raw.githubusercontent.com/GyulyVGC/sniffnet/main/resources/logos/raw/icon.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-teleguard",
                        Name = "TeleGuard",
                        Description = "Secure messaging app with end-to-end encryption",
                        GroupName = "Privacy & Security",
                        WebsiteUrl = "https://teleguard.com/en",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://pub.teleguard.com/teleguard-desktop-latest.exe",
                            RequiresDirectDownload = true,
                        },
                        // Vendor doesn't expose a stable raw logo URL — the on-page logo is
                        // a base64 data URI on teleguard.com. Embed it directly here so the
                        // resolver can decode it at first run (Layer 2b's data: branch).
                        IconSources = [
                            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABwAAAAcCAMAAABF0y+mAAAAS1BMVEW1M2voyNWzKmexIGK0NGz///+1NGy1M2y0Mmy0NGy0MGrJcJX9+fvx2uPgrsO9SHvRiqflvM3Ofp/78vb36u/EX4r04unZnLW6PXSwiXUVAAAACnRSTlPx////i///igeMVuddrgAAASdJREFUKJF1k9m2gyAMRUkoagmzOPz/l96AqNjbngddYSeRDIppFMNXiXES421qjSytT3sUV5xGTymEtOzYcMf8BqtRarYxDCdtDMkyOTSDxx7KNKtOdsEbom+n4KAmsF6fUO9nnKNl34oB5bsVolPK2FwiYAUKTOeAB9R+ZVckfrpAm6WN3eJ+QOTbmIRIWUGEEGA35U66QumMMoQarYohAdkFOG/CA8aSRmLxycGFmEre0CA7qkz1pSI46+IH5NKW3PXhSgtHA3zKyqzNYyXdQ6YEbigVVeMJ+UDynanEzltrwgW5OIko7dncJ1Q5EsUyVcLhH+Qe16mka2QPWMtY5PAVmjm7He81wQ0uRRf8uWBtnrLThfr1+7LW4vUbvsT0/vU7vKc/87QWsGV1GD0AAAAASUVORK5CYII=",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-shutup10",
                        Name = "O&O ShutUp10++",
                        Description = "Free antispy tool for Windows 10 and 11",
                        GroupName = "Privacy & Security",
                        WinGetPackageId = ["OO-Software.ShutUp10"],
                        ChocoPackageId = "shutup10",
                        WebsiteUrl = "https://www.oo-software.com/en/shutup10",
                        IconSources = [
                            "https://www.oo-software.com/oocontent/uploads/oosu10.png",
                        ],
                    }
                }
            };
        }
    }
}

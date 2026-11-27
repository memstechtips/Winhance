using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.SoftwareApps.Models;
namespace Winhance.Core.Features.SoftwareApps.Catalogs;

public static partial class ExternalAppDefinitions
{
    public static class OtherUtilities
    {
        public static ItemGroup GetOtherUtilities()
        {
            return new ItemGroup
            {
                Name = "Other Utilities",
                FeatureId = FeatureIds.ExternalApps,
                Items = new List<ItemDefinition>
                {
                    new ItemDefinition
                    {
                        Id = "external-app-ccleaner",
                        Name = "CCleaner",
                        Description = "System optimization and cleaning tool",
                        GroupName = "Other Utilities",
                        RegistrySubKeyName = "CCleaner {version}",
                        RegistryDisplayName = "CCleaner {version}",
                        WinGetPackageId = ["Piriform.CCleaner"],
                        ChocoPackageId = "ccleaner",
                        MsStoreId = "XPFCWP0SQWXM3V",
                        WebsiteUrl = "https://www.ccleaner.com/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-snappy-driver-installer",
                        Name = "Snappy Driver Installer Origin",
                        Description = "Offline-capable driver installer and updater for Windows",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["GlennDelahoy.SnappyDriverInstallerOrigin"],
                        ChocoPackageId = "sdio",
                        WebsiteUrl = "https://www.snappy-driver-installer.org/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-nvidia-control-panel",
                        Name = "NVIDIA Control Panel",
                        Description = "NVIDIA display, 3D and per-app graphics settings",
                        GroupName = "Other Utilities",
                        MsStoreId = "9NF8H0H7WMLT",
                        AppxPackageName = ["NVIDIACorp.NVIDIAControlPanel"],
                        WebsiteUrl = "https://www.nvidia.com/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-wise-disk-cleaner",
                        Name = "Wise Disk Cleaner",
                        Description = "Free disk cleanup and defragmentation utility",
                        GroupName = "Other Utilities",
                        RegistrySubKeyName = "Wise Disk Cleaner_is1",
                        RegistryDisplayName = "Wise Disk Cleaner",
                        WinGetPackageId = ["WiseCleaner.WiseDiskCleaner"],
                        MsStoreId = "XP9CW3GPQQS852", // MS Store package
                        WebsiteUrl = "https://www.wisecleaner.com/wise-disk-cleaner.html",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-wise-registry-cleaner",
                        Name = "Wise Registry Cleaner",
                        Description = "Registry cleaning and optimization tool",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["WiseCleaner.WiseRegistryCleaner"],
                        MsStoreId = "XPDLS1XBTXVPP4", // MS Store package
                        WebsiteUrl = "https://www.wisecleaner.com/wise-registry-cleaner.html",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-unigetui",
                        Name = "UniGetUI",
                        Description = "Universal package manager interface supporting WinGet, Chocolatey, and more",
                        GroupName = "Other Utilities",
                        // Devolutions took the project over in 2026 and winget dropped the MartiCliment id; the old
                        // id stays so an existing install still reads as installed.
                        WinGetPackageId = ["Devolutions.UniGetUI", "MartiCliment.UniGetUI"],
                        ChocoPackageId = "unigetui",
                        MsStoreId = "XPFFTQ032PTPHF",
                        WebsiteUrl = "https://devolutions.net/unigetui/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-qbittorrent",
                        Name = "qBittorrent",
                        Description = "Free open-source BitTorrent client",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["qBittorrent.qBittorrent"],
                        ChocoPackageId = "qbittorrent",
                        WebsiteUrl = "https://www.qbittorrent.org/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-internet-download-manager",
                        Name = "Internet Download Manager",
                        Description = "Paid download manager with segmented downloads, resume and scheduling",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["Tonec.InternetDownloadManager"],
                        ChocoPackageId = "internet-download-manager",
                        WebsiteUrl = "https://www.internetdownloadmanager.com/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-openrgb",
                        Name = "OpenRGB",
                        Description = "Open source RGB lighting control software",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["OpenRGB.OpenRGB"],
                        ChocoPackageId = "openrgb",
                        WebsiteUrl = "https://openrgb.org/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-fancontrol",
                        Name = "FanControl",
                        Description = "Controls fan speeds from temperature sensors with custom curves",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["Rem0o.FanControl"],
                        ChocoPackageId = "fancontrol",
                        WebsiteUrl = "https://getfancontrol.com/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-openaudible",
                        Name = "OpenAudible",
                        Description = "Audiobook manager and converter for Audible files",
                        RegistryDisplayName = "OpenAudible {version}",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["OpenAudible.OpenAudible"],
                        ChocoPackageId = "openaudible",
                        WebsiteUrl = "https://openaudible.org/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-naps2",
                        Name = "NAPS2",
                        Description = "Document scanning application with OCR support",
                        GroupName = "Other Utilities",
                        AppxPackageName = ["NAPS2Software.NAPS2-NotAnotherPDFScanner"],
                        WinGetPackageId = ["Cyanfish.NAPS2"],
                        ChocoPackageId = "naps2",
                        MsStoreId = "9N3QQ9W0B23Q",
                        WebsiteUrl = "https://www.naps2.com/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-iobit-uninstaller",
                        Name = "IObit Uninstaller",
                        Description = "Removes apps, leftover files, and stubborn browser plug-ins",
                        GroupName = "Other Utilities",
                        RegistrySubKeyName = "IObitUninstall",
                        RegistryDisplayName = "IObit Uninstaller {version}",
                        WinGetPackageId = ["IObit.Uninstaller"],
                        ChocoPackageId = "iobit-uninstaller",
                        MsStoreId = "XP8K2SCQWD27VT",
                        WebsiteUrl = "https://www.iobit.com/en/advanceduninstaller.php",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-revo-uninstaller",
                        Name = "Revo Uninstaller",
                        Description = "Uninstaller that scans for and removes leftover files and registry entries",
                        GroupName = "Other Utilities",
                        RegistrySubKeyName = "{A28DBDA2-3CC7-4ADC-8BFE-66D7743C6C97}_is1",
                        RegistryDisplayName = "Revo Uninstaller {version}",
                        WinGetPackageId = ["RevoUninstaller.RevoUninstaller"],
                        ChocoPackageId = "revo-uninstaller",
                        MsStoreId = "XPFFVD4CMXN8VN",
                        WebsiteUrl = "https://www.revouninstaller.com/products/revo-uninstaller-free/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-virtualbox",
                        Name = "Oracle VirtualBox",
                        Description = "Free and open-source virtualization software",
                        RegistryDisplayName = "Oracle VirtualBox {version}",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["Oracle.VirtualBox"],
                        ChocoPackageId = "virtualbox",
                        WebsiteUrl = "https://www.virtualbox.org/",
                    }
                }
            };
        }
    }
}

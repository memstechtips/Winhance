using Winhance.Core.Features.Common.Constants;
namespace Winhance.Core.Features.SoftwareApps.Models;

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
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/en/4/4a/CCleaner_logo_2013.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-snappy-driver-installer",
                        Name = "Snappy Driver Installer Origin",
                        Description = "Driver installer and updater",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["GlennDelahoy.SnappyDriverInstallerOrigin"],
                        ChocoPackageId = "sdio",
                        WebsiteUrl = "https://www.snappy-driver-installer.org/",
                        IconSources = [
                            "https://www.glenn.delahoy.com/wp-content/uploads/2018/08/logo-150x150.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-wise-disk-cleaner",
                        Name = "Wise Disk Cleaner",
                        Description = "Free Disk Cleanup and Defragment Tool",
                        GroupName = "Other Utilities",
                        RegistrySubKeyName = "Wise Disk Cleaner_is1",
                        RegistryDisplayName = "Wise Disk Cleaner",
                        WinGetPackageId = ["WiseCleaner.WiseDiskCleaner"],
                        MsStoreId = "XP9CW3GPQQS852", // MS Store package
                        WebsiteUrl = "https://www.wisecleaner.com/wise-disk-cleaner.html",
                        IconSources = [
                            "https://www.wisecleaner.com/static/img/product/wise-disk-cleaner/wisefolderhider_icon.png",
                            "https://www.wisecleaner.com/favicon.ico",
                        ],
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
                        IconSources = [
                            "https://www.wisecleaner.com/static/img/product/wise-registry-cleaner/wiseregistrycleaner_icon.png",
                            "https://www.wisecleaner.com/favicon.ico",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-unigetui",
                        Name = "UniGetUI",
                        Description = "Universal package manager interface supporting WinGet, Chocolatey, and more",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["MartiCliment.UniGetUI"],
                        ChocoPackageId = "wingetui",
                        WebsiteUrl = "https://www.marticliment.com/unigetui/",
                        IconSources = [
                            "https://raw.githubusercontent.com/marticliment/UniGetUI/main/media/icon.png",
                        ],
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
                        IconSources = [
                            "https://raw.githubusercontent.com/CalcProgrammer1/OpenRGB/master/Documentation/Images/OpenRGB.png",
                        ],
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
                        IconSources = [
                            "https://openaudible.org/icons/512x512.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-naps2",
                        Name = "NAPS2",
                        Description = "Document scanning application with OCR support",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["Cyanfish.NAPS2"],
                        ChocoPackageId = "naps2",
                        WebsiteUrl = "https://www.naps2.com/",
                        IconSources = [
                            "https://raw.githubusercontent.com/cyanfish/naps2/master/NAPS2.Lib/Icons/favicon.ico",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-iobit-uninstaller",
                        Name = "IObit Uninstaller",
                        Description = "Completely Uninstall Unwanted Software, Windows Apps & Browser Plug-ins",
                        GroupName = "Other Utilities",
                        RegistrySubKeyName = "IObitUninstall",
                        RegistryDisplayName = "IObit Uninstaller {version}",
                        WinGetPackageId = ["IObit.Uninstaller"],
                        ChocoPackageId = "iobit-uninstaller",
                        WebsiteUrl = "https://www.iobit.com/en/advanceduninstaller.php",
                        IconSources = [
                            "https://www.iobit.com/tpl/images/product-icons/iu_96.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-revo-uninstaller",
                        Name = "Revo Uninstaller",
                        Description = "Revo Uninstaller helps you to uninstall software and remove unwanted programs easily.",
                        GroupName = "Other Utilities",
                        RegistrySubKeyName = "{A28DBDA2-3CC7-4ADC-8BFE-66D7743C6C97}_is1",
                        RegistryDisplayName = "Revo Uninstaller {version}",
                        WinGetPackageId = ["RevoUninstaller.RevoUninstaller"],
                        ChocoPackageId = "revo-uninstaller",
                        WebsiteUrl = "https://www.revouninstaller.com/products/revo-uninstaller-free/",
                        IconSources = [
                            "https://www.revouninstaller.com/favicon.ico",
                            "https://f057a20f961f56a72089-b74530d2d26278124f446233f95622ef.ssl.cf1.rackcdn.com/site/revo-uninstaller-logo-white.png",
                        ],
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
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/f/ff/VirtualBox_2024_Logo.svg/256px-VirtualBox_2024_Logo.svg.png",
                            "https://upload.wikimedia.org/wikipedia/commons/f/f2/VirtualBox_logo_64px.png",
                        ],
                    }
                }
            };
        }
    }
}

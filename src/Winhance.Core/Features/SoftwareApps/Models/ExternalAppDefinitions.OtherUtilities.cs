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
                        WebsiteUrl = "https://www.ccleaner.com/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-snappy-driver-installer",
                        Name = "Snappy Driver Installer Origin",
                        Description = "Driver installer and updater",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["GlennDelahoy.SnappyDriverInstallerOrigin"],
                        ChocoPackageId = "sdio",
                        WebsiteUrl = "https://www.snappy-driver-installer.org/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-wise-disk-cleaner",
                        Name = "Wise Disk Cleaner",
                        Description = "Free Disk Cleanup and Defragment Tool",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["WiseCleaner.WiseDiskCleaner"],
                        MsStoreId = "XP9CW3GPQQS852", // MS Store package
                        WebsiteUrl = "https://www.wisecleaner.com/wise-disk-cleaner.html"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-wise-registry-cleaner",
                        Name = "Wise Registry Cleaner",
                        Description = "Registry cleaning and optimization tool",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["WiseCleaner.WiseRegistryCleaner"],
                        MsStoreId = "XPDLS1XBTXVPP4", // MS Store package
                        WebsiteUrl = "https://www.wisecleaner.com/wise-registry-cleaner.html"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-unigetui",
                        Name = "UniGetUI",
                        Description = "Universal package manager interface supporting WinGet, Chocolatey, and more",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["MartiCliment.UniGetUI"],
                        ChocoPackageId = "wingetui",
                        WebsiteUrl = "https://www.marticliment.com/unigetui/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-openrgb",
                        Name = "OpenRGB",
                        Description = "Open source RGB lighting control software",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["OpenRGB.OpenRGB"],
                        ChocoPackageId = "openrgb",
                        WebsiteUrl = "https://openrgb.org/"
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
                        WebsiteUrl = "https://openaudible.org/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-naps2",
                        Name = "NAPS2",
                        Description = "Document scanning application with OCR support",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["Cyanfish.NAPS2"],
                        ChocoPackageId = "naps2",
                        WebsiteUrl = "https://www.naps2.com/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-iobit-uninstaller",
                        Name = "IObit Uninstaller",
                        Description = "Completely Uninstall Unwanted Software, Windows Apps & Browser Plug-ins",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["IObit.Uninstaller"],
                        ChocoPackageId = "iobit-uninstaller",
                        WebsiteUrl = "https://www.iobit.com/en/advanceduninstaller.php"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-revo-uninstaller",
                        Name = "Revo Uninstaller",
                        Description = "Revo Uninstaller helps you to uninstall software and remove unwanted programs easily.",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["RevoUninstaller.RevoUninstaller"],
                        ChocoPackageId = "revo-uninstaller",
                        WebsiteUrl = "https://www.revouninstaller.com/products/revo-uninstaller-free/"
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
                        WebsiteUrl = "https://www.virtualbox.org/"
                    }
                }
            };
        }
    }
}

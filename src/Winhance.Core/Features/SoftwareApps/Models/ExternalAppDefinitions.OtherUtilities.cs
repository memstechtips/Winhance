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
                        Description = "Offline-capable driver installer and updater for Windows",
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
                        Description = "Free disk cleanup and defragmentation utility",
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
                        MsStoreId = "XPFFTQ032PTPHF",
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
                            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABwAAAAcCAYAAAByDd+UAAACz0lEQVR4Ae3WA4xcURiG4a+2bXu9Ue2ug9q2bTeobdu2bdu27Xb26/lzc2bHM4u4Sd7VxZN7cGcR1DXcozJnzlynRnesabkAj2quwR+pwHjcSds052o55ul93J8ANJ22CDz7FTxMcIlqvKqzqokqWOVrApPNw185N1Zgw1I48O4J+NcEPqM9qLE8BOMxEfE2MxGRZm+MwCZBuMibIF9EgZdVmxyAaTXI7CovjXoO+qZMMyNyNchDKkHfCeoYzGOLmYIkJkiRaYJHIACvc11BzgcF5UFBrYdVwCa2T6exyJZGh+szmy/yuAVl3tgf5GjJHpWnXGIGHWDsb8RxlBXsEgwPD0+8pzzIBqo2Kg3PVx0G31wH2/TDuYRAT0m2hSwUjWkIXGK0otsfl6Dsp8+lQAZJ1vDnCXA4L/I3wawg7jd6uZn58uULcgrKYmEJ0DKNTwnEG2crT55EI8agS7dVDygj4RSsFS/5QqYAzaWFURZQjjkFu7VdB36nwypVGOcaREY6yDXYovc2PCXNfbCoVLkxTkF5fMKP1oWo6nAlypqcgm1WEZtodNwIV4xcDqlM8BuUFkRDqnaqQaq5rIyA3XZYmhon0MY4BeNUCxiFL71PAF5OQUmexEA0NIqCmXCCxAvWQtNH+txUiRv+RE4ZNlUN0haWJ3ez8fU8miHVGjNmQqQeIvOWSJ6AdIbKcbcggNQ70cYhtgmHKMf1ufLqyoAzdIiGrqW8SNyCevG8xiLBrMAeGHrR9tzUGHFVg2a06mPqz0aPQP0SsMTu4TMdfbLLjQU0o0VfOxxKt6Ceo4s4LiBnYT1lCGUlCyzJz/K3zNhpgElP6DmO1b8YXjKUZVJXYbZs2Rwmq1WGVm+BWIG6goUK/nEGlihR/I8+L05AWW1yU2egl5fXlzgF/4NxPoeSn59f9+LFi++3xeRvcizOQZ23t/dyWbGS/Bzd6/8BBv6LPrD9Px8AAAAASUVORK5CYII=",
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
                        AppxPackageName = ["NAPS2Software.NAPS2-NotAnotherPDFScanner"],
                        WinGetPackageId = ["Cyanfish.NAPS2"],
                        ChocoPackageId = "naps2",
                        MsStoreId = "9N3QQ9W0B23Q",
                        WebsiteUrl = "https://www.naps2.com/",
                        IconSources = [
                            "https://raw.githubusercontent.com/cyanfish/naps2/master/NAPS2.Lib/Icons/favicon.ico",
                        ],
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
                        IconSources = [
                            "https://www.iobit.com/tpl/images/product-icons/iu_96.png",
                        ],
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
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/8/83/Revouninstallerpro_icon.png",
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
                            "https://upload.wikimedia.org/wikipedia/commons/d/d5/Virtualbox_logo.png",
                        ],
                    }
                }
            };
        }
    }
}

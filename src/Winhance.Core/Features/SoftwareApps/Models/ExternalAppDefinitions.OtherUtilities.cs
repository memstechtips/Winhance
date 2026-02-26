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
                        Id = "external-app-wise-registry-cleaner",
                        Name = "Wise Registry Cleaner",
                        Description = "Registry cleaning and optimization tool",
                        GroupName = "Other Utilities",
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
                        Id = "external-app-jdownloader",
                        Name = "JDownloader 2",
                        Description = "Download management tool",
                        GroupName = "Other Utilities",
                        WinGetPackageId = ["AppWork.JDownloader"],
                        ChocoPackageId = "jdownloader",
                        WebsiteUrl = "https://jdownloader.org/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-openaudible",
                        Name = "OpenAudible",
                        Description = "Audiobook manager and converter for Audible files",
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
                        Id = "external-app-virtualbox",
                        Name = "VirtualBox",
                        Description = "Free and open-source virtualization software",
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

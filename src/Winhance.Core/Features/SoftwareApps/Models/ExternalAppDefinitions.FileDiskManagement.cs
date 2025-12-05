using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    public static partial class ExternalAppDefinitions
    {
        public static class FileDiskManagement
        {
            public static ItemGroup GetFileDiskManagement()
            {
                return new ItemGroup
                {
                    Name = "File & Disk Management",
                    FeatureId = FeatureIds.ExternalApps,
                    Items = new List<ItemDefinition>
                    {
                        new ItemDefinition
                        {
                            Id = "external-app-windirstat",
                            Name = "WinDirStat",
                            Description = "Disk usage statistics viewer and cleanup tool",
                            GroupName = "File & Disk Management",
                            WinGetPackageId = "WinDirStat.WinDirStat",
                            Category = "File & Disk Management",
                            WebsiteUrl = "https://windirstat.net/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-wiztree",
                            Name = "WizTree",
                            Description = "Disk space analyzer with extremely fast scanning",
                            GroupName = "File & Disk Management",
                            WinGetPackageId = "AntibodySoftware.WizTree",
                            Category = "File & Disk Management",
                            WebsiteUrl = "https://www.diskanalyzer.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-treesize-free",
                            Name = "TreeSize Free",
                            Description = "Disk space manager",
                            GroupName = "File & Disk Management",
                            WinGetPackageId = "JAMSoftware.TreeSize.Free",
                            Category = "File & Disk Management",
                            WebsiteUrl = "https://www.jam-software.com/treesize_free"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-everything",
                            Name = "Everything",
                            Description = "Locate files and folders by name instantly",
                            GroupName = "File & Disk Management",
                            WinGetPackageId = "voidtools.Everything",
                            Category = "File & Disk Management",
                            WebsiteUrl = "https://www.voidtools.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-teracopy",
                            Name = "TeraCopy",
                            Description = "Copy files faster and more securely",
                            GroupName = "File & Disk Management",
                            WinGetPackageId = "CodeSector.TeraCopy",
                            Category = "File & Disk Management",
                            WebsiteUrl = "https://www.codesector.com/teracopy"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-file-converter",
                            Name = "File Converter",
                            Description = "Batch file converter for Windows",
                            GroupName = "File & Disk Management",
                            WinGetPackageId = "AdrienAllard.FileConverter",
                            Category = "File & Disk Management",
                            WebsiteUrl = "https://file-converter.io/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-crystal-disk-info",
                            Name = "Crystal Disk Info",
                            Description = "Hard drive health monitoring utility",
                            GroupName = "File & Disk Management",
                            WinGetPackageId = "WsSolInfor.CrystalDiskInfo",
                            Category = "File & Disk Management",
                            WebsiteUrl = "https://crystalmark.info/en/software/crystaldiskinfo/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-bulk-rename-utility",
                            Name = "Bulk Rename Utility",
                            Description = "File renaming software for Windows",
                            GroupName = "File & Disk Management",
                            WinGetPackageId = "TGRMNSoftware.BulkRenameUtility",
                            Category = "File & Disk Management",
                            WebsiteUrl = "https://www.bulkrenameutility.co.uk/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-iobit-unlocker",
                            Name = "IObit Unlocker",
                            Description = "Tool to unlock files that are in use by other processes",
                            GroupName = "File & Disk Management",
                            WinGetPackageId = "IObit.IObitUnlocker",
                            Category = "File & Disk Management",
                            WebsiteUrl = "https://www.iobit.com/en/iobit-unlocker.php"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-hibit-uninstaller",
                            Name = "HiBit Uninstaller",
                            Description = "Completely Uninstall Stubborn Software, Windows Apps & Browser Extension",
                            GroupName = "File & Disk Management",
                            WinGetPackageId = "HiBitSoftware.HiBitUninstaller",
                            Category = "File & Disk Management",
                            WebsiteUrl = "https://www.hibitsoft.ir/Uninstaller.html"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-ventoy",
                            Name = "Ventoy",
                            Description = "Open source tool to create bootable USB drive for ISO files",
                            GroupName = "File & Disk Management",
                            WinGetPackageId = "Ventoy.Ventoy",
                            Category = "File & Disk Management",
                            WebsiteUrl = "https://www.ventoy.net/"
                        }
                    }
                };
            }
        }
    }
}
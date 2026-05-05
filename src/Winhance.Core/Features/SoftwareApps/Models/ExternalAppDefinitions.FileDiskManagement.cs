using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Models;

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
                        WinGetPackageId = ["WinDirStat.WinDirStat"],
                        ChocoPackageId = "windirstat",
                        WebsiteUrl = "https://windirstat.net/",
                        IconSources = [
                            "https://raw.githubusercontent.com/windirstat/windirstat/master/windirstat/logos/logo_256px.png",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/5/57/WinDirStat_Logo_color.svg/250px-WinDirStat_Logo_color.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-wiztree",
                        Name = "WizTree",
                        Description = "Disk space analyzer with extremely fast scanning",
                        RegistryDisplayName = "WizTree {version}",
                        GroupName = "File & Disk Management",
                        WinGetPackageId = ["AntibodySoftware.WizTree"],
                        ChocoPackageId = "wiztree",
                        WebsiteUrl = "https://www.diskanalyzer.com/",
                        IconSources = [
                            "https://antibodysoftware-17031.kxcdn.com/images/wiztree200x.png",
                            "https://www.diskanalyzer.com/favicon.ico",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-treesize-free",
                        Name = "TreeSize Free",
                        Description = "Disk space manager",
                        RegistryDisplayName = "TreeSize Free {version}",
                        GroupName = "File & Disk Management",
                        WinGetPackageId = ["JAMSoftware.TreeSize.Free"],
                        ChocoPackageId = "treesizefree",
                        MsStoreId = "XPDDXV3SD1SB5K",
                        WebsiteUrl = "https://www.jam-software.com/treesize_free",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/b/bd/TreeSize-Icon-256.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-everything",
                        Name = "Everything",
                        Description = "Locate files and folders by name instantly",
                        RegistryDisplayName = "Everything {version} ({arch})",
                        GroupName = "File & Disk Management",
                        WinGetPackageId = ["voidtools.Everything"],
                        ChocoPackageId = "everything",
                        WebsiteUrl = "https://www.voidtools.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/5/52/Everything_%28software%29_logo.png",
                            "https://www.voidtools.com/favicon.ico",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-teracopy",
                        Name = "TeraCopy",
                        Description = "Copy files faster and more securely",
                        GroupName = "File & Disk Management",
                        WinGetPackageId = ["CodeSector.TeraCopy"],
                        ChocoPackageId = "teracopy",
                        MsStoreId = "XPDCCPPSK2XPQW",
                        WebsiteUrl = "https://www.codesector.com/teracopy",
                        // Icon resolved via MS Store CDN (Layer 2a). No usable
                        // catalog URL — vendor site only ships SVG.
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-file-converter",
                        Name = "File Converter",
                        Description = "Batch file converter for Windows",
                        GroupName = "File & Disk Management",
                        WinGetPackageId = ["AdrienAllard.FileConverter"],
                        ChocoPackageId = "file-converter",
                        WebsiteUrl = "https://file-converter.io/",
                        IconSources = [
                            "https://raw.githubusercontent.com/Tichau/FileConverter/master/Application/FileConverter/Resources/ApplicationIcon.ico",
                            "https://file-converter.io/favicon.ico",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-crystal-disk-info",
                        Name = "Crystal Disk Info",
                        Description = "Hard drive health monitoring utility",
                        GroupName = "File & Disk Management",
                        WinGetPackageId = ["WsSolInfor.CrystalDiskInfo"],
                        ChocoPackageId = "crystaldiskinfo",
                        WebsiteUrl = "https://crystalmark.info/en/software/crystaldiskinfo/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/4/42/%D0%9B%D0%BE%D0%B3%D0%BE%D1%82%D0%B8%D0%BF_%D0%BF%D1%80%D0%BE%D0%B3%D1%80%D0%B0%D0%BC%D0%BC%D1%8B_CrystalDiskInfo.png",
                            "https://crystalmark.info/favicon.ico",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-bulk-rename-utility",
                        Name = "Bulk Rename Utility",
                        Description = "File renaming software for Windows",
                        RegistryDisplayName = "Bulk Rename Utility {version} ({arch})",
                        GroupName = "File & Disk Management",
                        WinGetPackageId = ["TGRMNSoftware.BulkRenameUtility"],
                        ChocoPackageId = "bulkrenameutility",
                        WebsiteUrl = "https://www.bulkrenameutility.co.uk/",
                        // Vendor's `bru.svg` icon-only mark fits a square cell better
                        // than `brulogo.png` (wordmark). Embed a PNG render of the
                        // SVG since the resolver can't decode SVG directly.
                        IconSources = [
                            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAIAAAD8GO2jAAAABnRSTlMAAAAAAABupgeRAAADtUlEQVR4AbSTRXCbSRCFfV66LfNels7Lu6dlpjAzmJmZmRkrYsksShQLYmZm5jAzSZWntGtKJf3Gqkw9c/f7uueN7dZ+4uISC4uK7J7dASApL8jFwzEjM3vdzUKZXKVvXkFVmloPL79SQ5J+IiOvItjJwx68dQBgcfehiWl0crasUg4L6OChI0wEOD2WqBqOKtL4eIXae/n4rgkgr2kg66s3b4eERkTGRii10tr26vGLtUPnNX2L1Z3zspZZXv1UAQNUDQaV9Xtmqxx8oo4As1YA3KvVZRdujS1e75u50rYqQNzjyO86lGPY45uw19HJpapazg3AFcNdV9sYExdz7d78egHFHbvy2jdF8LfiGikb7g2wafdA2wYAscJ99m6H0Z6eXbjsBggWFbcfXCHA4HQjMtC1yjTNAnVjsbw+r7I2QzeUYwkQd3gmSexhnVtQotA2nhBIRJJyaVk1NwDPRigSXLq+IC0Xenp7RcUmQpExkPkbb78gRydX9oqqeiLii9yPORzNKyyZXrh0/a6pbWguIzsPVtwMAOgfFUFhkLGZS5dvmS5cN517qsWrpvJTdSgAQNGZHJfr4+7lhoHIGrp62wwo5suYtPo6awAGxBPqG5kl65qGYbvnv4K++9l57vISICDcA2UY8PzV28wa9SR00TRNA/PWS6A/KCRi7sJtNrV/JI8AL73+U9/EjbKTdbgNPDPmS7K0JneooW8OqVhvgCKIFb390f+whsCISq0EAM+M3Fewxq7Qme65zOx86w0s69RnhulyIHzz6yZ/AJi1rtH8VyvZe2aR+8xFMyA7t9AaYDmCdxgPPfgckVJJtwQAG5wTQItOnTdBuk4bAPoZHxW4HzSgTts6R81ugVmwtsofi6IFCdGPGcVa9E6cM6GLAwBrcq+q4Rjwh79dWUiaWo4CrAvr0UUjpGmd5QAQHPIM4dn2v/LB7xQSS8hW5Zqh4QXj4NwyAHIHn+5Hph7qmzH2TBlFyiHqz+NpKSSlfglQrR2mlk8/34cfN+2PgTukarIByFS1qAMfoiJyJ3VOGIVyFpIZwEamLgagLm6ApTXErDvGIQBWCQlKyNVSl7zBBiBR1HJat44uCQAWEga3ssY7dgnkoYumqarnAlhakzsBmkcgM4A9kuV2hVYCFMlO5QqVpByBwlJZPCX+yhUSx0BQmcEGgBOy4gF15ZDgy9ZlgHUcbLBySGTdOARtCCCo1K8cEnxJdQNGmX79gCdDjiTU6IFGEhoCFdekgnoSAcMwAQAK+6Xkm4C2WwAAAABJRU5ErkJggg==",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-iobit-unlocker",
                        Name = "IObit Unlocker",
                        Description = "Tool to unlock files that are in use by other processes",
                        GroupName = "File & Disk Management",
                        WinGetPackageId = ["IObit.IObitUnlocker"],
                        ChocoPackageId = "iobit-unlocker",
                        WebsiteUrl = "https://www.iobit.com/en/iobit-unlocker.php",
                        IconSources = [
                            "https://www.iobit.com/tpl/images/product-icons/unlocker_60.png",
                            "https://upload.wikimedia.org/wikipedia/commons/5/52/IObit_logo.png",
                        ],
                    },
                    // HiBit is unavailable for download due to conflict in the region
                    /*
                    new ItemDefinition
                    {
                        Id = "external-app-hibit-uninstaller",
                        Name = "HiBit Uninstaller",
                        Description = "Completely Uninstall Stubborn Software, Windows Apps & Browser Extension",
                        GroupName = "File & Disk Management",
                        WinGetPackageId = ["HiBitSoftware.HiBitUninstaller"],
                        WebsiteUrl = "https://www.hibitsoft.ir/Uninstaller.html"
                    },
                    */
                    new ItemDefinition
                    {
                        Id = "external-app-sandisk-dashboard",
                        Name = "SanDisk Dashboard",
                        Description = "Drive management tool for SanDisk SSDs and flash drives",
                        RegistrySubKeyName = "SanDisk Dashboard",
                        GroupName = "File & Disk Management",
                        WinGetPackageId = ["SanDisk.Dashboard"],
                        ChocoPackageId = "sandisk-dashboard",
                        WebsiteUrl = "https://support-en.sandisk.com/app/products/downloads/softwaredownloads",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://sddashboarddownloads.sandisk.com/wdDashboard/DashboardSetup.exe",
                        },
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/5/5f/SanDisk_2024_logo.svg/250px-SanDisk_2024_logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-rufus",
                        Name = "Rufus",
                        Description = "Utility to create bootable USB flash drives",
                        RegistryDisplayName = "Rufus",
                        GroupName = "File & Disk Management",
                        WinGetPackageId = ["Rufus.Rufus"],
                        ChocoPackageId = "rufus",
                        MsStoreId = "9PC3H3V7Q9CH",
                        WebsiteUrl = "https://rufus.ie/en/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/d/de/Rufus-logo.png",
                            "https://raw.githubusercontent.com/pbatard/rufus/master/res/rufus.ico",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-advanced-renamer",
                        Name = "Advanced Renamer",
                        Description = "Batch file renaming utility with advanced options",
                        RegistryDisplayName = "Advanced Renamer",
                        GroupName = "File & Disk Management",
                        WinGetPackageId = ["HulubuluSoftware.AdvancedRenamer"],
                        ChocoPackageId = "advanced-renamer",
                        MsStoreId = "XP9MD3S1KFCPH1",
                        WebsiteUrl = "https://www.advancedrenamer.com/",
                        IconSources = [
                            "https://www.advancedrenamer.com/pic/arenlogo_1024.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-ventoy",
                        Name = "Ventoy",
                        Description = "Open source tool to create bootable USB drive for ISO files",
                        GroupName = "File & Disk Management",
                        WinGetPackageId = ["Ventoy.Ventoy"],
                        ChocoPackageId = "ventoy",
                        WebsiteUrl = "https://www.ventoy.net/",
                        IconSources = [
                            "https://raw.githubusercontent.com/ventoy/Ventoy/master/ICON/logo_256.png",
                            "https://upload.wikimedia.org/wikipedia/commons/0/00/Ventoy_Logo.png",
                        ],
                    }
                }
            };
        }
    }
}

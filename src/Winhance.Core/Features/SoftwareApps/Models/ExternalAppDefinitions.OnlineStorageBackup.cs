using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Models;

public static partial class ExternalAppDefinitions
{
    public static class OnlineStorageBackup
    {
        public static ItemGroup GetOnlineStorageBackup()
        {
            return new ItemGroup
            {
                Name = "Online Storage & Backup",
                FeatureId = FeatureIds.ExternalApps,
                Items = new List<ItemDefinition>
                {
                    new ItemDefinition
                    {
                        Id = "external-app-google-drive",
                        Name = "Google Drive",
                        Description = "Cloud storage and file synchronization service",
                        GroupName = "Online Storage & Backup",
                        WinGetPackageId = ["Google.GoogleDrive"],
                        ChocoPackageId = "googledrive",
                        WebsiteUrl = "https://www.google.com/drive/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-dropbox",
                        Name = "Dropbox",
                        Description = "File hosting service that offers cloud storage, file synchronization, personal cloud",
                        GroupName = "Online Storage & Backup",
                        AppxPackageName = ["DropboxInc.Dropbox"],
                        WinGetPackageId = ["Dropbox.Dropbox"],
                        ChocoPackageId = "dropbox",
                        MsStoreId = "9NK4T08DHQ80",
                        WebsiteUrl = "https://www.dropbox.com/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-sugarsync",
                        Name = "SugarSync",
                        Description = "Automatically access and share your photos, videos, and files in any folder",
                        GroupName = "Online Storage & Backup",
                        WinGetPackageId = ["IPVanish.SugarSync"],
                        ChocoPackageId = "sugarsync",
                        WebsiteUrl = "https://www.sugarsync.com/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-nextcloud",
                        Name = "Nextcloud",
                        Description = "Self-hosted cloud platform for files, calendar, contacts, and chat",
                        GroupName = "Online Storage & Backup",
                        WinGetPackageId = ["Nextcloud.NextcloudDesktop"],
                        ChocoPackageId = "nextcloud-client",
                        WebsiteUrl = "https://nextcloud.com/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-proton-drive",
                        Name = "Proton Drive",
                        Description = "Secure cloud storage with end-to-end encryption",
                        GroupName = "Online Storage & Backup",
                        WinGetPackageId = ["Proton.ProtonDrive"],
                        ChocoPackageId = "protondrive",
                        WebsiteUrl = "https://proton.me/drive",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-freefilesync",
                        Name = "FreeFileSync",
                        Description = "Open-source folder comparison and synchronization tool",
                        GroupName = "Online Storage & Backup",
                        ChocoPackageId = "freefilesync",
                        WebsiteUrl = "https://freefilesync.org/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-hekasoft-backup",
                        Name = "Hekasoft Backup & Restore",
                        Description = "Backs up and restores browser bookmarks, settings, and profiles",
                        RegistryDisplayName = "Hekasoft Backup & Restore {version}",
                        GroupName = "Online Storage & Backup",
                        WinGetPackageId = ["Hekasoft.Backup-Restore"],
                        MsStoreId = "9NLJQ1B18MZT",
                        WebsiteUrl = "https://hekasoft.com/hekasoft-backup-restore/",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://hekasoft.com/?download=112",
                            FallbackDownloadUrl = "https://hekasoft.com/?download=612",
                        },
                        // Icon resolved via MS Store CDN (Layer 2a). No trusted catalog URL.
                    },
                }
            };
        }
    }
}

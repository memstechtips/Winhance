using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
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
                            WinGetPackageId = "Google.GoogleDrive",
                            Category = "Online Storage & Backup",
                            WebsiteUrl = "https://www.google.com/drive/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-dropbox",
                            Name = "Dropbox",
                            Description = "File hosting service that offers cloud storage, file synchronization, personal cloud",
                            GroupName = "Online Storage & Backup",
                            WinGetPackageId = "Dropbox.Dropbox",
                            Category = "Online Storage & Backup",
                            WebsiteUrl = "https://www.dropbox.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-sugarsync",
                            Name = "SugarSync",
                            Description = "Automatically access and share your photos, videos, and files in any folder",
                            GroupName = "Online Storage & Backup",
                            WinGetPackageId = "IPVanish.SugarSync",
                            Category = "Online Storage & Backup",
                            WebsiteUrl = "https://www.sugarsync.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-nextcloud",
                            Name = "NextCloud",
                            Description = "Access, share and protect your files, calendars, contacts, communication & more at home and in your organization",
                            GroupName = "Online Storage & Backup",
                            WinGetPackageId = "Nextcloud.NextcloudDesktop",
                            Category = "Online Storage & Backup",
                            WebsiteUrl = "https://nextcloud.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-proton-drive",
                            Name = "Proton Drive",
                            Description = "Secure cloud storage with end-to-end encryption",
                            GroupName = "Online Storage & Backup",
                            WinGetPackageId = "Proton.ProtonDrive",
                            Category = "Online Storage & Backup",
                            WebsiteUrl = "https://proton.me/drive"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-hekasoft-backup",
                            Name = "Hekasoft Backup & Restore",
                            Description = "The complete free solution for browser backup and management",
                            GroupName = "Online Storage & Backup",
                            WinGetPackageId = "Hekasoft.Backup-Restore",
                            Category = "Online Storage & Backup",
                            WebsiteUrl = "https://hekasoft.com/hekasoft-backup-restore/"
                        },
                    }
                };
            }
        }
    }
}
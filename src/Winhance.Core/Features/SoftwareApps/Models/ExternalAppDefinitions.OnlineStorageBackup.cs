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
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/1/12/Google_Drive_icon_%282020%29.svg/500px-Google_Drive_icon_%282020%29.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-dropbox",
                        Name = "Dropbox",
                        Description = "File hosting service that offers cloud storage, file synchronization, personal cloud",
                        GroupName = "Online Storage & Backup",
                        WinGetPackageId = ["Dropbox.Dropbox"],
                        ChocoPackageId = "dropbox",
                        WebsiteUrl = "https://www.dropbox.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/7/78/Dropbox_Icon.svg/500px-Dropbox_Icon.svg.png",
                        ],
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
                        IconSources = [
                            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABwAAAAcCAMAAABF0y+mAAAAbFBMVEVHcEwMeDQLfzgMeDQNfTcPfzsLdjIWqFURbDELdjIWqlYWqFUMejUWq1cLdTILeDMMdzMOhj4MfjgMeDQWqFUVpFIkZzwTnU0sJCQqKiUnNycRkkYWqFUnNCcWWy0kPSgXslwmOSgOgDoTmUstRt+KAAAAJHRSTlMAkSn/nA14xkrCiv+3//9g//7/0NT///9BzP//sbAYcT9g8d7ZxZnpAAAA00lEQVR4AX3RRQLDIBBAURokitdd7n/HzrTxTPjbh8PINmy9jCdUcC5hCI2cc8XynLQCsKzqmsSGQ3oF0Uw9x8LiISyFyjkfSsZii9uRCRd2e70TkrdYjS9uDoC1Fo7/DyQ7Op7OnBsTLloHROf1tadbcb8rDu53hiNe9H/i/fboVkblmNk9GfZqqb2E49j7b3c5u/4/RX4HN8bgbEG+3GV3uOChLPHNZgd3PaCyRbLhB1DviKnQXTWf+gIYCRy+dA05lqWwSKFdwZjCLIXsvilj9gWLvAr+hK5X3AAAAABJRU5ErkJggg==",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-nextcloud",
                        Name = "Nextcloud",
                        Description = "Access, share and protect your files, calendars, contacts, communication & more at home and in your organization",
                        GroupName = "Online Storage & Backup",
                        WinGetPackageId = ["Nextcloud.NextcloudDesktop"],
                        ChocoPackageId = "nextcloud-client",
                        WebsiteUrl = "https://nextcloud.com/",
                        IconSources = [
                            "https://raw.githubusercontent.com/nextcloud/desktop/master/theme/colored/Nextcloud-w10starttile.png",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/6/60/Nextcloud_Logo.svg/500px-Nextcloud_Logo.svg.png",
                        ],
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
                        IconSources = [
                            "https://raw.githubusercontent.com/ProtonDriveApps/windows-drive/main/assets/ProtonDrive.ico",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3a/Proton_Drive_Logo.svg/500px-Proton_Drive_Logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-freefilesync",
                        Name = "FreeFileSync",
                        Description = "Open-source folder comparison and synchronization tool",
                        GroupName = "Online Storage & Backup",
                        ChocoPackageId = "freefilesync",
                        WebsiteUrl = "https://freefilesync.org/",
                        IconSources = [
                            "https://freefilesync.org/images/FreeFileSync.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-hekasoft-backup",
                        Name = "Hekasoft Backup & Restore",
                        Description = "The complete free solution for browser backup and management",
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

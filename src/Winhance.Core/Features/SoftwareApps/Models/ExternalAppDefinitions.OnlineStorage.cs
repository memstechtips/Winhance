using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    public static partial class ExternalAppDefinitions
    {
        public static class OnlineStorage
        {
            public static ItemGroup GetOnlineStorage()
            {
                return new ItemGroup
                {
                    Name = "Online Storage",
                    FeatureId = FeatureIds.ExternalApps,
                    Items = new List<ItemDefinition>
                    {
                        new ItemDefinition
                        {
                            Id = "external-app-google-drive",
                            Name = "Google Drive",
                            Description = "Cloud storage and file synchronization service",
                            GroupName = "Online Storage",
                            WinGetPackageId = "Google.GoogleDrive",
                            Category = "Online Storage"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-dropbox",
                            Name = "Dropbox",
                            Description = "File hosting service that offers cloud storage, file synchronization, personal cloud",
                            GroupName = "Online Storage",
                            WinGetPackageId = "Dropbox.Dropbox",
                            Category = "Online Storage"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-sugarsync",
                            Name = "SugarSync",
                            Description = "Automatically access and share your photos, videos, and files in any folder",
                            GroupName = "Online Storage",
                            WinGetPackageId = "IPVanish.SugarSync",
                            Category = "Online Storage"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-nextcloud",
                            Name = "NextCloud",
                            Description = "Access, share and protect your files, calendars, contacts, communication & more at home and in your organization",
                            GroupName = "Online Storage",
                            WinGetPackageId = "Nextcloud.NextcloudDesktop",
                            Category = "Online Storage"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-proton-drive",
                            Name = "Proton Drive",
                            Description = "Secure cloud storage with end-to-end encryption",
                            GroupName = "Online Storage",
                            WinGetPackageId = "Proton.ProtonDrive",
                            Category = "Online Storage"
                        }
                    }
                };
            }
        }
    }
}
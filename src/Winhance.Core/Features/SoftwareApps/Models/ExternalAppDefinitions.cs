using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    public static partial class ExternalAppDefinitions
    {
        public static ItemGroup GetExternalApps()
        {
            var allItems = new List<ItemDefinition>();

            // Add all category items
            allItems.AddRange(Browsers.GetBrowsers().Items);
            allItems.AddRange(DevelopmentApps.GetDevelopmentApps().Items);
            allItems.AddRange(DocumentViewers.GetDocumentViewers().Items);
            allItems.AddRange(OnlineStorage.GetOnlineStorage().Items);
            allItems.AddRange(Multimedia.GetMultimedia().Items);
            allItems.AddRange(Imaging.GetImaging().Items);
            allItems.AddRange(Compression.GetCompression().Items);
            allItems.AddRange(MessagingEmailCalendar.GetMessagingEmailCalendar().Items);
            allItems.AddRange(FileDiskManagement.GetFileDiskManagement().Items);
            allItems.AddRange(RemoteAccess.GetRemoteAccess().Items);
            allItems.AddRange(OpticalDiscTools.GetOpticalDiscTools().Items);
            allItems.AddRange(OtherUtilities.GetOtherUtilities().Items);
            allItems.AddRange(CustomizationUtilities.GetCustomizationUtilities().Items);
            allItems.AddRange(Gaming.GetGaming().Items);
            allItems.AddRange(PrivacySecurity.GetPrivacySecurity().Items);

            return new ItemGroup
            {
                Name = "External Apps",
                FeatureId = FeatureIds.ExternalApps,
                Items = allItems
            };
        }
    }
}
using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models;

public static partial class ExternalAppDefinitions
{
    public static class MessagingEmailCalendar
    {
        public static ItemGroup GetMessagingEmailCalendar()
        {
            return new ItemGroup
            {
                Name = "Messaging, Email & Calendar",
                FeatureId = FeatureIds.ExternalApps,
                Items = new List<ItemDefinition>
                {
                    new ItemDefinition
                    {
                        Id = "external-app-telegram",
                        Name = "Telegram Desktop",
                        Description = "Instant messaging and voice calling app",
                        GroupName = "Messaging, Email & Calendar",
                        AppxPackageName = ["TelegramMessengerLLP.TelegramDesktop"],
                        WinGetPackageId = ["Telegram.TelegramDesktop"],
                        ChocoPackageId = "telegram",
                        MsStoreId = "9NZTWSQNTD0S",
                        WebsiteUrl = "https://telegram.org/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-whatsapp",
                        Name = "WhatsApp",
                        Description = "Meta's instant messaging app with end-to-end encryption",
                        GroupName = "Messaging, Email & Calendar",
                        AppxPackageName = ["5319275A.WhatsAppDesktop"],
                        ChocoPackageId = "whatsapp",
                        MsStoreId = "9NKSQGP7F2NH",
                        WebsiteUrl = "https://www.whatsapp.com/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-zoom",
                        Name = "Zoom Workplace",
                        Description = "Video conferencing and messaging platform",
                        RegistryDisplayName = "Zoom Workplace ({arch})",
                        GroupName = "Messaging, Email & Calendar",
                        WinGetPackageId = ["Zoom.Zoom"],
                        ChocoPackageId = "zoom",
                        MsStoreId = "XP99J3KP4XZ4VV",
                        WebsiteUrl = "https://zoom.us/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-discord",
                        Name = "Discord",
                        Description = "Voice, video and text communication service",
                        GroupName = "Messaging, Email & Calendar",
                        WinGetPackageId = ["Discord.Discord"],
                        ChocoPackageId = "discord",
                        MsStoreId = "XPDC2RH70K22MN",
                        WebsiteUrl = "https://discord.com/",
                        // Vendor brand page only ships SVG/ZIP and the Wikimedia
                        // render keeps rotting (Discord_color_D.svg has been
                        // rehashed/removed twice already). Embed the on-page
                        // Symbol mark so the resolver decodes it via the data:
                        // branch — locked-in and immune to URL rot.
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-pidgin",
                        Name = "Pidgin",
                        Description = "Multi-protocol instant messaging client",
                        GroupName = "Messaging, Email & Calendar",
                        WinGetPackageId = ["Pidgin.Pidgin"],
                        ChocoPackageId = "pidgin",
                        WebsiteUrl = "https://pidgin.im/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-thunderbird",
                        Name = "Mozilla Thunderbird",
                        Description = "Mozilla's open-source email and calendar client",
                        RegistrySubKeyName = "Mozilla Thunderbird {version} ({arch} {locale})",
                        RegistryDisplayName = "Mozilla Thunderbird ({arch} {locale})",
                        GroupName = "Messaging, Email & Calendar",
                        AppxPackageName = ["MozillaThunderbird.MozillaThunderbirdRelease"],
                        WinGetPackageId = ["Mozilla.Thunderbird"],
                        ChocoPackageId = "thunderbird",
                        MsStoreId = "9MX7TGVGQFCL",
                        WebsiteUrl = "https://www.thunderbird.net/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-emclient",
                        Name = "eM Client",
                        Description = "Email client with calendar, tasks, and chat",
                        GroupName = "Messaging, Email & Calendar",
                        AppxPackageName = ["eMClient.20054CA46072C"],
                        WinGetPackageId = ["eMClient.eMClient"],
                        ChocoPackageId = "em-client",
                        MsStoreId = "9NM8S4PVF0N2",
                        WebsiteUrl = "https://www.emclient.com/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-proton-mail",
                        Name = "Proton Mail",
                        Description = "Secure email service with end-to-end encryption",
                        GroupName = "Messaging, Email & Calendar",
                        WinGetPackageId = ["Proton.ProtonMail"],
                        ChocoPackageId = "protonmail",
                        WebsiteUrl = "https://proton.me/mail",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-trillian",
                        Name = "Trillian",
                        Description = "Instant messaging application",
                        RegistryDisplayName = "Trillian Machine-Wide Installer",
                        GroupName = "Messaging, Email & Calendar",
                        WinGetPackageId = ["CeruleanStudios.Trillian"],
                        ChocoPackageId = "trillian",
                        WebsiteUrl = "https://www.trillian.im/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-element",
                        Name = "Element",
                        Description = "Secure and decentralized messaging app built on Matrix protocol",
                        GroupName = "Messaging, Email & Calendar",
                        WinGetPackageId = ["Element.Element"],
                        ChocoPackageId = "element-desktop",
                        WebsiteUrl = "https://element.io/",
                    }
                }
            };
        }
    }
}

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
                        WinGetPackageId = ["Telegram.TelegramDesktop"],
                        ChocoPackageId = "telegram",
                        WebsiteUrl = "https://telegram.org/",
                        IconSources = [
                            "https://raw.githubusercontent.com/telegramdesktop/tdesktop/v6.7.5/Telegram/Resources/art/icon256.png",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/8/82/Telegram_logo.svg/512px-Telegram_logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-whatsapp",
                        Name = "WhatsApp",
                        Description = "Instant messaging and voice calling app",
                        GroupName = "Messaging, Email & Calendar",
                        AppxPackageName = ["5319275A.WhatsAppDesktop"],
                        ChocoPackageId = "whatsapp",
                        MsStoreId = "9NKSQGP7F2NH",
                        WebsiteUrl = "https://www.whatsapp.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/5/5e/WhatsApp_icon.png",
                        ],
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
                        WebsiteUrl = "https://zoom.us/",
                        IconSources = [
                            "https://st1.zoom.us/zoom.ico",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/7/7b/Zoom_Communications_Logo.svg/512px-Zoom_Communications_Logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-discord",
                        Name = "Discord",
                        Description = "Voice, video and text communication service",
                        GroupName = "Messaging, Email & Calendar",
                        WinGetPackageId = ["Discord.Discord"],
                        ChocoPackageId = "discord",
                        WebsiteUrl = "https://discord.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/f/fc/Discord_color_D.svg/256px-Discord_color_D.svg.png",
                        ],
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
                        IconSources = [
                            "https://pidgin.im/images/logo.png",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/1/18/Pidgin.svg/256px-Pidgin.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-thunderbird",
                        Name = "Mozilla Thunderbird",
                        Description = "Free email application",
                        RegistrySubKeyName = "Mozilla Thunderbird {version} ({arch} {locale})",
                        RegistryDisplayName = "Mozilla Thunderbird ({arch} {locale})",
                        GroupName = "Messaging, Email & Calendar",
                        WinGetPackageId = ["Mozilla.Thunderbird"],
                        ChocoPackageId = "thunderbird",
                        WebsiteUrl = "https://www.thunderbird.net/",
                        IconSources = [
                            "https://www.thunderbird.net/media/img/thunderbird/favicon-196.png",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/2/2f/Thunderbird_2023_icon.svg/256px-Thunderbird_2023_icon.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-emclient",
                        Name = "eM Client",
                        Description = "Email client with calendar, tasks, and chat",
                        GroupName = "Messaging, Email & Calendar",
                        WinGetPackageId = ["eMClient.eMClient"],
                        ChocoPackageId = "em-client",
                        WebsiteUrl = "https://www.emclient.com/",
                        IconSources = [
                            "https://www.emclient.com/favicon.ico",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3b/EM_Client_logo.svg/256px-EM_Client_logo.svg.png",
                        ],
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
                        IconSources = [
                            "https://proton.me/favicon.ico",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/0/0c/ProtonMail_icon.svg/240px-ProtonMail_icon.svg.png",
                        ],
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
                        IconSources = [
                            "https://www.trillian.im/favicon.ico",
                            "https://www.trillian.im/shared/images/index/cs_logo.png",
                        ],
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
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/1/13/Element_%28software%29_logo_%282024%29.svg/256px-Element_%28software%29_logo_%282024%29.svg.png",
                        ],
                    }
                }
            };
        }
    }
}

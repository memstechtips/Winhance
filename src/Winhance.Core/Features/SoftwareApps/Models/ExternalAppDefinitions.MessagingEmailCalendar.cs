using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
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
                            Name = "Telegram",
                            Description = "Instant messaging and voice calling app",
                            GroupName = "Messaging, Email & Calendar",
                            WinGetPackageId = "Telegram.TelegramDesktop",
                            WebsiteUrl = "https://telegram.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-whatsapp",
                            Name = "Whatsapp",
                            Description = "Instant messaging and voice calling app",
                            GroupName = "Messaging, Email & Calendar",
                            WinGetPackageId = "9NKSQGP7F2NH",
                            WebsiteUrl = "https://www.whatsapp.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-zoom",
                            Name = "Zoom",
                            Description = "Video conferencing and messaging platform",
                            GroupName = "Messaging, Email & Calendar",
                            WinGetPackageId = "Zoom.Zoom",
                            WebsiteUrl = "https://zoom.us/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-discord",
                            Name = "Discord",
                            Description = "Voice, video and text communication service",
                            GroupName = "Messaging, Email & Calendar",
                            WinGetPackageId = "Discord.Discord",
                            WebsiteUrl = "https://discord.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-pidgin",
                            Name = "Pidgin",
                            Description = "Multi-protocol instant messaging client",
                            GroupName = "Messaging, Email & Calendar",
                            WinGetPackageId = "Pidgin.Pidgin",
                            WebsiteUrl = "https://pidgin.im/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-thunderbird",
                            Name = "Thunderbird",
                            Description = "Free email application",
                            GroupName = "Messaging, Email & Calendar",
                            WinGetPackageId = "Mozilla.Thunderbird",
                            WebsiteUrl = "https://www.thunderbird.net/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-emclient",
                            Name = "eMClient",
                            Description = "Email client with calendar, tasks, and chat",
                            GroupName = "Messaging, Email & Calendar",
                            WinGetPackageId = "eMClient.eMClient",
                            WebsiteUrl = "https://www.emclient.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-proton-mail",
                            Name = "Proton Mail",
                            Description = "Secure email service with end-to-end encryption",
                            GroupName = "Messaging, Email & Calendar",
                            WinGetPackageId = "Proton.ProtonMail",
                            WebsiteUrl = "https://proton.me/mail"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-trillian",
                            Name = "Trillian",
                            Description = "Instant messaging application",
                            GroupName = "Messaging, Email & Calendar",
                            WinGetPackageId = "CeruleanStudios.Trillian",
                            WebsiteUrl = "https://www.trillian.im/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-element",
                            Name = "Element",
                            Description = "Secure and decentralized messaging app built on Matrix protocol",
                            GroupName = "Messaging, Email & Calendar",
                            WinGetPackageId = "Element.Element",
                            WebsiteUrl = "https://element.io/"
                        }
                    }
                };
            }
        }
    }
}
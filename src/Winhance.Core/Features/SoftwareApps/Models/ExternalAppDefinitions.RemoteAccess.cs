using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    public static partial class ExternalAppDefinitions
    {
        public static class RemoteAccess
        {
            public static ItemGroup GetRemoteAccess()
            {
                return new ItemGroup
                {
                    Name = "Remote Access",
                    FeatureId = FeatureIds.ExternalApps,
                    Items = new List<ItemDefinition>
                    {
                        new ItemDefinition
                        {
                            Id = "external-app-rustdesk",
                            Name = "RustDesk",
                            Description = "Fast Open-Source Remote Access and Support Software",
                            GroupName = "Remote Access",
                            WinGetPackageId = "RustDesk.RustDesk",
                            Category = "Remote Access",
                            WebsiteUrl = "https://rustdesk.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-input-leap",
                            Name = "Input Leap",
                            Description = "Open-source KVM software for sharing mouse and keyboard between computers",
                            GroupName = "Remote Access",
                            WinGetPackageId = "input-leap.input-leap",
                            Category = "Remote Access",
                            WebsiteUrl = "https://github.com/input-leap/input-leap"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-anydesk",
                            Name = "AnyDesk",
                            Description = "Remote desktop software for remote access and support",
                            GroupName = "Remote Access",
                            WinGetPackageId = "AnyDesk.AnyDesk",
                            Category = "Remote Access",
                            WebsiteUrl = "https://anydesk.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-teamviewer",
                            Name = "TeamViewer 15",
                            Description = "Remote control, desktop sharing, online meetings, web conferencing and file transfer",
                            GroupName = "Remote Access",
                            WinGetPackageId = "TeamViewer.TeamViewer",
                            Category = "Remote Access",
                            WebsiteUrl = "https://www.teamviewer.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vnc-server",
                            Name = "RealVNC Server",
                            Description = "Remote access software",
                            GroupName = "Remote Access",
                            WinGetPackageId = "RealVNC.VNCServer",
                            Category = "Remote Access",
                            WebsiteUrl = "https://www.realvnc.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vnc-viewer",
                            Name = "RealVNC Viewer",
                            Description = "Remote access software",
                            GroupName = "Remote Access",
                            WinGetPackageId = "RealVNC.VNCViewer",
                            Category = "Remote Access",
                            WebsiteUrl = "https://www.realvnc.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-chrome-remote-desktop",
                            Name = "Chrome Remote Desktop",
                            Description = "Remote access to your computer through Chrome browser",
                            GroupName = "Remote Access",
                            WinGetPackageId = "Google.ChromeRemoteDesktopHost",
                            Category = "Remote Access",
                            WebsiteUrl = "https://remotedesktop.google.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-parsec",
                            Name = "Parsec",
                            Description = "Remote desktop reimagined. Secure, flexible, effortless access to whatever you do, at any time, from wherever you go",
                            GroupName = "Remote Access",
                            WinGetPackageId = "Parsec.Parsec",
                            Category = "Remote Access",
                            WebsiteUrl = "https://parsec.app/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-parsec-display",
                            Name = "Parsec Virtual Display Driver",
                            Description = "Virtual display driver for Parsec Remote Desktop",
                            GroupName = "Remote Access",
                            WinGetPackageId = "Parsec.ParsecVDD",
                            Category = "Remote Access",
                            WebsiteUrl = "https://parsec.app/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-parsec-usb",
                            Name = "Parsec Virtual USB Adapter Driver",
                            Description = "Virtual USB driver for Parsec Remote Desktop",
                            GroupName = "Remote Access",
                            WinGetPackageId = "Parsec.ParsecVUD",
                            Category = "Remote Access",
                            WebsiteUrl = "https://parsec.app/"
                        }
                    }
                };
            }
        }
    }
}
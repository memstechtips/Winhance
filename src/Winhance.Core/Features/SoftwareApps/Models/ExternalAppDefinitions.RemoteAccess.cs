using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models;

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
                        WinGetPackageId = ["RustDesk.RustDesk"],
                        ChocoPackageId = "rustdesk",
                        WebsiteUrl = "https://rustdesk.com/",
                        IconSources = [
                            "https://raw.githubusercontent.com/rustdesk/rustdesk/master/res/icon.png",
                            "https://upload.wikimedia.org/wikipedia/commons/b/b2/RustDesk-128x128.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-anydesk",
                        Name = "AnyDesk",
                        Description = "Remote desktop software for remote access and support",
                        GroupName = "Remote Access",
                        WinGetPackageId = ["AnyDesk.AnyDesk"],
                        ChocoPackageId = "anydesk",
                        WebsiteUrl = "https://anydesk.com/",
                        // Vendor site is Cloudflare-protected and Wikimedia only has
                        // a wide wordmark. Embed the on-page AnyDesk mark (base64) so
                        // the resolver decodes it via the data: branch.
                        IconSources = [
                            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABwAAAAcCAYAAAByDd+UAAAAsUlEQVR4AWJwL/ChKx4YC0ctfO9inQ+grQ4wAIZhKAwDQ4HtvgH0RLvGztIrbC0LPPSf7Q2B5vGhTdOrTPrLyDjAxM5ebZJpd6a+AhXLSlQyu2SCQMYYPZ6iiLlRwPwoYH4UMD8KmA/NORXsG9qrEDoOwwiuNKPZCAO20D3qo4m/sQQVdWOVBj/8GH9t1Y4JCCg/f8AAFBSwnTECBRVM+8W2gAXdJv1Ci/fa8Gm1jVoIAMjxngpAp7znAAAAAElFTkSuQmCC",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-teamviewer",
                        Name = "TeamViewer",
                        Description = "Remote control, desktop sharing, online meetings, web conferencing and file transfer",
                        GroupName = "Remote Access",
                        WinGetPackageId = ["TeamViewer.TeamViewer"],
                        ChocoPackageId = "teamviewer",
                        MsStoreId = "XPDM17HK323C4X",
                        WebsiteUrl = "https://www.teamviewer.com/",
                        IconSources = [
                            "https://www.teamviewer.com/favicon.ico",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/9/90/TeamViewer_logo.svg/250px-TeamViewer_logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-ultraviewer",
                        Name = "UltraViewer",
                        Description = "Remote support tool for assisting clients or family members",
                        RegistryDisplayName = "UltraViewer version {version}",
                        GroupName = "Remote Access",
                        WinGetPackageId = ["DucFabulous.UltraViewer"],
                        ChocoPackageId = "ultraviewer",
                        WebsiteUrl = "https://www.ultraviewer.net/",
                        IconSources = [
                            "https://www.ultraviewer.net/images/logo.png",
                            "https://www.ultraviewer.net/favicon.ico",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-vnc-server",
                        Name = "RealVNC Server",
                        Description = "VNC server for hosting remote access connections",
                        RegistryDisplayName = "RealVNC Server {version}",
                        GroupName = "Remote Access",
                        WinGetPackageId = ["RealVNC.VNCServer"],
                        ChocoPackageId = "vnc-connect",
                        MsStoreId = "XP98VL6ML4GPS9",
                        WebsiteUrl = "https://www.realvnc.com/",
                        // Icon resolved via MS Store CDN (Layer 2a). The Wikimedia
                        // RealVNC_Logo is a shared family wordmark — Store has the
                        // distinct Server vs Viewer marks.
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-vnc-viewer",
                        Name = "RealVNC Viewer",
                        Description = "VNC client for connecting to remote VNC servers",
                        RegistryDisplayName = "RealVNC Viewer {version}",
                        GroupName = "Remote Access",
                        WinGetPackageId = ["RealVNC.VNCViewer"],
                        ChocoPackageId = "vnc-viewer",
                        MsStoreId = "XP99DVCPGKTXNJ",
                        WebsiteUrl = "https://www.realvnc.com/",
                        // Icon resolved via MS Store CDN (Layer 2a). The Wikimedia
                        // RealVNC_Logo is a shared family wordmark — Store has the
                        // distinct Server vs Viewer marks.
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-chrome-remote-desktop",
                        Name = "Chrome Remote Desktop",
                        Description = "Remote access to your computer through Chrome browser",
                        RegistryDisplayName = "Chrome Remote Desktop Host",
                        GroupName = "Remote Access",
                        WinGetPackageId = ["Google.ChromeRemoteDesktopHost"],
                        ChocoPackageId = "chrome-remote-desktop-host",
                        WebsiteUrl = "https://remotedesktop.google.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/8/83/Chrome_Remote_Desktop_logo.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-parsec",
                        Name = "Parsec",
                        Description = "Low-latency remote desktop built for gaming and creative workflows",
                        GroupName = "Remote Access",
                        WinGetPackageId = ["Parsec.Parsec"],
                        ChocoPackageId = "parsec",
                        WebsiteUrl = "https://parsec.app/",
                        IconSources = [
                            "https://parsec.app/favicon.ico",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-parsec-display",
                        Name = "Parsec Virtual Display Driver",
                        Description = "Virtual display driver for Parsec Remote Desktop",
                        RegistrySubKeyName = "ParsecVDD",
                        RegistryDisplayName = "Parsec Virtual Display Driver",
                        GroupName = "Remote Access",
                        WinGetPackageId = ["Parsec.ParsecVDD"],
                        WebsiteUrl = "https://parsec.app/",
                        IconSources = [
                            "https://parsec.app/favicon.ico",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-parsec-usb",
                        Name = "Parsec Virtual USB Driver",
                        Description = "Virtual USB driver for Parsec Remote Desktop",
                        RegistrySubKeyName = "ParsecVUD",
                        RegistryDisplayName = "Parsec Virtual USB Adapter Driver",
                        GroupName = "Remote Access",
                        WinGetPackageId = ["Parsec.ParsecVUD"],
                        WebsiteUrl = "https://parsec.app/",
                        IconSources = [
                            "https://parsec.app/favicon.ico",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-input-leap",
                        Name = "InputLeap",
                        Description = "Open-source KVM software for sharing mouse and keyboard between computers",
                        RegistryDisplayName = "InputLeap {version}-release",
                        GroupName = "Remote Access",
                        WinGetPackageId = ["input-leap.input-leap"],
                        ChocoPackageId = "input-leap",
                        WebsiteUrl = "https://github.com/input-leap/input-leap",
                        IconSources = [
                            "https://raw.githubusercontent.com/input-leap/input-leap/master/res/input-leap.ico",
                            "https://raw.githubusercontent.com/input-leap/input-leap/master/res/input-leap.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-deskflow",
                        Name = "Deskflow",
                        Description = "Share a single keyboard and mouse between multiple computers",
                        GroupName = "Remote Access",
                        WinGetPackageId = ["Deskflow.Deskflow"],
                        ChocoPackageId = "deskflow",
                        WebsiteUrl = "https://github.com/deskflow/deskflow",
                        IconSources = [
                            "https://raw.githubusercontent.com/deskflow/deskflow-artwork/main/icon/deskflow-icon-fit-512.png",
                        ],
                    }
                }
            };
        }
    }
}

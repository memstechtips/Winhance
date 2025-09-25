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
                            Category = "Remote Access"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-input-leap",
                            Name = "Input Leap",
                            Description = "Open-source KVM software for sharing mouse and keyboard between computers",
                            GroupName = "Remote Access",
                            WinGetPackageId = "input-leap.input-leap",
                            Category = "Remote Access"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-anydesk",
                            Name = "AnyDesk",
                            Description = "Remote desktop software for remote access and support",
                            GroupName = "Remote Access",
                            WinGetPackageId = "AnyDesk.AnyDesk",
                            Category = "Remote Access"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-teamviewer",
                            Name = "TeamViewer 15",
                            Description = "Remote control, desktop sharing, online meetings, web conferencing and file transfer",
                            GroupName = "Remote Access",
                            WinGetPackageId = "TeamViewer.TeamViewer",
                            Category = "Remote Access"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vnc-server",
                            Name = "RealVNC Server",
                            Description = "Remote access software",
                            GroupName = "Remote Access",
                            WinGetPackageId = "RealVNC.VNCServer",
                            Category = "Remote Access"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vnc-viewer",
                            Name = "RealVNC Viewer",
                            Description = "Remote access software",
                            GroupName = "Remote Access",
                            WinGetPackageId = "RealVNC.VNCViewer",
                            Category = "Remote Access"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-chrome-remote-desktop",
                            Name = "Chrome Remote Desktop Host",
                            Description = "Remote access to your computer through Chrome browser",
                            GroupName = "Remote Access",
                            WinGetPackageId = "Google.ChromeRemoteDesktopHost",
                            Category = "Remote Access"
                        }
                    }
                };
            }
        }
    }
}
using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    public static partial class ExternalAppDefinitions
    {
        public static class DevelopmentApps
        {
            public static ItemGroup GetDevelopmentApps()
            {
                return new ItemGroup
                {
                    Name = "Development Apps",
                    FeatureId = FeatureIds.ExternalApps,
                    Items = new List<ItemDefinition>
                    {
                        new ItemDefinition
                        {
                            Id = "external-app-python313",
                            Name = "Python 3.13",
                            Description = "Python programming language",
                            GroupName = "Development Apps",
                            WinGetPackageId = "Python.Python.3.13",
                            WebsiteUrl = "https://www.python.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-notepadplusplus",
                            Name = "Notepad++",
                            Description = "Free source code editor and Notepad replacement",
                            GroupName = "Development Apps",
                            WinGetPackageId = "Notepad++.Notepad++",
                            WebsiteUrl = "https://notepad-plus-plus.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-winscp",
                            Name = "WinSCP",
                            Description = "Free SFTP, SCP, Amazon S3, WebDAV, and FTP client",
                            GroupName = "Development Apps",
                            WinGetPackageId = "WinSCP.WinSCP",
                            WebsiteUrl = "https://winscp.net/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-putty",
                            Name = "PuTTY",
                            Description = "Free SSH and telnet client",
                            GroupName = "Development Apps",
                            WinGetPackageId = "PuTTY.PuTTY",
                            WebsiteUrl = "https://www.putty.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-winmerge",
                            Name = "WinMerge",
                            Description = "Open source differencing and merging tool",
                            GroupName = "Development Apps",
                            WinGetPackageId = "WinMerge.WinMerge",
                            WebsiteUrl = "https://winmerge.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-eclipse",
                            Name = "Eclipse",
                            Description = "Java IDE and development platform",
                            GroupName = "Development Apps",
                            WinGetPackageId = "EclipseFoundation.EclipseIDEforJavaDevelopers",
                            WebsiteUrl = "https://www.eclipse.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vscode",
                            Name = "Visual Studio Code",
                            Description = "Code editor with support for development operations",
                            GroupName = "Development Apps",
                            WinGetPackageId = "Microsoft.VisualStudioCode",
                            WebsiteUrl = "https://code.visualstudio.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-git",
                            Name = "Git",
                            Description = "Distributed version control system",
                            GroupName = "Development Apps",
                            WinGetPackageId = "Git.Git",
                            WebsiteUrl = "https://git-scm.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-github-desktop",
                            Name = "GitHub Desktop",
                            Description = "GitHub desktop client",
                            GroupName = "Development Apps",
                            WinGetPackageId = "GitHub.GitHubDesktop",
                            WebsiteUrl = "https://desktop.github.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-meld",
                            Name = "Meld",
                            Description = "Visual diff and merge tool",
                            GroupName = "Development Apps",
                            WinGetPackageId = "Meld.Meld",
                            WebsiteUrl = "https://meldmerge.org/"
                        }
                    }
                };
            }
        }
    }
}
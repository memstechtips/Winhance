using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models;

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
                        RegistryDisplayName = "Python {version} ({arch})",
                        GroupName = "Development Apps",
                        WinGetPackageId = ["Python.Python.3.13"],
                        ChocoPackageId = "python3",
                        WebsiteUrl = "https://www.python.org/",
                        IconSources = [
                            "https://www.python.org/static/img/python-logo.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-notepadplusplus",
                        Name = "Notepad++",
                        Description = "Free source code editor and Notepad replacement",
                        GroupName = "Development Apps",
                        WinGetPackageId = ["Notepad++.Notepad++"],
                        ChocoPackageId = "notepadplusplus",
                        WebsiteUrl = "https://notepad-plus-plus.org/",
                        IconSources = [
                            "https://raw.githubusercontent.com/notepad-plus-plus/notepad-plus-plus/master/PowerEditor/src/icons/npp.ico",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-winscp",
                        Name = "WinSCP",
                        Description = "Free SFTP, SCP, Amazon S3, WebDAV, and FTP client",
                        RegistryDisplayName = "WinSCP {version}",
                        GroupName = "Development Apps",
                        WinGetPackageId = ["WinSCP.WinSCP"],
                        ChocoPackageId = "winscp",
                        WebsiteUrl = "https://winscp.net/",
                        IconSources = [
                            "https://winscp-static-746341.c.cdn77.org/assets/images/logos/logo.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-putty",
                        Name = "PuTTY",
                        Description = "Free SSH and telnet client",
                        RegistryDisplayName = "PuTTY release {version} ({arch})",
                        GroupName = "Development Apps",
                        WinGetPackageId = ["PuTTY.PuTTY"],
                        ChocoPackageId = "putty",
                        WebsiteUrl = "https://www.putty.org/",
                        IconSources = [
                            "https://raw.githubusercontent.com/github/putty/master/windows/puttygen.ico",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-winmerge",
                        Name = "WinMerge",
                        Description = "Open source differencing and merging tool",
                        RegistryDisplayName = "WinMerge {version}",
                        GroupName = "Development Apps",
                        WinGetPackageId = ["WinMerge.WinMerge"],
                        ChocoPackageId = "winmerge",
                        WebsiteUrl = "https://winmerge.org/",
                        IconSources = [
                            "https://raw.githubusercontent.com/WinMerge/winmerge/master/Src/res/Merge.ico",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-eclipse",
                        Name = "Eclipse IDE for Java",
                        RegistryDisplayName = "Eclipse IDE for Java Developers",
                        Description = "Java IDE and development platform",
                        GroupName = "Development Apps",
                        WinGetPackageId = ["EclipseFoundation.Eclipse.Java"],
                        ChocoPackageId = "eclipse-java-oxygen",
                        WebsiteUrl = "https://eclipseide.org/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/c/cf/Eclipse-SVG.svg/256px-Eclipse-SVG.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-vscode",
                        Name = "Microsoft Visual Studio Code",
                        Description = "Code editor with support for development operations",
                        RegistryDisplayName = "Microsoft Visual Studio Code ({version})",
                        GroupName = "Development Apps",
                        WinGetPackageId = ["Microsoft.VisualStudioCode"],
                        ChocoPackageId = "vscode",
                        WebsiteUrl = "https://code.visualstudio.com/",
                        IconSources = [
                            "https://raw.githubusercontent.com/microsoft/vscode/main/resources/win32/code.ico",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-git",
                        Name = "Git",
                        Description = "Distributed version control system",
                        GroupName = "Development Apps",
                        WinGetPackageId = ["Git.Git"],
                        ChocoPackageId = "git",
                        WebsiteUrl = "https://git-scm.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e0/Git-logo.svg/256px-Git-logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-github-desktop",
                        Name = "GitHub Desktop",
                        Description = "GitHub desktop client",
                        GroupName = "Development Apps",
                        WinGetPackageId = ["GitHub.GitHubDesktop"],
                        ChocoPackageId = "github-desktop",
                        WebsiteUrl = "https://desktop.github.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c2/GitHub_Invertocat_Logo.svg/256px-GitHub_Invertocat_Logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-meld",
                        Name = "Meld",
                        Description = "Visual diff and merge tool",
                        GroupName = "Development Apps",
                        WinGetPackageId = ["Meld.Meld"],
                        ChocoPackageId = "meld",
                        WebsiteUrl = "https://meldmerge.org/",
                        IconSources = [
                            "https://gitlab.gnome.org/GNOME/meld/-/raw/main/data/icons/org.gnome.meld.ico",
                        ],
                    }
                }
            };
        }
    }
}

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
                        AppxPackageName = ["PythonSoftwareFoundation.Python.3.13"],
                        WinGetPackageId = ["Python.Python.3.13"],
                        ChocoPackageId = "python3",
                        MsStoreId = "9PNRBTZXMB4Z",
                        WebsiteUrl = "https://www.python.org/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c3/Python-logo-notext.svg/250px-Python-logo-notext.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-powershell",
                        Name = "PowerShell",
                        Description = "Cross-platform task automation and configuration management shell",
                        GroupName = "Development Apps",
                        AppxPackageName = ["Microsoft.PowerShell"],
                        WinGetPackageId = ["Microsoft.PowerShell"],
                        ChocoPackageId = "powershell-core",
                        MsStoreId = "9MZ1SNWT0N5D",
                        WebsiteUrl = "https://github.com/PowerShell/PowerShell",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/0/0c/Ps_black_128.svg/250px-Ps_black_128.svg.png",
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
                        AppxPackageName = ["MartinPikryl.WinSCP"],
                        WinGetPackageId = ["WinSCP.WinSCP"],
                        ChocoPackageId = "winscp",
                        MsStoreId = "9P0PQ8B65N8X",
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
                        MsStoreId = "XPFNZKSKLBP7RJ",
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
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/c/cf/Eclipse-SVG.svg/250px-Eclipse-SVG.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-vscode",
                        Name = "Microsoft Visual Studio Code",
                        Description = "Microsoft's code editor with extensions, debugging, and Git integration",
                        RegistryDisplayName = "Microsoft Visual Studio Code ({version})",
                        GroupName = "Development Apps",
                        WinGetPackageId = ["Microsoft.VisualStudioCode"],
                        ChocoPackageId = "vscode",
                        MsStoreId = "XP9KHM4BK9FZ7Q",
                        WebsiteUrl = "https://code.visualstudio.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/9/9a/Visual_Studio_Code_1.35_icon.svg/250px-Visual_Studio_Code_1.35_icon.svg.png",
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
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3f/Git_icon.svg/250px-Git_icon.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-github-desktop",
                        Name = "GitHub Desktop",
                        Description = "GUI for Git and GitHub workflows without the command line",
                        GroupName = "Development Apps",
                        WinGetPackageId = ["GitHub.GitHubDesktop"],
                        ChocoPackageId = "github-desktop",
                        WebsiteUrl = "https://desktop.github.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/a/ae/Github-desktop-logo-symbol.svg/250px-Github-desktop-logo-symbol.svg.png",
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

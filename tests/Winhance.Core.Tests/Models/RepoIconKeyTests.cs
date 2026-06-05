using FluentAssertions;\r
using Winhance.Core.Features.SoftwareApps.Models;\r
using Xunit;\r
\r
namespace Winhance.Core.Tests.Models;\r
\r
public class RepoIconKeyTests\r
{\r
    // ------------------------------------------------------------------\r
    // external-app-* cases\r
    // ------------------------------------------------------------------\r
\r
    [Fact]\r
    public void For_ExternalApp_WinGetPackageId_WinsOverChoco()\r
    {\r
        var def = new ItemDefinition\r
        {\r
            Id = "external-app-7zip",\r
            Name = "7-Zip",\r
            Description = "7-Zip archiver",\r
            WinGetPackageId = ["7zip.7zip"],\r
            ChocoPackageId = "7zip",\r
        };\r
\r
        RepoIconKey.For(def).Should().Be("icons/external/7zip.7zip.png");\r
    }\r
\r
    [Fact]\r
    public void For_ExternalApp_OnlyChocoPackageId_UsesChoco()\r
    {\r
        var def = new ItemDefinition\r
        {\r
            Id = "external-app-musicbee",\r
            Name = "MusicBee",\r
            Description = "Music player",\r
            ChocoPackageId = "musicbee",\r
        };\r
\r
        RepoIconKey.For(def).Should().Be("icons/external/musicbee.png");\r
    }\r
\r
    [Fact]\r
    public void For_ExternalApp_NoPackageIds_UsesStrippedId()\r
    {\r
        var def = new ItemDefinition\r
        {\r
            Id = "external-app-autohotkey-v1",\r
            Name = "AutoHotkey v1",\r
            Description = "Automation scripting",\r
        };\r
\r
        RepoIconKey.For(def).Should().Be("icons/external/autohotkey-v1.png");\r
    }\r
\r
    [Fact]\r
    public void For_ExternalApp_WinGetPackageId_IsLowercased()\r
    {\r
        var def = new ItemDefinition\r
        {\r
            Id = "external-app-notepadplusplus",\r
            Name = "Notepad++",\r
            Description = "Text editor",\r
            WinGetPackageId = ["Notepad++.Notepad++"],\r
        };\r
\r
        RepoIconKey.For(def).Should().Be("icons/external/notepad++.notepad++.png");\r
    }\r
\r
    // ------------------------------------------------------------------\r
    // windows-app-* cases\r
    // ------------------------------------------------------------------\r
\r
    [Fact]\r
    public void For_WindowsApp_WithAppxPackageName_ReturnsLowercasedPath()\r
    {\r
        var def = new ItemDefinition\r
        {\r
            Id = "windows-app-calculator",\r
            Name = "Calculator",\r
            Description = "Windows Calculator",\r
            AppxPackageName = ["Microsoft.WindowsCalculator"],\r
        };\r
\r
        RepoIconKey.For(def).Should().Be("icons/windows/microsoft.windowscalculator.png");\r
    }\r
\r
    [Fact]\r
    public void For_WindowsApp_NoAppxPackageName_ReturnsNull()\r
    {\r
        var def = new ItemDefinition\r
        {\r
            Id = "windows-app-no-package",\r
            Name = "No Package",\r
            Description = "App without an AppX package name",\r
        };\r
\r
        RepoIconKey.For(def).Should().BeNull();\r
    }\r
\r
    // ------------------------------------------------------------------\r
    // Non-software ids\r
    // ------------------------------------------------------------------\r
\r
    [Fact]\r
    public void For_CapabilityId_ReturnsNull()\r
    {\r
        var def = new ItemDefinition\r
        {\r
            Id = "capability-internet-explorer",\r
            Name = "Internet Explorer",\r
            Description = "Legacy browser capability",\r
        };\r
\r
        RepoIconKey.For(def).Should().BeNull();\r
    }\r
\r
    // ------------------------------------------------------------------\r
    // WindowsCandidates\r
    // ------------------------------------------------------------------\r
\r
    [Fact]\r
    public void WindowsCandidates_MultiplePackageNames_ReturnsAllInOrder()\r
    {\r
        var def = new ItemDefinition\r
        {\r
            Id = "windows-app-gaming",\r
            Name = "Gaming App",\r
            Description = "Xbox / Gaming App",\r
            AppxPackageName = ["Microsoft.GamingApp", "Microsoft.XboxApp"],\r
        };\r
\r
        RepoIconKey.WindowsCandidates(def).Should().Equal(\r
            "icons/windows/microsoft.gamingapp.png",\r
            "icons/windows/microsoft.xboxapp.png");\r
    }\r
}\r

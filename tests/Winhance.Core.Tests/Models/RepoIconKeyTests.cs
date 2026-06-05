using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class RepoIconKeyTests
{
    // ------------------------------------------------------------------
    // external-app-* cases
    // ------------------------------------------------------------------

    [Fact]
    public void For_ExternalApp_WinGetPackageId_WinsOverChoco()
    {
        var def = new ItemDefinition
        {
            Id = "external-app-7zip",
            Name = "7-Zip",
            Description = "7-Zip archiver",
            WinGetPackageId = ["7zip.7zip"],
            ChocoPackageId = "7zip",
        };

        RepoIconKey.For(def).Should().Be("icons/external/7zip.7zip.png");
    }

    [Fact]
    public void For_ExternalApp_OnlyChocoPackageId_UsesChoco()
    {
        var def = new ItemDefinition
        {
            Id = "external-app-musicbee",
            Name = "MusicBee",
            Description = "Music player",
            ChocoPackageId = "musicbee",
        };

        RepoIconKey.For(def).Should().Be("icons/external/musicbee.png");
    }

    [Fact]
    public void For_ExternalApp_NoPackageIds_UsesStrippedId()
    {
        var def = new ItemDefinition
        {
            Id = "external-app-autohotkey-v1",
            Name = "AutoHotkey v1",
            Description = "Automation scripting",
        };

        RepoIconKey.For(def).Should().Be("icons/external/autohotkey-v1.png");
    }

    [Fact]
    public void For_ExternalApp_WinGetPackageId_IsLowercased()
    {
        var def = new ItemDefinition
        {
            Id = "external-app-notepadplusplus",
            Name = "Notepad++",
            Description = "Text editor",
            WinGetPackageId = ["Notepad++.Notepad++"],
        };

        RepoIconKey.For(def).Should().Be("icons/external/notepad++.notepad++.png");
    }

    // ------------------------------------------------------------------
    // windows-app-* cases
    // ------------------------------------------------------------------

    [Fact]
    public void For_WindowsApp_WithAppxPackageName_ReturnsLowercasedPath()
    {
        var def = new ItemDefinition
        {
            Id = "windows-app-calculator",
            Name = "Calculator",
            Description = "Windows Calculator",
            AppxPackageName = ["Microsoft.WindowsCalculator"],
        };

        RepoIconKey.For(def).Should().Be("icons/windows/microsoft.windowscalculator.png");
    }

    [Fact]
    public void For_WindowsApp_NoAppxPackageName_ReturnsNull()
    {
        var def = new ItemDefinition
        {
            Id = "windows-app-no-package",
            Name = "No Package",
            Description = "App without an AppX package name",
        };

        RepoIconKey.For(def).Should().BeNull();
    }

    // ------------------------------------------------------------------
    // Non-software ids
    // ------------------------------------------------------------------

    [Fact]
    public void For_CapabilityId_ReturnsNull()
    {
        var def = new ItemDefinition
        {
            Id = "capability-internet-explorer",
            Name = "Internet Explorer",
            Description = "Legacy browser capability",
        };

        RepoIconKey.For(def).Should().BeNull();
    }

    // ------------------------------------------------------------------
    // WindowsCandidates
    // ------------------------------------------------------------------

    [Fact]
    public void WindowsCandidates_MultiplePackageNames_ReturnsAllInOrder()
    {
        var def = new ItemDefinition
        {
            Id = "windows-app-gaming",
            Name = "Gaming App",
            Description = "Xbox / Gaming App",
            AppxPackageName = ["Microsoft.GamingApp", "Microsoft.XboxApp"],
        };

        RepoIconKey.WindowsCandidates(def).Should().Equal(
            "icons/windows/microsoft.gamingapp.png",
            "icons/windows/microsoft.xboxapp.png");
    }
}

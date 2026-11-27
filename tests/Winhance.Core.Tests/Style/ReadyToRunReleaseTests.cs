using System.Text.RegularExpressions;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Style;

// ReadyToRun is applied by the Publish target only. A plain build ships JIT-only DLLs, so the release script must
// publish and package the publish folder, and the project must turn ReadyToRun on outside Debug.
public class ReadyToRunReleaseTests
{
    private static readonly string Script = File.ReadAllText(
        Path.Combine(RepoPaths.SolutionDir(), "extras", "build-and-package.ps1"));

    private static readonly string Project = File.ReadAllText(
        Path.Combine(RepoPaths.SolutionDir(), "src", "Winhance.UI", "Winhance.UI.csproj"));

    [Fact]
    public void Project_PublishesReadyToRunOutsideDebug()
    {
        Project.Should().Contain("<PublishReadyToRun Condition=\"'$(Configuration)' != 'Debug'\">true</PublishReadyToRun>");
    }

    [Fact]
    public void ReleaseScript_PublishesAndPackagesThePublishFolder()
    {
        Script.Should().Contain("/t:Publish /p:Configuration=Release");
        Script.Should().MatchRegex(@"\$publishOutputPath = "".*\\win-x64\\publish""");
    }

    // An unpackaged app's .pri and .xbf files reach publish only from this package version on; without them the
    // installed app fails on "Cannot locate resource from 'ms-appx:///MainWindow.xaml'".
    [Fact]
    public void Project_ReferencesTheMsixBuildToolsThatPublishThePriAndXbfFiles()
    {
        var match = Regex.Match(Project, @"<PackageReference Include=""Microsoft\.Windows\.SDK\.BuildTools\.MSIX"" Version=""([\d.]+)""");
        match.Success.Should().BeTrue();
        Version.Parse(match.Groups[1].Value).Should().BeGreaterThanOrEqualTo(new Version(1, 7, 260930100));
    }

    [Fact]
    public void Project_AddsThePageXbfFilesToPublish()
    {
        Project.Should().Contain("<Target Name=\"PublishXbfFiles\" AfterTargets=\"ComputeResolvedFilesToPublishList\">");
        Project.Should().Contain("<ResolvedFileToPublish Include=\"@(_XbfToPublish)\" Exclude=\"@(ResolvedFileToPublish)\">");
    }

    [Fact]
    public void ReleaseScript_EmptiesPublishFirstAndFailsOnABuildFileMissingFromIt()
    {
        Script.Should().Contain("if (Test-Path $publishOutputPath) { Remove-Item -Path $publishOutputPath -Recurse -Force }");
        Script.Should().Contain("These build files are missing from the publish folder:");
        Script.IndexOf("Remove-Item -Path $publishOutputPath", StringComparison.Ordinal)
            .Should().BeLessThan(Script.IndexOf("/t:Publish", StringComparison.Ordinal));
    }

    [Fact]
    public void Installer_UpgradePreservesPortableData()
    {
        var installer = File.ReadAllText(Path.Combine(RepoPaths.SolutionDir(), "extras", "Winhance.Installer.iss"));
        installer.Should().MatchRegex(@"CompareText\(FindRec\.Name, 'Scripts'\) <> 0\) and\s+\(CompareText\(FindRec\.Name, 'Data'\) <> 0\) then\s+DelTree");
    }

    [Fact]
    public void Installer_PackagesOneFolderOnly()
    {
        var installer = File.ReadAllText(Path.Combine(RepoPaths.SolutionDir(), "extras", "Winhance.Installer.iss"));
        Regex.Count(installer, @"^Source: ""C:\\Winhance\\src\\", RegexOptions.Multiline).Should().Be(1);
    }
}

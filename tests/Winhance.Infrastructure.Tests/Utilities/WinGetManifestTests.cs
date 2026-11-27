using FluentAssertions;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.Utilities;

public class WinGetManifestTests
{
    private const string InstallerPath = @"C:\ProgramData\Winhance\Unattend\Apps\app\x64\setup.exe";

    private static readonly int[] VlcSuccessCodes = [0, 3010, 1641, 1638];
    private static readonly int[] BothSuccessCodeLists = [0, 1, 2, -2147024891];

    // What `winget download` really wrote for VLC 3.0.23 x64, not a hand-written manifest.
    private static string VlcManifest => File.ReadAllText(Path.Combine(
        RepoPaths.SolutionDir(), "tests", "Winhance.Infrastructure.Tests", "Utilities", "Fixtures",
        "VLC media player_3.0.23_Machine_X64_wix_en-US.yaml"));

    [Fact]
    public void TryRead_WingetDownloadManifest_ReadsTypeSwitchesAndSuccessCodes()
    {
        var manifest = WinGetManifest.TryRead(VlcManifest);

        manifest.Should().NotBeNull();
        manifest!.InstallerType.Should().Be("wix");
        manifest.Silent.Should().Be("/quiet /norestart");
        manifest.SuccessCodes.Should().Contain(VlcSuccessCodes);
        manifest.SuccessCodes.Should().NotContain(1602);
    }

    [Fact]
    public void CommandFor_MachineScopeWixManifest_RunsMsiexecWithTheManifestSwitchesForAllUsers()
    {
        const string msi = @"C:\ProgramData\Winhance\Unattend\Apps\vlc\x64\vlc.msi";

        var command = WinGetManifest.TryRead(VlcManifest)!.CommandFor(msi);

        command.Should().NotBeNull();
        command!.Value.FileName.Should().Be("msiexec.exe");
        command.Value.Arguments.Should().Be($"/i \"{msi}\" /quiet /norestart ALLUSERS=1");
    }

    [Fact]
    public void CommandFor_UserScopeMsi_LeavesAllUsersToTheMsi()
    {
        const string msi = @"C:\ProgramData\Winhance\Unattend\Apps\app\x64\app.msi";

        var command = WinGetManifest.TryRead("""
            Installers:
            - InstallerType: msi
              Scope: user
            """)!.CommandFor(msi);

        command.Should().Be(("msiexec.exe", $"/i \"{msi}\" /quiet /norestart"));
    }

    [Fact]
    public void TryRead_InstallerSuccessCodes_CountAsSuccess()
    {
        var manifest = WinGetManifest.TryRead("""
            InstallerSuccessCodes:
            - 1
            Installers:
            - InstallerType: exe
              InstallerSuccessCodes:
              - 2
              - -2147024891
            """);

        manifest!.SuccessCodes.Should().BeEquivalentTo(BothSuccessCodeLists);
    }

    [Fact]
    public void TryRead_InstallerTypeAtTheRoot_ReadsIt()
    {
        var manifest = WinGetManifest.TryRead("""
            PackageIdentifier: Test.App
            InstallerType: NullSoft
            Installers:
            - Architecture: x64
              InstallerUrl: https://example.com/setup.exe
            ManifestType: merged
            """);

        manifest!.InstallerType.Should().Be("nullsoft");
    }

    [Fact]
    public void TryRead_NullsoftWithNoSwitches_UsesWingetsDefault()
    {
        var command = WinGetManifest.TryRead("""
            Installers:
            - InstallerType: nullsoft
            """)!.CommandFor(InstallerPath);

        command.Should().Be((InstallerPath, "/S"));
    }

    [Fact]
    public void TryRead_InnoWithNoSwitches_UsesWingetsDefault()
    {
        var manifest = WinGetManifest.TryRead("""
            Installers:
            - InstallerType: inno
            """);

        manifest!.Silent.Should().Be("/SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART");
        manifest.SuccessCodes.Should().Equal(0);
    }

    [Fact]
    public void CommandFor_ExeWithNoSilentSwitch_ReturnsNull()
    {
        var manifest = WinGetManifest.TryRead("""
            Installers:
            - InstallerType: exe
              InstallerSwitches:
                Custom: /ALLUSERS
            """);

        manifest.Should().NotBeNull();
        manifest!.CommandFor(InstallerPath).Should().BeNull();
    }

    [Fact]
    public void CommandFor_CustomSwitches_AreAppendedAfterSilent()
    {
        var command = WinGetManifest.TryRead("""
            Installers:
            - InstallerType: exe
              InstallerSwitches:
                Silent: /S
                Custom: /ALLUSERS
            """)!.CommandFor(InstallerPath);

        command.Should().Be((InstallerPath, "/S /ALLUSERS"));
    }

    [Theory]
    [InlineData("InstallerType: [msi")]
    [InlineData("just some text")]
    [InlineData("")]
    public void TryRead_NotAManifest_ReturnsNull(string yaml)
    {
        WinGetManifest.TryRead(yaml).Should().BeNull();
    }

    [Fact]
    public void TryRead_NoInstallerType_ReturnsNull()
    {
        WinGetManifest.TryRead("""
            PackageIdentifier: Test.App
            Installers:
            - InstallerSwitches:
                Silent: /S
            """).Should().BeNull();
    }

    [Theory]
    [InlineData("msi", null, 0, true)]
    [InlineData("WIX", null, 0, true)]
    [InlineData("exe", null, 0, true)]
    [InlineData("msix", null, 0, true)]
    [InlineData("portable", null, 0, true)]
    [InlineData("zip", "portable", 2, true)]
    [InlineData("zip", "inno", 1, true)]
    [InlineData("zip", "portable", 0, false)]
    [InlineData("zip", "msix", 1, false)]
    [InlineData("zip", "font", 3, false)]
    [InlineData("zip", null, 0, false)]
    [InlineData("font", null, 0, false)]
    public void InstallsWithoutWinGet_ByInstallerType(string installerType, string? nestedType, int nestedFiles, bool installs)
    {
        var yaml = $"InstallerType: {installerType}\n"
            + (nestedType == null ? string.Empty : $"NestedInstallerType: {nestedType}\n")
            + (nestedFiles == 0 ? string.Empty : "NestedInstallerFiles:\n")
            + string.Concat(Enumerable.Range(1, nestedFiles).Select(i => $"- RelativeFilePath: app\\file{i}.exe\n"));

        WinGetManifest.TryRead(yaml)!.InstallsWithoutWinGet.Should().Be(installs);
    }

    [Fact]
    public void TryRead_PortableZip_ReadsTheNestedFilesAndTheUninstallEntry()
    {
        var manifest = WinGetManifest.TryRead("""
            PackageIdentifier: Example.Tool
            PackageVersion: 2.1.0
            PackageName: Tool
            Publisher: Example
            PackageUrl: https://example.com/tool
            Installers:
            - InstallerType: zip
              NestedInstallerType: portable
              NestedInstallerFiles:
              - RelativeFilePath: tool\tool.exe
                PortableCommandAlias: tool
              - RelativeFilePath: tool\helper.exe
              AppsAndFeaturesEntries:
              - DisplayName: Example Tool
            """)!;

        manifest.IsPortable.Should().BeTrue();
        manifest.NestedInstallerFiles.Should().Equal(
            new NestedInstallerFile(@"tool\tool.exe", "tool"),
            new NestedInstallerFile(@"tool\helper.exe", null));
        manifest.PackageIdentifier.Should().Be("Example.Tool");
        manifest.DisplayName.Should().Be("Example Tool");
        manifest.DisplayVersion.Should().Be("2.1.0");
        manifest.Publisher.Should().Be("Example");
        manifest.PackageUrl.Should().Be("https://example.com/tool");
    }

    [Fact]
    public void CommandFor_ZipHoldingAnInnoInstaller_UsesTheNestedTypesDefaults()
    {
        const string nested = @"C:\ProgramData\Winhance\Unattend\Apps\app\x64\extracted\setup.exe";

        var manifest = WinGetManifest.TryRead("""
            Installers:
            - InstallerType: zip
              NestedInstallerType: inno
              NestedInstallerFiles:
              - RelativeFilePath: setup.exe
            """)!;

        manifest.CommandFor(nested).Should().Be((nested, "/SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART"));
    }

    [Theory]
    [InlineData("/s /v\"/qn\"")]
    [InlineData("/VERYSILENT /DIR=\"C:\\Program Files\\App\" #1")]
    [InlineData("'quoted': yes")]
    public void Write_RoundTripsThroughTryRead(string silent)
    {
        var manifest = WinGetManifest.TryRead(WinGetManifest.Write("exe", silent));

        manifest!.InstallerType.Should().Be("exe");
        manifest.Silent.Should().Be(silent);
        manifest.CommandFor(InstallerPath).Should().Be((InstallerPath, silent));
    }
}

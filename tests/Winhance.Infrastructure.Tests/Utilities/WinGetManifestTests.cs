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
    [InlineData("msi", true)]
    [InlineData("WIX", true)]
    [InlineData("exe", true)]
    [InlineData("msix", false)]
    [InlineData("zip", false)]
    [InlineData("portable", false)]
    public void IsRunnable_ByInstallerType(string installerType, bool runnable)
    {
        WinGetManifest.IsRunnable(installerType).Should().Be(runnable);
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

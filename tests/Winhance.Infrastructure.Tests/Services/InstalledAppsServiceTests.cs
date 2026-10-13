using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class InstalledAppsServiceTests
{
    private static readonly string[] ToolSubKey = ["Tool"];

    private readonly Mock<IWinGetDetectionService> _winGetDetection = new();
    private readonly Mock<IChocolateyService> _chocolatey = new();
    private readonly Mock<IWinGetBootstrapper> _winGetBootstrapper = new();
    private readonly Mock<IWinGetPackageInstaller> _winGetInstaller = new();

    private readonly Mock<IWindowsRegistryService> _registry = new();
    private readonly Mock<IAppxPackageSource> _appxPackages = new();

    private InstalledAppsService CreateSut() => new(
        _registry.Object,
        _appxPackages.Object,
        _winGetDetection.Object,
        _chocolatey.Object,
        _winGetBootstrapper.Object,
        _winGetInstaller.Object,
        Mock.Of<ILogService>());

    private void GivenOneProgramAndNoPackages()
    {
        const string key = UninstallRegistryReader.Machine + @"\Tool";
        _registry.Setup(r => r.GetSubKeyNames(UninstallRegistryReader.Machine)).Returns(ToolSubKey);
        _registry.Setup(r => r.GetValue(key, "DisplayName")).Returns("Tool");
        _registry.Setup(r => r.GetValue(key, "UninstallString")).Returns("uninstall.exe");
        _appxPackages.Setup(a => a.GetStorePackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _chocolatey.Setup(c => c.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _chocolatey.Setup(c => c.GetOutdatedPackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, string>());
    }

    [Fact]
    public async Task GetInstalledAppsAsync_WhenWinGetCannotBeRead_StillListsProgramsAndSaysUpdatesAreUnknown()
    {
        GivenOneProgramAndNoPackages();
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<WinGetInstalledPackage>?)null);

        var scan = await CreateSut().GetInstalledAppsAsync();

        scan.Apps.Should().ContainSingle().Which.Name.Should().Be("Tool");
        scan.UpdatesKnown.Should().BeFalse();
    }

    [Fact]
    public async Task GetInstalledAppsAsync_WhenWinGetAnswers_SaysUpdatesAreKnown()
    {
        GivenOneProgramAndNoPackages();
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var scan = await CreateSut().GetInstalledAppsAsync();

        scan.Apps.Should().ContainSingle();
        scan.UpdatesKnown.Should().BeTrue();
    }

    [Fact]
    public async Task GetInstalledAppsAsync_WhenWinGetIsOnThePcButCannotBeRead_ChocolateyDoesNotMakeUpdatesKnown()
    {
        GivenOneProgramAndNoPackages();
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<WinGetInstalledPackage>?)null);
        _winGetBootstrapper.Setup(w => w.IsSystemWinGetAvailable).Returns(true);
        _chocolatey.Setup(c => c.IsChocolateyInstalledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var scan = await CreateSut().GetInstalledAppsAsync();

        scan.UpdatesKnown.Should().BeFalse();
    }

    [Fact]
    public async Task GetInstalledAppsAsync_OnAPcWithChocolateyAndNoWinGet_SaysUpdatesAreKnown()
    {
        GivenOneProgramAndNoPackages();
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<WinGetInstalledPackage>?)null);
        _chocolatey.Setup(c => c.IsChocolateyInstalledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var scan = await CreateSut().GetInstalledAppsAsync();

        scan.UpdatesKnown.Should().BeTrue();
    }

    [Fact]
    public async Task GetInstalledAppsAsync_WhenChocolateyCouldNotCheck_StillListsProgramsAndSaysUpdatesAreUnknown()
    {
        GivenOneProgramAndNoPackages();
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _chocolatey.Setup(c => c.GetOutdatedPackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyDictionary<string, string>?)null);

        var scan = await CreateSut().GetInstalledAppsAsync();

        scan.Apps.Should().ContainSingle();
        scan.UpdatesKnown.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_WhenCancelled_StillDropsTheWinGetInstalledList()
    {
        _winGetInstaller
            .Setup(w => w.UpgradePackageAsync("VideoLAN.VLC", "winget", "VLC", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var app = new InstalledApp("reg:machine:VLC", "VLC", InstalledAppKind.Program) { WinGetId = "VideoLAN.VLC", UpdateVia = PackageSources.WinGet };

        var act = () => CreateSut().UpdateAsync(app);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _winGetDetection.Verify(w => w.InvalidateInstalledPackages(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ThroughChocolatey_DropsTheWinGetInstalledList()
    {
        _chocolatey.Setup(c => c.UpgradePackageAsync("vlc", "VLC", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var app = new InstalledApp("reg:machine:VLC", "VLC", InstalledAppKind.Program) { ChocoId = "vlc", UpdateVia = PackageSources.Chocolatey };

        var result = await CreateSut().UpdateAsync(app);

        result.Success.Should().BeTrue();
        _winGetDetection.Verify(w => w.InvalidateInstalledPackages(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ThroughWinGet_UsesTheWinGetSourceAndDropsTheInstalledList()
    {
        _winGetInstaller
            .Setup(w => w.UpgradePackageAsync("VideoLAN.VLC", "winget", "VLC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());
        var app = new InstalledApp("reg:machine:VLC", "VLC", InstalledAppKind.Program) { WinGetId = "VideoLAN.VLC", UpdateVia = PackageSources.WinGet };

        var result = await CreateSut().UpdateAsync(app);

        result.Success.Should().BeTrue();
        _winGetDetection.Verify(w => w.InvalidateInstalledPackages(), Times.Once);
    }
}

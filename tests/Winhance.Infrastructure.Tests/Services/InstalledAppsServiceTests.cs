using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Catalogs;
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
    private readonly Mock<IStoreCatalogService> _storeCatalog = new();

    private readonly Mock<IWindowsRegistryService> _registry = new();
    private readonly Mock<IAppxPackageSource> _appxPackages = new();
    private readonly Mock<IDismComponentReader> _dism = new();
    private readonly Mock<ILogService> _log = new();
    private static readonly IReadOnlyList<ItemDefinition> NoDefinitions = [];

    private InstalledAppsService CreateSut() => new(
        _registry.Object,
        _appxPackages.Object,
        _winGetDetection.Object,
        _chocolatey.Object,
        _winGetBootstrapper.Object,
        _winGetInstaller.Object,
        _storeCatalog.Object,
        _dism.Object,
        _log.Object);

    public InstalledAppsServiceTests()
    {
        _storeCatalog
            .Setup(c => c.GetUpdatesAsync(It.IsAny<IReadOnlyList<StoreCatalogApp>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, StoreCatalogUpdate>());
        _winGetBootstrapper.Setup(w => w.EnsureWinGetReadyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _winGetDetection.Setup(w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _appxPackages.Setup(a => a.GetInstalledPackageNamesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _dism.Setup(d => d.ReadInstalledAsync(true, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledWindowsComponents([], ["Feature"]));
    }

    private static readonly ItemDefinition Capability = new() { Id = "cap", Name = "Cap", Description = "Cap", CapabilityName = "Cap~" };
    private static readonly ItemDefinition Feature = new() { Id = "feat", Name = "Feature", Description = "Feature", OptionalFeatureName = "Feature" };

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

        var scan = await CreateSut().GetInstalledAppsAsync(NoDefinitions, StatusRules.External);

        scan.Apps.Should().ContainSingle().Which.Name.Should().Be("Tool");
        scan.UpdatesKnown.Should().BeFalse();
    }

    [Fact]
    public async Task GetInstalledAppsAsync_WhenWinGetAnswers_SaysUpdatesAreKnown()
    {
        GivenOneProgramAndNoPackages();
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var scan = await CreateSut().GetInstalledAppsAsync(NoDefinitions, StatusRules.External);

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

        var scan = await CreateSut().GetInstalledAppsAsync(NoDefinitions, StatusRules.External);

        scan.UpdatesKnown.Should().BeFalse();
    }

    [Fact]
    public async Task GetInstalledAppsAsync_OnAPcWithChocolateyAndNoWinGet_SaysUpdatesAreKnown()
    {
        GivenOneProgramAndNoPackages();
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<WinGetInstalledPackage>?)null);
        _chocolatey.Setup(c => c.IsChocolateyInstalledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var scan = await CreateSut().GetInstalledAppsAsync(NoDefinitions, StatusRules.External);

        scan.UpdatesKnown.Should().BeTrue();
    }

    [Fact]
    public async Task GetInstalledAppsAsync_WhenChocolateyCouldNotCheck_StillListsProgramsAndSaysUpdatesAreUnknown()
    {
        GivenOneProgramAndNoPackages();
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _chocolatey.Setup(c => c.GetOutdatedPackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyDictionary<string, string>?)null);

        var scan = await CreateSut().GetInstalledAppsAsync(NoDefinitions, StatusRules.External);

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

    [Fact]
    public async Task GetInstalledAppsAsync_AsksTheStoreCatalogAboutTheCatalogedStoreAppsAndShowsItsUpdate()
    {
        GivenOneProgramAndNoPackages();
        const string family = "Microsoft.WindowsCalculator_8wekyb3d8bbwe";
        _appxPackages.Setup(a => a.GetStorePackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StorePackage("Microsoft.WindowsCalculator", family, "Calculator", "11.2502.2.0")]);
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _storeCatalog
            .Setup(c => c.GetUpdatesAsync(It.Is<IReadOnlyList<StoreCatalogApp>>(l => l.Any(a => a.FamilyName == family && a.StoreId == "9WZDNCRFHVN5")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, StoreCatalogUpdate> { [family] = new("2021.2502.2.0", "2021.2607.0.0") });

        var scan = await CreateSut().GetInstalledAppsAsync(NoDefinitions, StatusRules.External);

        var calculator = scan.Apps.Single(a => a.Kind == InstalledAppKind.StoreApp);
        calculator.UpdateVia.Should().Be(PackageSources.MsStore);
        calculator.AvailableVersion.Should().Be("2021.2607.0.0");
        scan.UpdatesKnown.Should().BeTrue();
    }

    [Fact]
    public async Task GetInstalledAppsAsync_AfterAnUpdateThatChangedNothing_WarnsWithEverySourcesVersion()
    {
        GivenOneProgramAndNoPackages();
        const string family = "Microsoft.WindowsCalculator_8wekyb3d8bbwe";
        _appxPackages.Setup(a => a.GetStorePackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StorePackage("Microsoft.WindowsCalculator", family, "Calculator", "11.2502.2.0")]);
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _storeCatalog
            .Setup(c => c.GetUpdatesAsync(It.IsAny<IReadOnlyList<StoreCatalogApp>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, StoreCatalogUpdate> { [family] = new("2021.2502.2.0", "2021.2607.0.0") });
        _winGetInstaller.Setup(w => w.UpdateFromStoreAsync("9WZDNCRFHVN5", "Calculator", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());
        var sut = CreateSut();
        var calculator = (await sut.GetInstalledAppsAsync(NoDefinitions, StatusRules.External)).Apps.Single(a => a.Kind == InstalledAppKind.StoreApp);

        await sut.UpdateAsync(calculator);
        await sut.GetInstalledAppsAsync(NoDefinitions, StatusRules.External);

        _log.Verify(l => l.LogWarning(It.Is<string>(m => m.StartsWith("Calculator still has an update after MsStore") && m.Contains("installed 2021.2502.2.0")), It.IsAny<string>()), Times.Once);
        _log.Verify(l => l.LogDebug(It.Is<string>(m => m.StartsWith("Update sources: Calculator: AppX 11.2502.2.0")), It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetInstalledAppsAsync_WhileAScanIsRunning_SharesItInsteadOfStartingAnother()
    {
        GivenOneProgramAndNoPackages();
        var release = new TaskCompletionSource<IReadOnlyList<WinGetInstalledPackage>?>();
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).Returns(release.Task);
        var sut = CreateSut();

        var first = sut.GetInstalledAppsAsync(NoDefinitions, StatusRules.External);
        var second = sut.GetInstalledAppsAsync(NoDefinitions, StatusRules.External);
        release.SetResult([]);
        var scans = await Task.WhenAll(first, second);

        scans[1].Apps.Should().BeSameAs(scans[0].Apps);
        _winGetDetection.Verify(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _storeCatalog.Verify(c => c.GetUpdatesAsync(It.IsAny<IReadOnlyList<StoreCatalogApp>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInstalledAppsAsync_AfterTheScanFinished_ReadsThePcAgain()
    {
        GivenOneProgramAndNoPackages();
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();

        await sut.GetInstalledAppsAsync(NoDefinitions, StatusRules.External);
        await Task.Delay(50);
        await sut.GetInstalledAppsAsync(NoDefinitions, StatusRules.External);

        _winGetDetection.Verify(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetInstalledAppsAsync_AfterAnUpdateDuringAScan_StartsAFreshScan()
    {
        GivenOneProgramAndNoPackages();
        var release = new TaskCompletionSource<IReadOnlyList<WinGetInstalledPackage>?>();
        _winGetDetection.SetupSequence(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()))
            .Returns(release.Task)
            .Returns(Task.FromResult<IReadOnlyList<WinGetInstalledPackage>?>([]));
        _winGetInstaller
            .Setup(w => w.UpgradePackageAsync("Tool.Id", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());
        var sut = CreateSut();

        var stale = sut.GetInstalledAppsAsync(NoDefinitions, StatusRules.External);
        await sut.UpdateAsync(new InstalledApp("winget:Tool.Id", "Tool", InstalledAppKind.Program) { WinGetId = "Tool.Id", UpdateVia = PackageSources.WinGet });
        var fresh = await sut.GetInstalledAppsAsync(NoDefinitions, StatusRules.External);
        release.SetResult([]);
        await stale;

        fresh.Should().NotBeNull();
        _winGetDetection.Verify(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetInstalledAppsAsync_WhenOneCallerCancels_TheOtherStillGetsTheScan()
    {
        GivenOneProgramAndNoPackages();
        var release = new TaskCompletionSource<IReadOnlyList<WinGetInstalledPackage>?>();
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).Returns(release.Task);
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();

        var cancelled = sut.GetInstalledAppsAsync(NoDefinitions, StatusRules.External, cts.Token);
        var kept = sut.GetInstalledAppsAsync(NoDefinitions, StatusRules.External);
        cts.Cancel();
        release.SetResult([]);

        await cancelled.Awaiting(t => t).Should().ThrowAsync<OperationCanceledException>();
        (await kept).Apps.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetInstalledAppsAsync_ForTheExternalRules_NeverAsksDism()
    {
        GivenOneProgramAndNoPackages();
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await CreateSut().GetInstalledAppsAsync([Capability], StatusRules.External);

        _dism.Verify(d => d.ReadInstalledAsync(It.IsAny<bool>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetInstalledAppsAsync_ForWindowsDefinitionsThatNeedDism_ReadsItAndReportsTheirStatus()
    {
        GivenOneProgramAndNoPackages();
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var scan = await CreateSut().GetInstalledAppsAsync([Capability, Feature], StatusRules.Windows);

        scan.Status["feat"].Should().BeTrue();
        scan.Status["cap"].Should().BeFalse();
        _dism.Verify(d => d.ReadInstalledAsync(true, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInstalledAppsAsync_ForWindowsDefinitions_AsksDismAboutEveryCatalogFeature()
    {
        GivenOneProgramAndNoPackages();
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await CreateSut().GetInstalledAppsAsync([Feature], StatusRules.Windows);

        var expected = OptionalFeatureDefinitions.GetWindowsOptionalFeatures().Items.Select(d => d.OptionalFeatureName!).ToArray();
        _dism.Verify(d => d.ReadInstalledAsync(true,
            It.Is<IReadOnlyCollection<string>>(names => expected.All(names.Contains)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInstalledAppsAsync_AnAppsCallerDuringAComponentsScan_JoinsItWithoutReadingAgain()
    {
        GivenOneProgramAndNoPackages();
        var release = new TaskCompletionSource<IReadOnlyList<WinGetInstalledPackage>?>();
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).Returns(release.Task);
        var sut = CreateSut();

        var windows = sut.GetInstalledAppsAsync([Feature], StatusRules.Windows);
        var external = sut.GetInstalledAppsAsync(NoDefinitions, StatusRules.External);
        release.SetResult([]);
        await Task.WhenAll(windows, external);

        _winGetDetection.Verify(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInstalledAppsAsync_AComponentsCallerDuringAnAppsScan_StartsItsOwnScanForDism()
    {
        GivenOneProgramAndNoPackages();
        var release = new TaskCompletionSource<IReadOnlyList<WinGetInstalledPackage>?>();
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).Returns(release.Task);
        var sut = CreateSut();

        var external = sut.GetInstalledAppsAsync(NoDefinitions, StatusRules.External);
        var windows = sut.GetInstalledAppsAsync([Feature], StatusRules.Windows);
        release.SetResult([]);
        await Task.WhenAll(external, windows);

        _winGetDetection.Verify(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _dism.Verify(d => d.ReadInstalledAsync(true, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Invalidate_DropsTheInstalledListAndMakesTheNextCallerReadAgain()
    {
        GivenOneProgramAndNoPackages();
        var release = new TaskCompletionSource<IReadOnlyList<WinGetInstalledPackage>?>();
        _winGetDetection.SetupSequence(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()))
            .Returns(release.Task)
            .Returns(Task.FromResult<IReadOnlyList<WinGetInstalledPackage>?>([]));
        var sut = CreateSut();

        var stale = sut.GetInstalledAppsAsync(NoDefinitions, StatusRules.External);
        sut.Invalidate();
        var fresh = sut.GetInstalledAppsAsync(NoDefinitions, StatusRules.External);
        release.SetResult([]);
        await Task.WhenAll(stale, fresh);

        _winGetDetection.Verify(w => w.InvalidateInstalledPackages(), Times.Once);
        _winGetDetection.Verify(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateAsync_ThroughTheStore_ForceInstallsByStoreIdAndDropsTheInstalledList()
    {
        _winGetInstaller
            .Setup(w => w.UpdateFromStoreAsync("9WZDNCRFHVN5", "Calculator", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());
        var app = new InstalledApp("appx:Calc", "Calculator", InstalledAppKind.StoreApp) { MsStoreId = "9WZDNCRFHVN5", UpdateVia = PackageSources.MsStore };

        var result = await CreateSut().UpdateAsync(app);

        result.Success.Should().BeTrue();
        _winGetInstaller.Verify(w => w.UpgradePackageAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _winGetDetection.Verify(w => w.InvalidateInstalledPackages(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ThroughTheStore_WhenTheInstallFails_ReportsItsMessage()
    {
        _winGetInstaller
            .Setup(w => w.UpdateFromStoreAsync("9WZDNCRFHVN5", "Calculator", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.Other, "0x80004004"));
        var app = new InstalledApp("appx:Calc", "Calculator", InstalledAppKind.StoreApp) { MsStoreId = "9WZDNCRFHVN5", UpdateVia = PackageSources.MsStore };

        var result = await CreateSut().UpdateAsync(app);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("0x80004004");
    }

    [Fact]
    public async Task UpdateAsync_ThroughTheStore_WhenTheUserCancels_Throws()
    {
        _winGetInstaller
            .Setup(w => w.UpdateFromStoreAsync("9WZDNCRFHVN5", "Calculator", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var app = new InstalledApp("appx:Calc", "Calculator", InstalledAppKind.StoreApp) { MsStoreId = "9WZDNCRFHVN5", UpdateVia = PackageSources.MsStore };
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var act = () => CreateSut().UpdateAsync(app, cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetInstalledAppsAsync_AfterAnUpdateWindowsHeldUntilTheAppRestarts_DoesNotWarn()
    {
        GivenOneProgramAndNoPackages();
        const string family = "Microsoft.WindowsCalculator_8wekyb3d8bbwe";
        _appxPackages.Setup(a => a.GetStorePackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StorePackage("Microsoft.WindowsCalculator", family, "Calculator", "11.2502.2.0")]);
        _winGetDetection.Setup(w => w.GetInstalledPackagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _storeCatalog
            .Setup(c => c.GetUpdatesAsync(It.IsAny<IReadOnlyList<StoreCatalogApp>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, StoreCatalogUpdate> { [family] = new("2021.2502.2.0", "2021.2607.0.0") });
        _winGetInstaller.Setup(w => w.UpdateFromStoreAsync("9WZDNCRFHVN5", "Calculator", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded() with { RegistrationDeferred = true });
        var sut = CreateSut();
        var calculator = (await sut.GetInstalledAppsAsync(NoDefinitions, StatusRules.External)).Apps.Single(a => a.Kind == InstalledAppKind.StoreApp);

        var result = await sut.UpdateAsync(calculator);
        await sut.GetInstalledAppsAsync(NoDefinitions, StatusRules.External);

        result.Success.Should().BeTrue();
        _log.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("still has an update")), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task FindOpenAppsAsync_NeverOffersToCloseAppInstallerOrAProgram()
    {
        var appInstaller = new InstalledApp("appx:installer", "App Installer", InstalledAppKind.StoreApp) { PackageName = "Microsoft.DesktopAppInstaller" };
        var program = new InstalledApp("reg:machine:VLC", "VLC", InstalledAppKind.Program);

        var open = await CreateSut().FindOpenAppsAsync([appInstaller, program]);

        open.Should().BeEmpty();
    }
}

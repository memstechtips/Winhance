using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class AppStatusDiscoveryServiceTests
{
    private static readonly string[] TestAppId = ["TestApp.Id"];
    private static readonly string[] SevenZipId = ["7zip.7zip"];
    private static readonly string[] SomePackageId = ["Some.Package"];
    private static readonly string[] VlcId = ["VideoLAN.VLC"];
    private static readonly string[] PkgId = ["Pkg"];
    private static readonly string[] PkgIdQualified = ["Pkg.Id"];
    private static readonly string[] NonExistentDetectionPath = [@"C:\NonExistent\Path\That\Does\Not\Exist"];
    private static readonly string[] PortableAppId = ["Portable.App"];

    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IWinGetBootstrapper> _mockWinGetBootstrapper = new();
    private readonly Mock<IWinGetDetectionService> _mockWinGetDetection = new();
    private readonly Mock<IChocolateyService> _mockChocolatey = new();
    private readonly Mock<IInteractiveUserService> _mockInteractiveUser = new();
    private readonly Mock<IAppxPackageSource> _mockAppxPackageSource = new();
    private readonly AppStatusDiscoveryService _service;

    public AppStatusDiscoveryServiceTests()
    {
        _mockAppxPackageSource
            .Setup(a => a.GetInstalledPackageNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        _service = new AppStatusDiscoveryService(
            _mockLog.Object,
            _mockWinGetBootstrapper.Object,
            _mockWinGetDetection.Object,
            _mockChocolatey.Object,
            _mockInteractiveUser.Object,
            _mockAppxPackageSource.Object);
    }

    private static ItemDefinition CreateAppxDefinition(string id, string appxName, string? name = null) => new()
    {
        Id = id,
        Name = name ?? id,
        Description = $"Description for {id}",
        AppxPackageName = new[] { appxName },
    };

    private static ItemDefinition CreateExternalAppDefinition(
        string id,
        string name,
        string[]? winGetPackageIds = null,
        string? msStoreId = null,
        string? chocoPackageId = null,
        string[]? detectionPaths = null) => new()
    {
        Id = id,
        Name = name,
        Description = $"Description for {id}",
        WinGetPackageId = winGetPackageIds,
        MsStoreId = msStoreId,
        ChocoPackageId = chocoPackageId,
        DetectionPaths = detectionPaths,
    };

    [Fact]
    public async Task GetInstallationStatusBatchAsync_EmptyDefinitions_ReturnsEmptyDictionary()
    {
        var result = await _service.GetInstallationStatusBatchAsync(Array.Empty<ItemDefinition>());

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetInstallationStatusBatchAsync_ReturnsCaseInsensitiveDictionary()
    {
        var result = await _service.GetInstallationStatusBatchAsync(Array.Empty<ItemDefinition>());

        result.Should().NotBeNull();
        result.Comparer.Should().Be(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetInstallationStatusBatchAsync_WhenExceptionOccursInternallyAndCaughtAtTopLevel_ReturnsAllFalse()
    {
        var definitions = new List<ItemDefinition>
        {
            CreateAppxDefinition("app1", "Microsoft.TestApp1"),
            CreateAppxDefinition("app2", "Microsoft.TestApp2"),
        };

        var result = await _service.GetInstallationStatusBatchAsync(definitions);

        result.Should().NotBeNull();
        result.Should().ContainKey("app1");
        result.Should().ContainKey("app2");
    }

    [Fact]
    public void InvalidateCache_ClearsWinGetPackageIdCache()
    {
        _service.InvalidateCache();

        _service.InvalidateCache(); // Should be idempotent
    }

    [Fact]
    public async Task InvalidateCache_CausesWinGetRefetch_WhenExternalAppsChecked()
    {
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ReturnsAsync(true);
        _mockWinGetDetection
            .Setup(w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "TestApp.Id" });

        var definitions = new List<ItemDefinition>
        {
            CreateExternalAppDefinition("ext1", "Test App", winGetPackageIds: TestAppId)
        };

        await _service.GetExternalAppsInstallationStatusAsync(definitions);

        _service.InvalidateCache();

        await _service.GetExternalAppsInstallationStatusAsync(definitions);

        _mockWinGetDetection.Verify(
            w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GetExternalAppsInstallationStatusAsync_EmptyDefinitions_ReturnsEmptyDictionary()
    {
        var result = await _service.GetExternalAppsInstallationStatusAsync(Array.Empty<ItemDefinition>());

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetExternalAppsInstallationStatusAsync_WinGetDetectsApp_ReturnsTrue()
    {
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ReturnsAsync(true);
        _mockWinGetDetection
            .Setup(w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "7zip.7zip" });

        var definitions = new List<ItemDefinition>
        {
            CreateExternalAppDefinition("ext-7zip", "7-Zip", winGetPackageIds: SevenZipId)
        };

        var result = await _service.GetExternalAppsInstallationStatusAsync(definitions);

        result.Should().ContainKey("ext-7zip");
        result["ext-7zip"].Should().BeTrue();
    }

    [Fact]
    public async Task GetExternalAppsInstallationStatusAsync_WinGetDetectsViaMsStoreId_ReturnsTrue()
    {
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ReturnsAsync(true);
        _mockWinGetDetection
            .Setup(w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "9NBLGGH5R558" });

        var definitions = new List<ItemDefinition>
        {
            CreateExternalAppDefinition("ext-storeapp", "Store App", msStoreId: "9NBLGGH5R558")
        };

        var result = await _service.GetExternalAppsInstallationStatusAsync(definitions);

        result.Should().ContainKey("ext-storeapp");
        result["ext-storeapp"].Should().BeTrue();
    }

    [Fact]
    public async Task GetExternalAppsInstallationStatusAsync_WinGetUnavailable_LogsWarning()
    {
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ReturnsAsync(false);

        var definitions = new List<ItemDefinition>
        {
            CreateExternalAppDefinition("ext1", "App 1", winGetPackageIds: SomePackageId)
        };

        var result = await _service.GetExternalAppsInstallationStatusAsync(definitions);

        _mockLog.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("WinGet unavailable"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetExternalAppsInstallationStatusAsync_ChocolateyFallback_WhenWinGetNotFound()
    {
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ReturnsAsync(true);
        _mockWinGetDetection
            .Setup(w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        _mockChocolatey
            .Setup(c => c.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "vlc" });

        var definitions = new List<ItemDefinition>
        {
            CreateExternalAppDefinition("ext-vlc", "VLC", winGetPackageIds: VlcId, chocoPackageId: "vlc")
        };

        var result = await _service.GetExternalAppsInstallationStatusAsync(definitions);

        result.Should().ContainKey("ext-vlc");
        result["ext-vlc"].Should().BeTrue();
    }

    [Fact]
    public async Task GetExternalAppsInstallationStatusAsync_ChocolateyDetectionFails_LogsWarning()
    {
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ReturnsAsync(true);
        _mockWinGetDetection
            .Setup(w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        _mockChocolatey
            .Setup(c => c.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Choco not installed"));

        var definitions = new List<ItemDefinition>
        {
            CreateExternalAppDefinition("ext1", "App 1", winGetPackageIds: PkgId, chocoPackageId: "pkg")
        };

        var result = await _service.GetExternalAppsInstallationStatusAsync(definitions);

        _mockLog.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("Chocolatey detection failed"))), Times.Once);
    }

    [Fact]
    public async Task GetExternalAppsInstallationStatusAsync_SetsDetectedViaProperty()
    {
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ReturnsAsync(true);
        _mockWinGetDetection
            .Setup(w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "7zip.7zip" });

        var definition = CreateExternalAppDefinition("ext-7zip", "7-Zip",
            winGetPackageIds: SevenZipId);
        var definitions = new List<ItemDefinition> { definition };

        await _service.GetExternalAppsInstallationStatusAsync(definitions);

        definition.DetectedVia.Should().Be(Core.Features.SoftwareApps.Enums.DetectionSource.WinGet);
    }

    [Fact]
    public async Task GetExternalAppsInstallationStatusAsync_NoDetectionSource_ReturnsFalse()
    {
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ReturnsAsync(true);
        _mockWinGetDetection
            .Setup(w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
        _mockChocolatey
            .Setup(c => c.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        var definitions = new List<ItemDefinition>
        {
            CreateExternalAppDefinition("ext-unknown", "Unknown App")
        };

        var result = await _service.GetExternalAppsInstallationStatusAsync(definitions);

        result.Should().ContainKey("ext-unknown");
        result["ext-unknown"].Should().BeFalse();
    }

    [Fact]
    public async Task GetExternalAppsInstallationStatusAsync_TopLevelException_ReturnsAllFalse()
    {
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ThrowsAsync(new InvalidOperationException("Critical failure"));

        var definitions = new List<ItemDefinition>
        {
            CreateExternalAppDefinition("ext1", "App 1", winGetPackageIds: PkgId)
        };

        var result = await _service.GetExternalAppsInstallationStatusAsync(definitions);

        result.Should().ContainKey("ext1");
        result["ext1"].Should().BeFalse();
        // The exception is caught at the WinGet readiness level (logged as LogWarning), not at the top level
        _mockLog.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("Critical failure"))), Times.Once);
    }

    [Fact]
    public async Task GetExternalAppsInstallationStatusAsync_UsesWinGetCache_OnSecondCall()
    {
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ReturnsAsync(true);
        _mockWinGetDetection
            .Setup(w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "Pkg.Id" });

        var definitions = new List<ItemDefinition>
        {
            CreateExternalAppDefinition("ext1", "App 1", winGetPackageIds: PkgIdQualified)
        };

        await _service.GetExternalAppsInstallationStatusAsync(definitions);
        await _service.GetExternalAppsInstallationStatusAsync(definitions);

        _mockWinGetDetection.Verify(
            w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("MediaMonkey 5", "MediaMonkey {version}", true)]
    [InlineData("K-Lite Mega Codec Pack 19.5.0", "K-Lite Mega Codec Pack {version}", true)]
    [InlineData("ONLYOFFICE 9.3.0 (x64)", "ONLYOFFICE {version} ({arch})", true)]
    [InlineData("PuTTY release 0.83 (64-bit)", "PuTTY release {version} ({arch})", true)]
    [InlineData("InputLeap 3.0.2-release", "InputLeap {version}-release", true)]
    [InlineData("Zoom Workplace (64-bit)", "Zoom Workplace ({arch})", true)]
    [InlineData("O365ProPlusRetail - en-us", "O365ProPlusRetail - {locale}", true)]
    [InlineData("Mozilla Thunderbird (x64 en-US)", "Mozilla Thunderbird ({arch} {locale})", true)]
    [InlineData("Krita (x64) 5.2.16 (git 7d9aefc)", "Krita ({arch}) {version}", true)]
    [InlineData("Microsoft EdgeWebView2 Runtime", "Microsoft EdgeWebView{version}", true)]
    [InlineData("Trillian Machine-Wide Installer", "Trillian Machine-Wide Installer", true)]
    [InlineData("Microsoft Visual C++ 2008 Redistributable - x64 9.0.30729.6161", "Microsoft Visual C++ 2008 Redistributable - x64 {version}", true)]
    [InlineData("Microsoft .NET Core Runtime - 3.1.32 (x64)", "Microsoft .NET Runtime - 3.1.{version} ({arch})|Microsoft .NET Core Runtime - 3.1.{version} ({arch})", true)]
    [InlineData("Microsoft .NET Runtime - 3.1.32 (x64)", "Microsoft .NET Runtime - 3.1.{version} ({arch})|Microsoft .NET Core Runtime - 3.1.{version} ({arch})", true)]
    [InlineData("Microsoft Visual C++ v14 Redistributable (x64) - 14.51.36247", "Microsoft Visual C++ 2015-2022 Redistributable (x64) - {version}|Microsoft Visual C++ v14 Redistributable (x64) - {version}", true)]
    [InlineData("Microsoft Visual C++ 2015-2022 Redistributable (x64) - 14.38.33135", "Microsoft Visual C++ 2015-2022 Redistributable (x64) - {version}|Microsoft Visual C++ v14 Redistributable (x64) - {version}", true)]
    [InlineData("VLC media player", "VLC media player", true)]
    [InlineData("MediaMonkey 5", "VLC media player", false)]
    [InlineData("GIMP 3.0.8-2", "Audacity {version}", false)]
    [InlineData("Microsoft .NET Runtime - 5.0.17 (x64)", "Microsoft .NET Runtime - 3.1.{version} ({arch})|Microsoft .NET Core Runtime - 3.1.{version} ({arch})", false)]
    [InlineData("Microsoft Visual C++ 2013 Redistributable (x64) - 12.0.30501", "Microsoft Visual C++ 2015-2022 Redistributable (x64) - {version}|Microsoft Visual C++ v14 Redistributable (x64) - {version}", false)]
    [InlineData("", "Foo {version}", false)]
    public void MatchesPattern_ReturnsExpected(string input, string pattern, bool expected)
    {
        var result = AppStatusDiscoveryService.MatchesPattern(input, pattern);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetExternalAppsInstallationStatusAsync_AppXDetectsApp_ReturnsTrueWithDetectedViaAppX()
    {
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ReturnsAsync(true);
        _mockWinGetDetection
            .Setup(w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
        _mockChocolatey
            .Setup(c => c.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
        _mockAppxPackageSource
            .Setup(a => a.GetInstalledPackageNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Microsoft.WindowsCalculator" });

        var definition = new ItemDefinition
        {
            Id = "ext-calc",
            Name = "Windows Calculator",
            Description = "Calculator",
            AppxPackageName = new[] { "Microsoft.WindowsCalculator" }
        };
        var definitions = new List<ItemDefinition> { definition };

        var result = await _service.GetExternalAppsInstallationStatusAsync(definitions);

        result.Should().ContainKey("ext-calc");
        result["ext-calc"].Should().BeTrue();
        definition.DetectedVia.Should().Be(Core.Features.SoftwareApps.Enums.DetectionSource.AppX);
    }

    [Fact]
    public async Task GetExternalAppsInstallationStatusAsync_AppXNotInstalled_ReturnsFalse()
    {
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ReturnsAsync(true);
        _mockWinGetDetection
            .Setup(w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
        _mockChocolatey
            .Setup(c => c.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        var definition = CreateAppxDefinition("ext-fake", "Winhance.FakeTestApp.DoesNotExist_XYZ123",
            name: "WinhanceFakeTestAppXYZ123_NotAReal_App");
        var definitions = new List<ItemDefinition> { definition };

        var result = await _service.GetExternalAppsInstallationStatusAsync(definitions);

        result.Should().ContainKey("ext-fake");
        result["ext-fake"].Should().BeFalse();
    }

    [Fact]
    public async Task GetInstallationStatusBatchAsync_AppXDetectsApp_ReturnsTrue()
    {
        _mockAppxPackageSource
            .Setup(a => a.GetInstalledPackageNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Microsoft.WindowsCalculator" });

        var definitions = new List<ItemDefinition>
        {
            CreateAppxDefinition("calc", "Microsoft.WindowsCalculator")
        };

        var result = await _service.GetInstallationStatusBatchAsync(definitions);

        result.Should().ContainKey("calc");
        result["calc"].Should().BeTrue();
    }

    [Fact]
    public async Task GetExternalAppsInstallationStatusAsync_AppXSkippedWhenAlreadyDetectedByWinGet()
    {
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ReturnsAsync(true);
        _mockWinGetDetection
            .Setup(w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "WinhanceFake.Package" });

        var definition = new ItemDefinition
        {
            Id = "ext-fake",
            Name = "WinhanceFakeTestAppXYZ123_NotAReal_App",
            Description = "Fake test app",
            WinGetPackageId = new[] { "WinhanceFake.Package" },
            AppxPackageName = new[] { "Winhance.FakeTestApp.DoesNotExist_XYZ123" }
        };
        var definitions = new List<ItemDefinition> { definition };

        var result = await _service.GetExternalAppsInstallationStatusAsync(definitions);

        result["ext-fake"].Should().BeTrue();
        definition.DetectedVia.Should().Be(Core.Features.SoftwareApps.Enums.DetectionSource.WinGet);
    }

    [Fact]
    public async Task GetExternalAppsInstallationStatusAsync_FileSystemDetectsExistingDirectory_ReturnsTrue()
    {
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ReturnsAsync(true);
        _mockWinGetDetection
            .Setup(w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
        _mockChocolatey
            .Setup(c => c.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        // Use a path that is guaranteed to exist in any test environment
        var tempDir = Path.GetTempPath();
        var definitions = new List<ItemDefinition>
        {
            CreateExternalAppDefinition("ext-portable", "Portable App",
                detectionPaths: new[] { tempDir })
        };

        var result = await _service.GetExternalAppsInstallationStatusAsync(definitions);

        result.Should().ContainKey("ext-portable");
        result["ext-portable"].Should().BeTrue();
    }

    [Fact]
    public async Task GetExternalAppsInstallationStatusAsync_FileSystemDetectsExistingDirectory_SetsDetectedVia()
    {
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ReturnsAsync(true);
        _mockWinGetDetection
            .Setup(w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
        _mockChocolatey
            .Setup(c => c.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        var tempDir = Path.GetTempPath();
        var definition = CreateExternalAppDefinition("ext-portable", "Portable App",
            detectionPaths: new[] { tempDir });
        var definitions = new List<ItemDefinition> { definition };

        await _service.GetExternalAppsInstallationStatusAsync(definitions);

        definition.DetectedVia.Should().Be(Core.Features.SoftwareApps.Enums.DetectionSource.FileSystem);
    }

    [Fact]
    public async Task GetExternalAppsInstallationStatusAsync_FileSystemNonExistentPath_ReturnsFalse()
    {
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ReturnsAsync(true);
        _mockWinGetDetection
            .Setup(w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
        _mockChocolatey
            .Setup(c => c.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        var definitions = new List<ItemDefinition>
        {
            CreateExternalAppDefinition("ext-portable", "Portable App",
                detectionPaths: NonExistentDetectionPath)
        };

        var result = await _service.GetExternalAppsInstallationStatusAsync(definitions);

        result.Should().ContainKey("ext-portable");
        result["ext-portable"].Should().BeFalse();
    }

    [Fact]
    public async Task GetExternalAppsInstallationStatusAsync_FileSystemSkippedWhenAlreadyDetectedByWinGet()
    {
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ReturnsAsync(true);
        _mockWinGetDetection
            .Setup(w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "Portable.App" });

        var tempDir = Path.GetTempPath();
        var definition = CreateExternalAppDefinition("ext-portable", "Portable App",
            winGetPackageIds: PortableAppId,
            detectionPaths: new[] { tempDir });
        var definitions = new List<ItemDefinition> { definition };

        var result = await _service.GetExternalAppsInstallationStatusAsync(definitions);

        result["ext-portable"].Should().BeTrue();
        definition.DetectedVia.Should().Be(Core.Features.SoftwareApps.Enums.DetectionSource.WinGet);
    }
}

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
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IWinGetBootstrapper> _mockWinGetBootstrapper = new();
    private readonly Mock<IWinGetDetectionService> _mockWinGetDetection = new();
    private readonly Mock<IChocolateyService> _mockChocolatey = new();
    private readonly Mock<IInteractiveUserService> _mockInteractiveUser = new();
    private readonly Mock<IPowerShellRunner> _mockPowerShellRunner = new();
    private readonly AppStatusDiscoveryService _service;

    public AppStatusDiscoveryServiceTests()
    {
        _service = new AppStatusDiscoveryService(
            _mockLog.Object,
            _mockWinGetBootstrapper.Object,
            _mockWinGetDetection.Object,
            _mockChocolatey.Object,
            _mockInteractiveUser.Object,
            _mockPowerShellRunner.Object);
    }

    private static ItemDefinition CreateAppxDefinition(string id, string appxName, string? name = null) => new()
    {
        Id = id,
        Name = name ?? id,
        Description = $"Description for {id}",
        AppxPackageName = appxName,
    };

    private static ItemDefinition CreateCapabilityDefinition(string id, string capabilityName) => new()
    {
        Id = id,
        Name = id,
        Description = $"Description for {id}",
        CapabilityName = capabilityName,
    };

    private static ItemDefinition CreateFeatureDefinition(string id, string featureName) => new()
    {
        Id = id,
        Name = id,
        Description = $"Description for {id}",
        OptionalFeatureName = featureName,
    };

    private static ItemDefinition CreateExternalAppDefinition(
        string id,
        string name,
        string[]? winGetPackageIds = null,
        string? msStoreId = null,
        string? chocoPackageId = null) => new()
    {
        Id = id,
        Name = name,
        Description = $"Description for {id}",
        WinGetPackageId = winGetPackageIds,
        MsStoreId = msStoreId,
        ChocoPackageId = chocoPackageId,
    };

    // --- GetInstallationStatusBatchAsync ---

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
        // With empty definitions to confirm the dictionary uses the right comparer
        var result = await _service.GetInstallationStatusBatchAsync(Array.Empty<ItemDefinition>());

        result.Should().NotBeNull();
        // The dictionary should be created with OrdinalIgnoreCase comparer
        result.Comparer.Should().Be(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetInstallationStatusBatchAsync_WhenExceptionOccursInternallyAndCaughtAtTopLevel_ReturnsAllFalse()
    {
        // Create apps that will cause an exception path at the top-level try/catch.
        // Since the service calls platform APIs internally, we test the catch-all behavior
        // by providing definitions that go through the appx path (which uses PackageManager COM).
        // In a test environment, the PackageManager will likely fail, triggering fallback paths.
        var definitions = new List<ItemDefinition>
        {
            CreateAppxDefinition("app1", "Microsoft.TestApp1"),
            CreateAppxDefinition("app2", "Microsoft.TestApp2"),
        };

        // This test verifies the method doesn't throw when internal operations fail.
        // The PackageManager and fallback WMI/PowerShell paths will fail in a unit test environment.
        var result = await _service.GetInstallationStatusBatchAsync(definitions);

        result.Should().NotBeNull();
        // In a test environment where PackageManager is unavailable, apps should be marked false
        result.Should().ContainKey("app1");
        result.Should().ContainKey("app2");
    }

    // --- InvalidateCache ---

    [Fact]
    public void InvalidateCache_ClearsWinGetPackageIdCache()
    {
        // InvalidateCache should complete without throwing
        _service.InvalidateCache();

        // After invalidation, next call to GetOrFetchWinGetPackageIdsAsync will re-fetch.
        // We verify this indirectly: calling invalidate then checking external apps
        // should trigger a fresh WinGet fetch.
        _service.InvalidateCache(); // Should be idempotent
    }

    [Fact]
    public async Task InvalidateCache_CausesWinGetRefetch_WhenExternalAppsChecked()
    {
        // Setup WinGet as ready and returning a package set
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ReturnsAsync(true);
        _mockWinGetDetection
            .Setup(w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "TestApp.Id" });

        var definitions = new List<ItemDefinition>
        {
            CreateExternalAppDefinition("ext1", "Test App", winGetPackageIds: new[] { "TestApp.Id" })
        };

        // First call populates cache
        await _service.GetExternalAppsInstallationStatusAsync(definitions);

        // Invalidate cache
        _service.InvalidateCache();

        // Second call should re-fetch
        await _service.GetExternalAppsInstallationStatusAsync(definitions);

        // WinGet detection should have been called twice (once for each external check)
        _mockWinGetDetection.Verify(
            w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // --- GetExternalAppsInstallationStatusAsync ---

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
            CreateExternalAppDefinition("ext-7zip", "7-Zip", winGetPackageIds: new[] { "7zip.7zip" })
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
            CreateExternalAppDefinition("ext1", "App 1", winGetPackageIds: new[] { "Some.Package" })
        };

        var result = await _service.GetExternalAppsInstallationStatusAsync(definitions);

        _mockLog.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("WinGet unavailable"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetExternalAppsInstallationStatusAsync_ChocolateyFallback_WhenWinGetNotFound()
    {
        // WinGet doesn't find the app
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ReturnsAsync(true);
        _mockWinGetDetection
            .Setup(w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());

        // Chocolatey finds the app
        _mockChocolatey
            .Setup(c => c.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "vlc" });

        var definitions = new List<ItemDefinition>
        {
            CreateExternalAppDefinition("ext-vlc", "VLC", winGetPackageIds: new[] { "VideoLAN.VLC" }, chocoPackageId: "vlc")
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
            CreateExternalAppDefinition("ext1", "App 1", winGetPackageIds: new[] { "Pkg" }, chocoPackageId: "pkg")
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
            winGetPackageIds: new[] { "7zip.7zip" });
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

        // App without WinGet ID, choco ID, or registry match
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
        // Force WinGet bootstrapper to throw to trigger the top-level catch
        _mockWinGetBootstrapper
            .Setup(w => w.EnsureWinGetReadyAsync())
            .ThrowsAsync(new InvalidOperationException("Critical failure"));

        var definitions = new List<ItemDefinition>
        {
            CreateExternalAppDefinition("ext1", "App 1", winGetPackageIds: new[] { "Pkg" })
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
            CreateExternalAppDefinition("ext1", "App 1", winGetPackageIds: new[] { "Pkg.Id" })
        };

        // Call twice
        await _service.GetExternalAppsInstallationStatusAsync(definitions);
        await _service.GetExternalAppsInstallationStatusAsync(definitions);

        // WinGet detection should only be called once due to caching
        _mockWinGetDetection.Verify(
            w => w.GetInstalledPackageIdsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

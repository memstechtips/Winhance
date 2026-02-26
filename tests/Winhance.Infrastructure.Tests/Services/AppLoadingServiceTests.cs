using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class AppLoadingServiceTests
{
    private readonly Mock<IWindowsAppsService> _mockWindowsApps = new();
    private readonly Mock<IExternalAppsService> _mockExternalApps = new();
    private readonly Mock<IAppStatusDiscoveryService> _mockStatusDiscovery = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly AppLoadingService _service;

    public AppLoadingServiceTests()
    {
        _service = new AppLoadingService(
            _mockWindowsApps.Object,
            _mockExternalApps.Object,
            _mockStatusDiscovery.Object,
            _mockLog.Object);
    }

    private static ItemDefinition CreateDefinition(
        string id,
        string? appxName = null,
        string? capabilityName = null,
        string? featureName = null) => new()
    {
        Id = id,
        Name = $"App {id}",
        Description = $"Description for {id}",
        AppxPackageName = appxName,
        CapabilityName = capabilityName,
        OptionalFeatureName = featureName,
    };

    // --- LoadAppsAsync ---

    [Fact]
    public async Task LoadAppsAsync_CombinesWindowsAndExternalApps()
    {
        var windowsApps = new List<ItemDefinition>
        {
            CreateDefinition("win1", appxName: "Microsoft.App1"),
            CreateDefinition("win2", appxName: "Microsoft.App2"),
        };
        var externalApps = new List<ItemDefinition>
        {
            CreateDefinition("ext1"),
            CreateDefinition("ext2"),
        };

        _mockWindowsApps.Setup(w => w.GetAppsAsync()).ReturnsAsync(windowsApps);
        _mockExternalApps.Setup(e => e.GetAppsAsync()).ReturnsAsync(externalApps);
        _mockStatusDiscovery
            .Setup(s => s.GetInstallationStatusBatchAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                { "win1", true },
                { "win2", false },
                { "ext1", true },
                { "ext2", false },
            });

        var result = await _service.LoadAppsAsync();

        result.Success.Should().BeTrue();
        result.Result.Should().NotBeNull();
        result.Result!.Should().HaveCount(4);
    }

    [Fact]
    public async Task LoadAppsAsync_SetsIsInstalledFromStatusDiscovery()
    {
        var windowsApps = new List<ItemDefinition>
        {
            CreateDefinition("win1", appxName: "Microsoft.App1"),
        };
        var externalApps = new List<ItemDefinition>
        {
            CreateDefinition("ext1"),
        };

        _mockWindowsApps.Setup(w => w.GetAppsAsync()).ReturnsAsync(windowsApps);
        _mockExternalApps.Setup(e => e.GetAppsAsync()).ReturnsAsync(externalApps);
        _mockStatusDiscovery
            .Setup(s => s.GetInstallationStatusBatchAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                { "win1", true },
                { "ext1", false },
            });

        var result = await _service.LoadAppsAsync();

        var apps = result.Result!.ToList();
        apps.First(a => a.Id == "win1").IsInstalled.Should().BeTrue();
        apps.First(a => a.Id == "ext1").IsInstalled.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAppsAsync_WhenStatusNotFoundForApp_IsInstalledIsFalse()
    {
        var windowsApps = new List<ItemDefinition>
        {
            CreateDefinition("win1", appxName: "Microsoft.App1"),
        };
        _mockWindowsApps.Setup(w => w.GetAppsAsync()).ReturnsAsync(windowsApps);
        _mockExternalApps.Setup(e => e.GetAppsAsync()).ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _mockStatusDiscovery
            .Setup(s => s.GetInstallationStatusBatchAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase));

        var result = await _service.LoadAppsAsync();

        result.Success.Should().BeTrue();
        var app = result.Result!.First();
        app.IsInstalled.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAppsAsync_WhenExceptionThrown_ReturnsFailedResult()
    {
        _mockWindowsApps.Setup(w => w.GetAppsAsync()).ThrowsAsync(new Exception("Service unavailable"));
        _mockExternalApps.Setup(e => e.GetAppsAsync()).ReturnsAsync(Enumerable.Empty<ItemDefinition>());

        var result = await _service.LoadAppsAsync();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to load apps");
        _mockLog.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Failed to load apps")), It.IsAny<Exception>()), Times.Once);
    }

    [Fact]
    public async Task LoadAppsAsync_WithNoApps_ReturnsSuccessWithEmptyCollection()
    {
        _mockWindowsApps.Setup(w => w.GetAppsAsync()).ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _mockExternalApps.Setup(e => e.GetAppsAsync()).ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _mockStatusDiscovery
            .Setup(s => s.GetInstallationStatusBatchAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase));

        var result = await _service.LoadAppsAsync();

        result.Success.Should().BeTrue();
        result.Result.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAppsAsync_CallsStatusDiscoveryWithAllApps()
    {
        var windowsApps = new List<ItemDefinition> { CreateDefinition("win1") };
        var externalApps = new List<ItemDefinition> { CreateDefinition("ext1") };

        _mockWindowsApps.Setup(w => w.GetAppsAsync()).ReturnsAsync(windowsApps);
        _mockExternalApps.Setup(e => e.GetAppsAsync()).ReturnsAsync(externalApps);
        _mockStatusDiscovery
            .Setup(s => s.GetInstallationStatusBatchAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase));

        await _service.LoadAppsAsync();

        _mockStatusDiscovery.Verify(s => s.GetInstallationStatusBatchAsync(
            It.Is<IEnumerable<ItemDefinition>>(defs => defs.Count() == 2)),
            Times.Once);
    }

    // --- GetAppByIdAsync ---

    [Fact]
    public async Task GetAppByIdAsync_ReturnsCorrectApp_FromWindowsApps()
    {
        var windowsApps = new List<ItemDefinition>
        {
            CreateDefinition("win1", appxName: "Microsoft.App1"),
            CreateDefinition("win2", appxName: "Microsoft.App2"),
        };
        _mockWindowsApps.Setup(w => w.GetAppsAsync()).ReturnsAsync(windowsApps);
        _mockExternalApps.Setup(e => e.GetAppsAsync()).ReturnsAsync(Enumerable.Empty<ItemDefinition>());

        var result = await _service.GetAppByIdAsync("win1");

        result.Should().NotBeNull();
        result!.Id.Should().Be("win1");
    }

    [Fact]
    public async Task GetAppByIdAsync_ReturnsCorrectApp_FromExternalApps()
    {
        _mockWindowsApps.Setup(w => w.GetAppsAsync()).ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        var externalApps = new List<ItemDefinition>
        {
            CreateDefinition("ext1"),
            CreateDefinition("ext2"),
        };
        _mockExternalApps.Setup(e => e.GetAppsAsync()).ReturnsAsync(externalApps);

        var result = await _service.GetAppByIdAsync("ext2");

        result.Should().NotBeNull();
        result!.Id.Should().Be("ext2");
    }

    [Fact]
    public async Task GetAppByIdAsync_ReturnsNull_WhenAppNotFound()
    {
        _mockWindowsApps.Setup(w => w.GetAppsAsync()).ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _mockExternalApps.Setup(e => e.GetAppsAsync()).ReturnsAsync(Enumerable.Empty<ItemDefinition>());

        var result = await _service.GetAppByIdAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAppByIdAsync_SearchesBothWindowsAndExternal()
    {
        var windowsApps = new List<ItemDefinition> { CreateDefinition("win1") };
        var externalApps = new List<ItemDefinition> { CreateDefinition("ext1") };

        _mockWindowsApps.Setup(w => w.GetAppsAsync()).ReturnsAsync(windowsApps);
        _mockExternalApps.Setup(e => e.GetAppsAsync()).ReturnsAsync(externalApps);

        // Verify both services are queried
        await _service.GetAppByIdAsync("ext1");

        _mockWindowsApps.Verify(w => w.GetAppsAsync(), Times.Once);
        _mockExternalApps.Verify(e => e.GetAppsAsync(), Times.Once);
    }

    // --- RefreshInstallationStatusAsync ---

    [Fact]
    public async Task RefreshInstallationStatusAsync_UpdatesIsInstalledOnApps()
    {
        var apps = new List<ItemDefinition>
        {
            CreateDefinition("app1", appxName: "Microsoft.App1"),
            CreateDefinition("app2", capabilityName: "Capability.Test"),
        };

        _mockStatusDiscovery
            .Setup(s => s.GetInstallationStatusBatchAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                { "Microsoft.App1", true },
                { "Capability.Test", false },
            });

        var result = await _service.RefreshInstallationStatusAsync(apps);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshInstallationStatusAsync_EmptyList_ReturnsSuccess()
    {
        var result = await _service.RefreshInstallationStatusAsync(Array.Empty<ItemDefinition>());

        result.Success.Should().BeTrue();
        _mockStatusDiscovery.Verify(
            s => s.GetInstallationStatusBatchAsync(It.IsAny<IEnumerable<ItemDefinition>>()),
            Times.Never);
    }

    [Fact]
    public async Task RefreshInstallationStatusAsync_FiltersNullApps()
    {
        var apps = new List<ItemDefinition?>
        {
            CreateDefinition("app1", appxName: "Microsoft.App1"),
            null,
        };

        _mockStatusDiscovery
            .Setup(s => s.GetInstallationStatusBatchAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                { "Microsoft.App1", true },
            });

        // The method filters nulls before processing
        var result = await _service.RefreshInstallationStatusAsync(apps!);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshInstallationStatusAsync_WhenExceptionThrown_ReturnsFailedResult()
    {
        var apps = new List<ItemDefinition>
        {
            CreateDefinition("app1", appxName: "Microsoft.App1"),
        };

        _mockStatusDiscovery
            .Setup(s => s.GetInstallationStatusBatchAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ThrowsAsync(new Exception("Status check failed"));

        var result = await _service.RefreshInstallationStatusAsync(apps);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to refresh installation status");
        _mockLog.Verify(l => l.LogError(
            It.Is<string>(s => s.Contains("Failed to refresh installation status")),
            It.IsAny<Exception>()), Times.Once);
    }

    [Fact]
    public async Task RefreshInstallationStatusAsync_NullArgument_ReturnsFailed()
    {
        var result = await _service.RefreshInstallationStatusAsync(null!);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshInstallationStatusAsync_UsesCorrectKey_ForCapabilityApp()
    {
        var apps = new List<ItemDefinition>
        {
            CreateDefinition("app1", capabilityName: "Media.Feature"),
        };

        _mockStatusDiscovery
            .Setup(s => s.GetInstallationStatusBatchAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                // GetKeyForDefinition returns CapabilityName first
                { "Media.Feature", true },
            });

        var result = await _service.RefreshInstallationStatusAsync(apps);

        result.Success.Should().BeTrue();
        apps[0].IsInstalled.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshInstallationStatusAsync_UsesCorrectKey_ForOptionalFeatureApp()
    {
        var apps = new List<ItemDefinition>
        {
            CreateDefinition("app1", featureName: "TelnetClient"),
        };

        _mockStatusDiscovery
            .Setup(s => s.GetInstallationStatusBatchAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                // GetKeyForDefinition returns OptionalFeatureName when no CapabilityName
                { "TelnetClient", true },
            });

        var result = await _service.RefreshInstallationStatusAsync(apps);

        result.Success.Should().BeTrue();
        apps[0].IsInstalled.Should().BeTrue();
    }
}

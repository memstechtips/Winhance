using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.Infrastructure.Tests.Services;

public class PolicyCleanupServiceTests
{
    private readonly Mock<ICatalogSettingsRegistry> _mockRegistry = new();
    private readonly Mock<IWindowsRegistryService> _mockRegistryService = new();
    private readonly Mock<ILogService> _mockLogService = new();

    private PolicyCleanupService CreateService() =>
        new(_mockRegistry.Object, _mockRegistryService.Object, _mockLogService.Object);

    private static Setting SettingWithGroupPolicyPaths(string id, params string[] keyPaths) =>
        new()
        {
            Id = id,
            Display = new Display { Name = TestKeys.Of(id), Description = TestKeys.Of("Test") },
            Targets = keyPaths.Select((kp, i) => (Target)new RegTarget(
                $"k{i}", new[] { kp }, "TestValue", Microsoft.Win32.RegistryValueKind.DWord)
                { IsGroupPolicy = true }).ToArray()
        };

    private static Setting SettingWithPaths(string id, params string[] keyPaths) =>
        new()
        {
            Id = id,
            Display = new Display { Name = TestKeys.Of(id), Description = TestKeys.Of("Test") },
            Targets = keyPaths.Select((kp, i) => (Target)new RegTarget(
                $"k{i}", new[] { kp }, "TestValue", Microsoft.Win32.RegistryValueKind.DWord)).ToArray()
        };

    [Fact]
    public void CollectPolicyKeyPaths_FindsGroupPolicyPaths()
    {
        var settings = new Dictionary<string, IReadOnlyList<Setting>>
        {
            ["Privacy"] = new[]
            {
                SettingWithGroupPolicyPaths("s1",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                    @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DataCollection"),
                SettingWithPaths("s2",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer")
            }
        };

        _mockRegistry.Setup(r => r.GetAll(new CatalogScope(true, false))).Returns(settings);

        var service = CreateService();
        var paths = service.CollectPolicyKeyPaths();

        paths.Should().Contain(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection");
        paths.Should().Contain(@"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DataCollection");
        paths.Should().NotContain(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer");
    }

    [Fact]
    public void CollectPolicyKeyPaths_IgnoresNonGroupPolicySettings()
    {
        // A setting with a Policies path but IsGroupPolicy = false should be ignored
        var settings = new Dictionary<string, IReadOnlyList<Setting>>
        {
            ["Privacy"] = new[]
            {
                SettingWithPaths("s1",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System")
            }
        };

        _mockRegistry.Setup(r => r.GetAll(new CatalogScope(true, false))).Returns(settings);

        var service = CreateService();
        var paths = service.CollectPolicyKeyPaths();

        paths.Should().BeEmpty();
    }

    [Fact]
    public void CollectPolicyKeyPaths_DeduplicatesParentAndChildPaths()
    {
        var settings = new Dictionary<string, IReadOnlyList<Setting>>
        {
            ["Update"] = new[]
            {
                SettingWithGroupPolicyPaths("s1",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate"),
                SettingWithGroupPolicyPaths("s2",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU")
            }
        };

        _mockRegistry.Setup(r => r.GetAll(new CatalogScope(true, false))).Returns(settings);

        var service = CreateService();
        var paths = service.CollectPolicyKeyPaths();

        paths.Should().Contain(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate");
        paths.Should().NotContain(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU");
    }

    private static readonly string[] TelemetryMirrorPaths =
    [
        @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
        @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
    ];

    // Windows writes CurrentVersion\Policies\DataCollection itself during setup, so deleting it is not a reset.
    [Fact]
    public void CollectPolicyKeyPaths_SkipsAGroupPolicyMirrorsWindowsOwnPaths()
    {
        var mirror = new Setting
        {
            Id = "s1",
            Display = new Display { Name = TestKeys.Of("s1"), Description = TestKeys.Of("Test") },
            Targets = new Target[]
            {
                new RegTarget("AllowTelemetry", TelemetryMirrorPaths, "AllowTelemetry", Microsoft.Win32.RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
        };
        var settings = new Dictionary<string, IReadOnlyList<Setting>>
        {
            ["Privacy"] = new[]
            {
                mirror,
                SettingWithGroupPolicyPaths("s2",
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\InkingAndTypingPersonalization"),
            }
        };

        _mockRegistry.Setup(r => r.GetAll(new CatalogScope(true, false))).Returns(settings);

        var paths = CreateService().CollectPolicyKeyPaths();

        paths.Should().BeEquivalentTo(TelemetryMirrorPaths[1..]);
    }

    [Fact]
    public void CleanupPolicyKeys_DeletesExistingPolicyKeys()
    {
        var settings = new Dictionary<string, IReadOnlyList<Setting>>
        {
            ["Privacy"] = new[]
            {
                SettingWithGroupPolicyPaths("s1",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection")
            }
        };

        _mockRegistry.Setup(r => r.GetAll(new CatalogScope(true, false))).Returns(settings);
        _mockRegistryService
            .Setup(r => r.KeyExists(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection"))
            .Returns(true);
        _mockRegistryService
            .Setup(r => r.DeleteKey(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection"))
            .Returns(true);

        var service = CreateService();
        var deletedCount = service.CleanupPolicyKeys();

        deletedCount.Should().Be(1);
        _mockRegistryService.Verify(
            r => r.DeleteKey(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection"),
            Times.Once);
    }

    [Fact]
    public void CleanupPolicyKeys_SkipsNonExistentKeys()
    {
        var settings = new Dictionary<string, IReadOnlyList<Setting>>
        {
            ["Privacy"] = new[]
            {
                SettingWithGroupPolicyPaths("s1",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection")
            }
        };

        _mockRegistry.Setup(r => r.GetAll(new CatalogScope(true, false))).Returns(settings);
        _mockRegistryService
            .Setup(r => r.KeyExists(It.IsAny<string>()))
            .Returns(false);

        var service = CreateService();
        var deletedCount = service.CleanupPolicyKeys();

        deletedCount.Should().Be(0);
        _mockRegistryService.Verify(r => r.DeleteKey(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void CleanupPolicyKeys_ContinuesOnDeleteFailure()
    {
        var settings = new Dictionary<string, IReadOnlyList<Setting>>
        {
            ["Privacy"] = new[]
            {
                SettingWithGroupPolicyPaths("s1",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection"),
                SettingWithGroupPolicyPaths("s2",
                    @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo")
            }
        };

        _mockRegistry.Setup(r => r.GetAll(new CatalogScope(true, false))).Returns(settings);
        _mockRegistryService.Setup(r => r.KeyExists(It.IsAny<string>())).Returns(true);

        _mockRegistryService
            .Setup(r => r.DeleteKey(@"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo"))
            .Returns(false);
        _mockRegistryService
            .Setup(r => r.DeleteKey(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection"))
            .Returns(true);

        var service = CreateService();
        var deletedCount = service.CleanupPolicyKeys();

        deletedCount.Should().Be(1);
        _mockRegistryService.Verify(r => r.DeleteKey(It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public void CollectPolicyKeyPaths_WithNoSettings_ReturnsEmpty()
    {
        _mockRegistry.Setup(r => r.GetAll(new CatalogScope(true, false)))
            .Returns(new Dictionary<string, IReadOnlyList<Setting>>());

        var service = CreateService();
        var paths = service.CollectPolicyKeyPaths();

        paths.Should().BeEmpty();
    }
}

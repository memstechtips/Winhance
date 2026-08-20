using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

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
            Display = new Display { Name = id, Description = "Test" },
            Targets = keyPaths.Select((kp, i) => (Target)new RegTarget(
                $"k{i}", new[] { kp }, "TestValue", Microsoft.Win32.RegistryValueKind.DWord)
                { IsGroupPolicy = true }).ToArray()
        };

    private static Setting SettingWithPaths(string id, params string[] keyPaths) =>
        new()
        {
            Id = id,
            Display = new Display { Name = id, Description = "Test" },
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

    [Fact]
    public void CollectPolicyKeyPaths_FindsCurrentVersionPoliciesPaths()
    {
        var settings = new Dictionary<string, IReadOnlyList<Setting>>
        {
            ["Privacy"] = new[]
            {
                SettingWithGroupPolicyPaths("s1",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection")
            }
        };

        _mockRegistry.Setup(r => r.GetAll(new CatalogScope(true, false))).Returns(settings);

        var service = CreateService();
        var paths = service.CollectPolicyKeyPaths();

        paths.Should().Contain(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection");
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

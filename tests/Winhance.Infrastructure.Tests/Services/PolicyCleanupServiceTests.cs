using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class PolicyCleanupServiceTests
{
    private readonly Mock<ICompatibleSettingsRegistry> _mockRegistry = new();
    private readonly Mock<IWindowsRegistryService> _mockRegistryService = new();
    private readonly Mock<ILogService> _mockLogService = new();

    private PolicyCleanupService CreateService() =>
        new(_mockRegistry.Object, _mockRegistryService.Object, _mockLogService.Object);

    private static SettingDefinition CreateSettingWithGroupPolicyPaths(string id, params string[] keyPaths)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = id,
            Description = "Test",
            RegistrySettings = keyPaths.Select(kp => new RegistrySetting
            {
                KeyPath = kp,
                ValueName = "TestValue",
                ValueType = Microsoft.Win32.RegistryValueKind.DWord,
                IsGroupPolicy = true,
            }).ToArray()
        };
    }

    private static SettingDefinition CreateSettingWithPaths(string id, params string[] keyPaths)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = id,
            Description = "Test",
            RegistrySettings = keyPaths.Select(kp => new RegistrySetting
            {
                KeyPath = kp,
                ValueName = "TestValue",
                ValueType = Microsoft.Win32.RegistryValueKind.DWord,
            }).ToArray()
        };
    }

    [Fact]
    public void CollectPolicyKeyPaths_FindsGroupPolicyPaths()
    {
        var settings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            ["Privacy"] = new[]
            {
                CreateSettingWithGroupPolicyPaths("s1",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                    @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DataCollection"),
                CreateSettingWithPaths("s2",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer")
            }
        };

        _mockRegistry.Setup(r => r.GetAllBypassedSettings()).Returns(settings);

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
        var settings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            ["Privacy"] = new[]
            {
                CreateSettingWithPaths("s1",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System")
            }
        };

        _mockRegistry.Setup(r => r.GetAllBypassedSettings()).Returns(settings);

        var service = CreateService();
        var paths = service.CollectPolicyKeyPaths();

        paths.Should().BeEmpty();
    }

    [Fact]
    public void CollectPolicyKeyPaths_DeduplicatesParentAndChildPaths()
    {
        var settings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            ["Update"] = new[]
            {
                CreateSettingWithGroupPolicyPaths("s1",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate"),
                CreateSettingWithGroupPolicyPaths("s2",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU")
            }
        };

        _mockRegistry.Setup(r => r.GetAllBypassedSettings()).Returns(settings);

        var service = CreateService();
        var paths = service.CollectPolicyKeyPaths();

        // Parent path should be kept, child path should be deduplicated
        paths.Should().Contain(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate");
        paths.Should().NotContain(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU");
    }

    [Fact]
    public void CollectPolicyKeyPaths_FindsCurrentVersionPoliciesPaths()
    {
        var settings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            ["Privacy"] = new[]
            {
                CreateSettingWithGroupPolicyPaths("s1",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection")
            }
        };

        _mockRegistry.Setup(r => r.GetAllBypassedSettings()).Returns(settings);

        var service = CreateService();
        var paths = service.CollectPolicyKeyPaths();

        paths.Should().Contain(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection");
    }

    [Fact]
    public void CleanupPolicyKeys_DeletesExistingPolicyKeys()
    {
        var settings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            ["Privacy"] = new[]
            {
                CreateSettingWithGroupPolicyPaths("s1",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection")
            }
        };

        _mockRegistry.Setup(r => r.GetAllBypassedSettings()).Returns(settings);
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
        var settings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            ["Privacy"] = new[]
            {
                CreateSettingWithGroupPolicyPaths("s1",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection")
            }
        };

        _mockRegistry.Setup(r => r.GetAllBypassedSettings()).Returns(settings);
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
        var settings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            ["Privacy"] = new[]
            {
                CreateSettingWithGroupPolicyPaths("s1",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection"),
                CreateSettingWithGroupPolicyPaths("s2",
                    @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo")
            }
        };

        _mockRegistry.Setup(r => r.GetAllBypassedSettings()).Returns(settings);
        _mockRegistryService.Setup(r => r.KeyExists(It.IsAny<string>())).Returns(true);

        // First key fails, second succeeds
        _mockRegistryService
            .Setup(r => r.DeleteKey(@"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo"))
            .Returns(false);
        _mockRegistryService
            .Setup(r => r.DeleteKey(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection"))
            .Returns(true);

        var service = CreateService();
        var deletedCount = service.CleanupPolicyKeys();

        // Only one succeeded
        deletedCount.Should().Be(1);
        // Both were attempted
        _mockRegistryService.Verify(r => r.DeleteKey(It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public void CollectPolicyKeyPaths_WithNoSettings_ReturnsEmpty()
    {
        _mockRegistry.Setup(r => r.GetAllBypassedSettings())
            .Returns(new Dictionary<string, IEnumerable<SettingDefinition>>());

        var service = CreateService();
        var paths = service.CollectPolicyKeyPaths();

        paths.Should().BeEmpty();
    }
}

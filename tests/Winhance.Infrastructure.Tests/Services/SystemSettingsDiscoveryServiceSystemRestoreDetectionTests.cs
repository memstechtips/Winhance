using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SystemSettingsDiscoveryServiceSystemRestoreDetectionTests
{
    private readonly Mock<IWindowsRegistryService> _registry = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IPowerSettingsQueryService> _powerQuery = new();
    private readonly Mock<ISpecialDiscoveryRegistry> _discoveryRegistry = new();
    private readonly Mock<IScheduledTaskService> _scheduledTask = new();
    private readonly Mock<ISystemRestoreService> _systemRestore = new();

    private SystemSettingsDiscoveryService NewService()
    {
        _discoveryRegistry.Setup(r => r.All).Returns(Array.Empty<ISpecialSettingHandler>());
        return new SystemSettingsDiscoveryService(
            _registry.Object,
            _log.Object,
            _powerQuery.Object,
            _discoveryRegistry.Object,
            _scheduledTask.Object,
            _systemRestore.Object);
    }

    private static SettingDefinition Setting(string id) => new()
    {
        Id = id,
        Name = id,
        Description = id,
        DetectionType = DetectionType.SystemRestore,
    };

    [Fact]
    public async Task GetSettingStates_UsesSystemRestoreService_WhenDetectionTypeIsSystemRestore()
    {
        _systemRestore.Setup(s => s.IsEnabledForC()).Returns(true);

        var service = NewService();
        var states = await service.GetSettingStatesAsync(new[] { Setting("sr") });

        states["sr"].Success.Should().BeTrue();
        states["sr"].IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetSettingStates_ReportsDisabled_WhenServiceReturnsFalse()
    {
        _systemRestore.Setup(s => s.IsEnabledForC()).Returns(false);

        var service = NewService();
        var states = await service.GetSettingStatesAsync(new[] { Setting("sr") });

        states["sr"].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetSettingStates_CallsSystemRestoreService_Once_PerBatch()
    {
        _systemRestore.Setup(s => s.IsEnabledForC()).Returns(true);

        var service = NewService();
        var settings = new[]
        {
            Setting("a"),
        };

        await service.GetSettingStatesAsync(settings);

        _systemRestore.Verify(s => s.IsEnabledForC(), Times.Once);
    }

    [Fact]
    public async Task GetSettingStates_SkipsSystemRestoreService_WhenNoSuchSettings()
    {
        var service = NewService();

        var regSetting = new SettingDefinition
        {
            Id = "reg",
            Name = "reg",
            Description = "reg",
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\Foo",
                    ValueName = "Bar",
                    ValueType = RegistryValueKind.DWord,
                    RecommendedValue = 1,
                    DefaultValue = 0,
                    EnabledValue = new object?[] { 1 },
                }
            }
        };
        _registry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>());

        await service.GetSettingStatesAsync(new[] { regSetting });

        _systemRestore.Verify(s => s.IsEnabledForC(), Times.Never);
    }

    [Fact]
    public async Task GetSettingStates_SystemRestore_OverridesRegistrySettings_WhenDetectionTypeIsSystemRestore()
    {
        var setting = new SettingDefinition
        {
            Id = "hybrid",
            Name = "hybrid",
            Description = "hybrid",
            DetectionType = DetectionType.SystemRestore,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\Foo",
                    ValueName = "Bar",
                    ValueType = RegistryValueKind.DWord,
                    RecommendedValue = 1,
                    DefaultValue = 0,
                    EnabledValue = new object?[] { 1 },
                }
            },
        };

        _registry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>());
        _systemRestore.Setup(s => s.IsEnabledForC()).Returns(true);

        var service = NewService();
        var states = await service.GetSettingStatesAsync(new[] { setting });

        states["hybrid"].IsEnabled.Should().BeTrue();
    }
}

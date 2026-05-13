using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SystemSettingsDiscoveryServicePsDetectionTests
{
    private readonly Mock<IWindowsRegistryService> _registry = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IPowerSettingsQueryService> _powerQuery = new();
    private readonly Mock<IDomainServiceRouter> _domainRouter = new();
    private readonly Mock<IScheduledTaskService> _scheduledTask = new();
    private readonly Mock<IPowerShellDetectionService> _psDetection = new();

    private SystemSettingsDiscoveryService NewService() => new(
        _registry.Object,
        _log.Object,
        _powerQuery.Object,
        _domainRouter.Object,
        _scheduledTask.Object,
        _psDetection.Object);

    private static SettingDefinition Setting(string id, string detectionScript) => new()
    {
        Id = id,
        Name = id,
        Description = id,
        DetectionType = DetectionType.PowerShellScript,
        PowerShellScripts = new[]
        {
            new PowerShellScriptSetting
            {
                DetectionScript = detectionScript,
                EnabledScript = "Enable",
                DisabledScript = "Disable",
            }
        }
    };

    [Fact]
    public async Task GetSettingStates_UsesPsDetection_WhenDetectionTypeIsPowerShellScript()
    {
        _psDetection
            .Setup(p => p.DetectAsync(It.IsAny<IEnumerable<SettingDefinition>>(), default))
            .ReturnsAsync(new Dictionary<string, bool> { ["sr"] = true });

        var service = NewService();
        var states = await service.GetSettingStatesAsync(new[] { Setting("sr", "$true") });

        states["sr"].Success.Should().BeTrue();
        states["sr"].IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetSettingStates_ReportsDisabled_WhenDetectionServiceReturnsFalse()
    {
        _psDetection
            .Setup(p => p.DetectAsync(It.IsAny<IEnumerable<SettingDefinition>>(), default))
            .ReturnsAsync(new Dictionary<string, bool> { ["sr"] = false });

        var service = NewService();
        var states = await service.GetSettingStatesAsync(new[] { Setting("sr", "$false") });

        states["sr"].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetSettingStates_BatchesAllPsDetectionInOneCall()
    {
        _psDetection
            .Setup(p => p.DetectAsync(It.IsAny<IEnumerable<SettingDefinition>>(), default))
            .ReturnsAsync(new Dictionary<string, bool>
            {
                ["a"] = true,
                ["b"] = false,
                ["c"] = true,
            });

        var service = NewService();
        var settings = new[]
        {
            Setting("a", "$true"),
            Setting("b", "$false"),
            Setting("c", "$true"),
        };

        await service.GetSettingStatesAsync(settings);

        _psDetection.Verify(
            p => p.DetectAsync(It.Is<IEnumerable<SettingDefinition>>(s => s.Count() == 3), default),
            Times.Once);
    }

    [Fact]
    public async Task GetSettingStates_SkipsPsDetection_WhenNoPsDetectionSettings()
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
                    EnabledValue = new object?[] { 1 },
                }
            }
        };
        _registry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>());

        await service.GetSettingStatesAsync(new[] { regSetting });

        _psDetection.Verify(p => p.DetectAsync(It.IsAny<IEnumerable<SettingDefinition>>(), default), Times.Never);
    }

    [Fact]
    public async Task GetSettingStates_PsDetection_OverridesRegistrySettings_WhenDetectionTypeIsPowerShellScript()
    {
        var setting = new SettingDefinition
        {
            Id = "hybrid",
            Name = "hybrid",
            Description = "hybrid",
            DetectionType = DetectionType.PowerShellScript,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\Foo",
                    ValueName = "Bar",
                    ValueType = RegistryValueKind.DWord,
                    EnabledValue = new object?[] { 1 },
                }
            },
            PowerShellScripts = new[]
            {
                new PowerShellScriptSetting { DetectionScript = "$true" }
            }
        };

        _registry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>());
        _psDetection
            .Setup(p => p.DetectAsync(It.IsAny<IEnumerable<SettingDefinition>>(), default))
            .ReturnsAsync(new Dictionary<string, bool> { ["hybrid"] = true });

        var service = NewService();
        var states = await service.GetSettingStatesAsync(new[] { setting });

        states["hybrid"].IsEnabled.Should().BeTrue();
    }
}

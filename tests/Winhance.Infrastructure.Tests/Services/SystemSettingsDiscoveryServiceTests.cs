using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SystemSettingsDiscoveryServiceTests
{
    private readonly Mock<IWindowsRegistryService> _mockRegistry = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IPowerSettingsQueryService> _mockPowerQuery = new();
    private readonly Mock<IDomainServiceRouter> _mockDomainRouter = new();
    private readonly Mock<IScheduledTaskService> _mockScheduledTask = new();
    private readonly SystemSettingsDiscoveryService _service;

    public SystemSettingsDiscoveryServiceTests()
    {
        _service = new SystemSettingsDiscoveryService(
            _mockRegistry.Object,
            _mockLog.Object,
            _mockPowerQuery.Object,
            _mockDomainRouter.Object,
            _mockScheduledTask.Object);
    }

    private static SettingDefinition CreateRegistrySetting(string id, string keyPath, string valueName, object? enabledValue = null)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = $"Setting {id}",
            Description = $"Description for {id}",
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = keyPath,
                    ValueName = valueName,
                    ValueType = RegistryValueKind.DWord,
                    EnabledValue = enabledValue ?? 1,
                },
            },
        };
    }

    private static SettingDefinition CreateScheduledTaskSetting(string id, string taskPath)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = $"Setting {id}",
            Description = $"Description for {id}",
            ScheduledTaskSettings = new[]
            {
                new ScheduledTaskSetting
                {
                    Id = $"{id}-task",
                    TaskPath = taskPath,
                },
            },
        };
    }

    private static SettingDefinition CreatePowerCfgSetting(
        string id,
        string settingGuid,
        PowerModeSupport mode = PowerModeSupport.Both)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = $"Setting {id}",
            Description = $"Description for {id}",
            PowerCfgSettings = new[]
            {
                new PowerCfgSetting
                {
                    SettingGuid = settingGuid,
                    SubgroupGuid = "sub-guid",
                    PowerModeSupport = mode,
                },
            },
        };
    }

    // --- GetRawSettingsValuesAsync ---

    [Fact]
    public async Task GetRawSettingsValuesAsync_NullSettings_ReturnsEmptyDictionary()
    {
        var result = await _service.GetRawSettingsValuesAsync(null!);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRawSettingsValuesAsync_EmptySettings_ReturnsEmptyDictionary()
    {
        var result = await _service.GetRawSettingsValuesAsync(Array.Empty<SettingDefinition>());

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRawSettingsValuesAsync_RegistrySettings_ReadsFromBatchValues()
    {
        var setting = CreateRegistrySetting("test-reg", @"HKEY_LOCAL_MACHINE\SOFTWARE\Test", "TestValue");

        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>
            {
                { @"HKEY_LOCAL_MACHINE\SOFTWARE\Test\TestValue", 1 },
            });

        // Need to set up the domain service router for Selection input type settings
        var mockDomain = new Mock<IDomainService>();
        mockDomain.Setup(d => d.DomainName).Returns("TestDomain");

        var result = await _service.GetRawSettingsValuesAsync(new[] { setting });

        result.Should().ContainKey("test-reg");
        result["test-reg"].Should().ContainKey("TestValue");
        result["test-reg"]["TestValue"].Should().Be(1);
    }

    [Fact]
    public async Task GetRawSettingsValuesAsync_RegistrySettings_KeyExistsCheck_WhenValueNameIsNull()
    {
        var setting = new SettingDefinition
        {
            Id = "test-key-exists",
            Name = "Key Exists Check",
            Description = "Tests key existence",
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\TestKey",
                    ValueName = null,
                    ValueType = RegistryValueKind.DWord,
                },
            },
        };

        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>
            {
                { @"HKEY_LOCAL_MACHINE\SOFTWARE\TestKey\__KEY_EXISTS__", true },
            });

        var result = await _service.GetRawSettingsValuesAsync(new[] { setting });

        result.Should().ContainKey("test-key-exists");
        result["test-key-exists"].Should().ContainKey("KeyExists");
    }

    [Fact]
    public async Task GetRawSettingsValuesAsync_RegistrySettings_HandlesBitMask()
    {
        var setting = new SettingDefinition
        {
            Id = "test-bitmask",
            Name = "BitMask Setting",
            Description = "Tests bitmask extraction",
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Test",
                    ValueName = "BinaryValue",
                    ValueType = RegistryValueKind.Binary,
                    BinaryByteIndex = 0,
                    BitMask = 0x04,
                },
            },
        };

        // Byte 0 = 0x05 (binary 00000101), BitMask 0x04 -> bit IS set -> true
        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>
            {
                { @"HKEY_LOCAL_MACHINE\SOFTWARE\Test\BinaryValue", new byte[] { 0x05 } },
            });

        var result = await _service.GetRawSettingsValuesAsync(new[] { setting });

        result["test-bitmask"]["BinaryValue"].Should().Be(true);
    }

    [Fact]
    public async Task GetRawSettingsValuesAsync_RegistrySettings_HandlesModifyByteOnly()
    {
        var setting = new SettingDefinition
        {
            Id = "test-modify-byte",
            Name = "Modify Byte Setting",
            Description = "Tests byte extraction",
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Test",
                    ValueName = "BinaryValue",
                    ValueType = RegistryValueKind.Binary,
                    BinaryByteIndex = 2,
                    ModifyByteOnly = true,
                },
            },
        };

        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>
            {
                { @"HKEY_LOCAL_MACHINE\SOFTWARE\Test\BinaryValue", new byte[] { 0x00, 0x01, 0xAB } },
            });

        var result = await _service.GetRawSettingsValuesAsync(new[] { setting });

        result["test-modify-byte"]["BinaryValue"].Should().Be((byte)0xAB);
    }

    [Fact]
    public async Task GetRawSettingsValuesAsync_SinglePowerCfgSetting_QueriesIndividually()
    {
        var setting = CreatePowerCfgSetting("test-power", "setting-guid-1");

        _mockPowerQuery
            .Setup(p => p.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((42, 30));

        var result = await _service.GetRawSettingsValuesAsync(new[] { setting });

        result.Should().ContainKey("test-power");
        result["test-power"]["PowerCfgValue"].Should().Be(42);
        result["test-power"]["ACValue"].Should().Be(42);
        result["test-power"]["DCValue"].Should().Be(30);
    }

    [Fact]
    public async Task GetRawSettingsValuesAsync_SingleSeparatePowerCfgSetting_ReturnsACandDC()
    {
        var setting = CreatePowerCfgSetting("test-power-sep", "setting-guid-sep",
            PowerModeSupport.Separate);

        _mockPowerQuery
            .Setup(p => p.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((100, 50));

        var result = await _service.GetRawSettingsValuesAsync(new[] { setting });

        result["test-power-sep"]["ACValue"].Should().Be(100);
        result["test-power-sep"]["DCValue"].Should().Be(50);
        result["test-power-sep"]["PowerCfgValue"].Should().Be(100);
    }

    [Fact]
    public async Task GetRawSettingsValuesAsync_MultiplePowerCfgSettings_UsesBulkQuery()
    {
        var settings = new[]
        {
            CreatePowerCfgSetting("power1", "guid-1"),
            CreatePowerCfgSetting("power2", "guid-2"),
        };

        _mockPowerQuery
            .Setup(p => p.GetAllPowerSettingsACDCAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>
            {
                { "guid-1", (60, 30) },
                { "guid-2", (90, 45) },
            });

        var result = await _service.GetRawSettingsValuesAsync(settings);

        result.Should().ContainKey("power1");
        result["power1"]["PowerCfgValue"].Should().Be(60);
        result.Should().ContainKey("power2");
        result["power2"]["PowerCfgValue"].Should().Be(90);
    }

    [Fact]
    public async Task GetRawSettingsValuesAsync_ScheduledTaskSetting_QueriesTaskState()
    {
        var setting = CreateScheduledTaskSetting("test-task", @"\Microsoft\Windows\Test\TaskName");

        _mockScheduledTask
            .Setup(s => s.IsTaskEnabledAsync(@"\Microsoft\Windows\Test\TaskName"))
            .ReturnsAsync(true);

        var result = await _service.GetRawSettingsValuesAsync(new[] { setting });

        result.Should().ContainKey("test-task");
        result["test-task"]["ScheduledTaskEnabled"].Should().Be(true);
        result["test-task"]["ScheduledTaskExists"].Should().Be(true);
    }

    [Fact]
    public async Task GetRawSettingsValuesAsync_ScheduledTaskNotFound_ReturnsNullEnabled()
    {
        var setting = CreateScheduledTaskSetting("test-task-missing", @"\Missing\Task");

        _mockScheduledTask
            .Setup(s => s.IsTaskEnabledAsync(@"\Missing\Task"))
            .ReturnsAsync((bool?)null);

        var result = await _service.GetRawSettingsValuesAsync(new[] { setting });

        result.Should().ContainKey("test-task-missing");
        result["test-task-missing"]["ScheduledTaskEnabled"].Should().BeNull();
        // null != null is false for "is false" check, so ScheduledTaskExists should be false
        result["test-task-missing"]["ScheduledTaskExists"].Should().Be(false);
    }

    [Fact]
    public async Task GetRawSettingsValuesAsync_ScheduledTaskThrowsException_ReturnsEmptyValues()
    {
        var setting = CreateScheduledTaskSetting("test-task-error", @"\Error\Task");

        _mockScheduledTask
            .Setup(s => s.IsTaskEnabledAsync(@"\Error\Task"))
            .ThrowsAsync(new Exception("COM error"));

        var result = await _service.GetRawSettingsValuesAsync(new[] { setting });

        result.Should().ContainKey("test-task-error");
        result["test-task-error"].Should().BeEmpty();
    }

    // --- GetSettingStatesAsync ---

    [Fact]
    public async Task GetSettingStatesAsync_RegistrySetting_ReturnsCorrectState()
    {
        var setting = CreateRegistrySetting("test-reg", @"HKEY_LOCAL_MACHINE\SOFTWARE\Test", "TestValue", enabledValue: 1);

        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>
            {
                { @"HKEY_LOCAL_MACHINE\SOFTWARE\Test\TestValue", 1 },
            });
        _mockRegistry.Setup(r => r.IsSettingApplied(It.IsAny<RegistrySetting>()))
            .Returns(true);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result.Should().ContainKey("test-reg");
        result["test-reg"].Success.Should().BeTrue();
        result["test-reg"].IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetSettingStatesAsync_RegistrySettingNotApplied_ReturnsDisabled()
    {
        var setting = CreateRegistrySetting("test-reg", @"HKEY_LOCAL_MACHINE\SOFTWARE\Test", "TestValue");

        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>
            {
                { @"HKEY_LOCAL_MACHINE\SOFTWARE\Test\TestValue", 0 },
            });
        _mockRegistry.Setup(r => r.IsSettingApplied(It.IsAny<RegistrySetting>()))
            .Returns(false);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-reg"].Success.Should().BeTrue();
        result["test-reg"].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetSettingStatesAsync_PowerCfgSetting_ReturnsEnabledWhenValueNonZero()
    {
        var setting = CreatePowerCfgSetting("test-power", "guid-power");

        _mockPowerQuery
            .Setup(p => p.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((1, 1));

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-power"].Success.Should().BeTrue();
        result["test-power"].IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetSettingStatesAsync_PowerCfgSetting_ReturnsDisabledWhenValueZero()
    {
        var setting = CreatePowerCfgSetting("test-power-off", "guid-power-off");

        _mockPowerQuery
            .Setup(p => p.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((0, 0));

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-power-off"].Success.Should().BeTrue();
        result["test-power-off"].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetSettingStatesAsync_ScheduledTaskSetting_EnabledTask()
    {
        var setting = CreateScheduledTaskSetting("test-task", @"\Test\Task");

        _mockScheduledTask
            .Setup(s => s.IsTaskEnabledAsync(@"\Test\Task"))
            .ReturnsAsync(true);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-task"].Success.Should().BeTrue();
        result["test-task"].IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetSettingStatesAsync_ScheduledTaskDoesNotExist_MarksAsUnavailable()
    {
        var setting = CreateScheduledTaskSetting("test-task-missing", @"\Missing\Task");

        _mockScheduledTask
            .Setup(s => s.IsTaskEnabledAsync(@"\Missing\Task"))
            .ReturnsAsync((bool?)null);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-task-missing"].Success.Should().BeFalse();
        result["test-task-missing"].ErrorMessage.Should().Contain("Scheduled task does not exist");
    }

    [Fact]
    public async Task GetSettingStatesAsync_SettingWithNoRawValues_ReturnsDisabled()
    {
        // A setting with registry settings but no values found
        var setting = CreateRegistrySetting("test-no-value", @"HKEY_LOCAL_MACHINE\SOFTWARE\Missing", "MissingValue");

        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>());
        _mockRegistry.Setup(r => r.IsSettingApplied(It.IsAny<RegistrySetting>()))
            .Returns(false);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-no-value"].Success.Should().BeTrue();
        result["test-no-value"].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetSettingStatesAsync_ExceptionForIndividualSetting_ReturnsFailedResult()
    {
        // Create a setting that will cause an error during interpretation
        var setting = new SettingDefinition
        {
            Id = "test-error",
            Name = "Error Setting",
            Description = "Will cause error",
            InputType = InputType.Selection,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Test",
                    ValueName = "TestValue",
                    ValueType = RegistryValueKind.DWord,
                },
            },
            // Selection type with ValueMappings that will cause a cast error
            CustomProperties = new Dictionary<string, object>
            {
                { CustomPropertyKeys.ValueMappings, "not-a-dictionary" }, // Wrong type
            },
        };

        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>
            {
                { @"HKEY_LOCAL_MACHINE\SOFTWARE\Test\TestValue", 1 },
            });
        _mockRegistry.Setup(r => r.IsSettingApplied(It.IsAny<RegistrySetting>()))
            .Returns(true);

        // Set up domain router for Selection type
        var mockDomain = new Mock<IDomainService>();
        mockDomain.Setup(d => d.DomainName).Returns("TestDomain");
        _mockDomainRouter.Setup(r => r.GetDomainService("test-error")).Returns(mockDomain.Object);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-error"].Success.Should().BeFalse();
        result["test-error"].ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetSettingStatesAsync_NumericRangePowerCfg_ReturnsCurrentValue()
    {
        var setting = new SettingDefinition
        {
            Id = "test-numeric-power",
            Name = "Numeric Power Setting",
            Description = "Tests numeric range power cfg",
            InputType = InputType.NumericRange,
            PowerCfgSettings = new[]
            {
                new PowerCfgSetting
                {
                    SettingGuid = "numeric-guid",
                    SubgroupGuid = "sub-guid",
                },
            },
        };

        _mockPowerQuery
            .Setup(p => p.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((75, 50));

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-numeric-power"].Success.Should().BeTrue();
        result["test-numeric-power"].CurrentValue.Should().Be(75);
    }

    [Fact]
    public async Task GetSettingStatesAsync_NumericRangeScheduledTask_ReturnsCurrentValue()
    {
        var setting = new SettingDefinition
        {
            Id = "test-numeric-task",
            Name = "Numeric Task Setting",
            Description = "Tests numeric range scheduled task",
            InputType = InputType.NumericRange,
            ScheduledTaskSettings = new[]
            {
                new ScheduledTaskSetting
                {
                    Id = "task-1",
                    TaskPath = @"\Test\NumericTask",
                },
            },
        };

        _mockScheduledTask
            .Setup(s => s.IsTaskEnabledAsync(@"\Test\NumericTask"))
            .ReturnsAsync(true);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-numeric-task"].Success.Should().BeTrue();
        result["test-numeric-task"].CurrentValue.Should().Be(true);
    }

    [Fact]
    public async Task GetSettingStatesAsync_MultipleSettings_ReturnsAllStates()
    {
        var settings = new[]
        {
            CreateRegistrySetting("reg1", @"HKEY_LOCAL_MACHINE\SOFTWARE\Test1", "Val1"),
            CreateRegistrySetting("reg2", @"HKEY_LOCAL_MACHINE\SOFTWARE\Test2", "Val2"),
        };

        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>
            {
                { @"HKEY_LOCAL_MACHINE\SOFTWARE\Test1\Val1", 1 },
                { @"HKEY_LOCAL_MACHINE\SOFTWARE\Test2\Val2", 0 },
            });
        _mockRegistry.Setup(r => r.IsSettingApplied(It.IsAny<RegistrySetting>()))
            .Returns(false);

        var result = await _service.GetSettingStatesAsync(settings);

        result.Should().HaveCount(2);
        result.Should().ContainKey("reg1");
        result.Should().ContainKey("reg2");
    }
}

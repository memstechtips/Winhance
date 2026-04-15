using FluentAssertions;
using Microsoft.Win32;
using Moq;
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

    private static SettingDefinition CreateRegistrySetting(string id, string keyPath, string valueName, object?[]? enabledValue = null)
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
                    EnabledValue = enabledValue ?? [1],
                    RecommendedValue = null,
                    DefaultValue = null
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
                    RecommendedState = null,
                    DefaultState = null
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
                    RecommendedValueAC = null,
                    RecommendedValueDC = null,
                    DefaultValueAC = null,
                    DefaultValueDC = null
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
                    RecommendedValue = null,
                    DefaultValue = null
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
                    RecommendedValue = null,
                    DefaultValue = null
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
                    RecommendedValue = null,
                    DefaultValue = null
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
        var setting = CreateRegistrySetting("test-reg", @"HKEY_LOCAL_MACHINE\SOFTWARE\Test", "TestValue", enabledValue: [1]);

        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>
            {
                { @"HKEY_LOCAL_MACHINE\SOFTWARE\Test\TestValue", 1 },
            });
        _mockRegistry.Setup(r => r.IsRegistryValueInEnabledState(It.IsAny<RegistrySetting>(), 1, true))
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
        _mockRegistry.Setup(r => r.IsRegistryValueInEnabledState(It.IsAny<RegistrySetting>(), 0, true))
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

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-no-value"].Success.Should().BeTrue();
        result["test-no-value"].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetSettingStatesAsync_RegistrySettingWithBatchValues_InterpretedSuccessfully()
    {
        // DetermineIfSettingIsEnabled uses pre-fetched raw values (not individual registry reads).
        // When batch values contain a valid value, the setting should be interpreted as enabled.
        var setting = new SettingDefinition
        {
            Id = "test-batch",
            Name = "Batch Setting",
            Description = "Uses batch values",
            InputType = InputType.Toggle,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Test",
                    ValueName = "TestValue",
                    ValueType = RegistryValueKind.DWord,
                    RecommendedValue = null,
                    DefaultValue = null
                },
            },
        };

        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>
            {
                { @"HKEY_LOCAL_MACHINE\SOFTWARE\Test\TestValue", 1 },
            });
        _mockRegistry.Setup(r => r.IsRegistryValueInEnabledState(It.IsAny<RegistrySetting>(), 1, true))
            .Returns(true);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-batch"].Success.Should().BeTrue();
        result["test-batch"].IsEnabled.Should().BeTrue();
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
                    RecommendedValueAC = null,
                    RecommendedValueDC = null,
                    DefaultValueAC = null,
                    DefaultValueDC = null
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
                    RecommendedState = null,
                    DefaultState = null
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
        _mockRegistry.Setup(r => r.IsRegistryValueInEnabledState(It.IsAny<RegistrySetting>(), It.IsAny<object?>(), true))
            .Returns(false);

        var result = await _service.GetSettingStatesAsync(settings);

        result.Should().HaveCount(2);
        result.Should().ContainKey("reg1");
        result.Should().ContainKey("reg2");
    }

    [Fact]
    public async Task GetSettingStatesAsync_EnabledValueNull_DelegatesToRegistryService_Disabled()
    {
        // DetermineIfSettingIsEnabled delegates to IsRegistryValueInEnabledState.
        // When the registry service returns false, the setting should be OFF.
        var setting = new SettingDefinition
        {
            Id = "test-null-enabled",
            Name = "Null Enabled Setting",
            Description = "Tests EnabledValue=[null] with DisabledValue match",
            InputType = InputType.Toggle,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_CURRENT_USER\Software\Test",
                    ValueName = "OptOut",
                    ValueType = RegistryValueKind.DWord,
                    EnabledValue = [null],
                    DisabledValue = [1],
                    RecommendedValue = null,
                    DefaultValue = null
                },
            },
        };

        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>
            {
                { @"HKEY_CURRENT_USER\Software\Test\OptOut", 1 },
            });
        _mockRegistry.Setup(r => r.IsRegistryValueInEnabledState(It.IsAny<RegistrySetting>(), 1, true))
            .Returns(false);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-null-enabled"].Success.Should().BeTrue();
        result["test-null-enabled"].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetSettingStatesAsync_EnabledValueNull_DelegatesToRegistryService_Enabled()
    {
        // When the registry service returns true, the setting should be ON.
        var setting = new SettingDefinition
        {
            Id = "test-null-enabled-on",
            Name = "Null Enabled Setting On",
            Description = "Tests EnabledValue=[null] with non-matching DisabledValue",
            InputType = InputType.Toggle,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_CURRENT_USER\Software\Test",
                    ValueName = "SomeValue",
                    ValueType = RegistryValueKind.DWord,
                    EnabledValue = [null],
                    DisabledValue = [0],
                    RecommendedValue = null,
                    DefaultValue = null
                },
            },
        };

        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>
            {
                { @"HKEY_CURRENT_USER\Software\Test\SomeValue", 1 },
            });
        _mockRegistry.Setup(r => r.IsRegistryValueInEnabledState(It.IsAny<RegistrySetting>(), 1, true))
            .Returns(true);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-null-enabled-on"].Success.Should().BeTrue();
        result["test-null-enabled-on"].IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetSettingStatesAsync_EnabledValueNull_ValueNotExists_ReturnsEnabled()
    {
        // When EnabledValue is null and the registry value doesn't exist,
        // absence means enabled — the registry service should be called with valueExists=false.
        var setting = new SettingDefinition
        {
            Id = "test-null-no-value",
            Name = "Null Enabled No Value",
            Description = "Tests EnabledValue=[null] with missing registry value",
            InputType = InputType.Toggle,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_CURRENT_USER\Software\Test",
                    ValueName = "Missing",
                    ValueType = RegistryValueKind.DWord,
                    EnabledValue = [null],
                    DisabledValue = [1],
                    RecommendedValue = null,
                    DefaultValue = null
                },
            },
        };

        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>
            {
                { @"HKEY_CURRENT_USER\Software\Test\Missing", null },
            });
        _mockRegistry.Setup(r => r.IsRegistryValueInEnabledState(It.IsAny<RegistrySetting>(), null, false))
            .Returns(true);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-null-no-value"].Success.Should().BeTrue();
        result["test-null-no-value"].IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetSettingStatesAsync_MultipleRegistrySettings_AllMatchDisabledValue_ReturnsDisabled()
    {
        // When multiple registry settings all return false from IsRegistryValueInEnabledState,
        // the setting should be OFF.
        var setting = new SettingDefinition
        {
            Id = "test-multi-disabled",
            Name = "Multi Disabled Setting",
            Description = "Tests multiple registry settings all matching DisabledValue",
            InputType = InputType.Toggle,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_CURRENT_USER\Software\Test",
                    ValueName = "Value1",
                    ValueType = RegistryValueKind.DWord,
                    EnabledValue = [null],
                    DisabledValue = [0],
                    RecommendedValue = null,
                    DefaultValue = null
                },
                new RegistrySetting
                {
                    KeyPath = @"HKEY_CURRENT_USER\Software\Test",
                    ValueName = "Value2",
                    ValueType = RegistryValueKind.DWord,
                    EnabledValue = [null],
                    DisabledValue = [0],
                    RecommendedValue = null,
                    DefaultValue = null
                },
            },
        };

        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>
            {
                { @"HKEY_CURRENT_USER\Software\Test\Value1", 0 },
                { @"HKEY_CURRENT_USER\Software\Test\Value2", 0 },
            });
        _mockRegistry.Setup(r => r.IsRegistryValueInEnabledState(It.IsAny<RegistrySetting>(), 0, true))
            .Returns(false);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-multi-disabled"].Success.Should().BeTrue();
        result["test-multi-disabled"].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetSettingStatesAsync_CompositeStringKey_DelegatesToIsRegistryValueInEnabledState()
    {
        // CompositeStringKey settings pass the full composite string to IsRegistryValueInEnabledState,
        // which extracts the sub-value internally.
        var setting = new SettingDefinition
        {
            Id = "test-composite",
            Name = "Composite Setting",
            Description = "Tests CompositeStringKey detection",
            InputType = InputType.Toggle,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences",
                    ValueName = "DirectXUserGlobalSettings",
                    CompositeStringKey = "SwapEffectUpgradeEnable",
                    EnabledValue = ["1"],
                    DisabledValue = ["0"],
                    DefaultValue = "1",
                    ValueType = RegistryValueKind.String,
                    RecommendedValue = null
                },
            },
        };

        var compositeString = "SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;";
        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>
            {
                { @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences\DirectXUserGlobalSettings", compositeString },
            });
        _mockRegistry.Setup(r => r.IsRegistryValueInEnabledState(
                It.Is<RegistrySetting>(rs => rs.CompositeStringKey == "SwapEffectUpgradeEnable"),
                compositeString,
                true))
            .Returns(true);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-composite"].Success.Should().BeTrue();
        result["test-composite"].IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetSettingStatesAsync_ApplyPerNetworkInterface_DelegatesToIsSettingApplied()
    {
        // ApplyPerNetworkInterface settings bypass batch values and delegate
        // to IsSettingApplied for correct sub-key expansion.
        var setting = new SettingDefinition
        {
            Id = "test-network-interface",
            Name = "Network Interface Setting",
            Description = "Tests ApplyPerNetworkInterface detection",
            InputType = InputType.Toggle,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces",
                    ValueName = "TcpAckFrequency",
                    EnabledValue = [null],
                    DisabledValue = [1],
                    ValueType = RegistryValueKind.DWord,
                    ApplyPerNetworkInterface = true,
                    RecommendedValue = null,
                    DefaultValue = null
                },
            },
        };

        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>());
        _mockRegistry.Setup(r => r.IsSettingApplied(
                It.Is<RegistrySetting>(rs => rs.ApplyPerNetworkInterface)))
            .Returns(true);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-network-interface"].Success.Should().BeTrue();
        result["test-network-interface"].IsEnabled.Should().BeTrue();
        _mockRegistry.Verify(r => r.IsSettingApplied(
            It.Is<RegistrySetting>(rs => rs.ApplyPerNetworkInterface)), Times.Once);
    }

    [Fact]
    public async Task GetSettingStatesAsync_ApplyPerNetworkInterface_NotApplied_ReturnsFalse()
    {
        var setting = new SettingDefinition
        {
            Id = "test-network-not-applied",
            Name = "Network Interface Not Applied",
            Description = "Tests ApplyPerNetworkInterface when not applied",
            InputType = InputType.Toggle,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces",
                    ValueName = "TcpAckFrequency",
                    EnabledValue = [null],
                    DisabledValue = [1],
                    ValueType = RegistryValueKind.DWord,
                    ApplyPerNetworkInterface = true,
                    RecommendedValue = null,
                    DefaultValue = null
                },
            },
        };

        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>());
        _mockRegistry.Setup(r => r.IsSettingApplied(
                It.Is<RegistrySetting>(rs => rs.ApplyPerNetworkInterface)))
            .Returns(false);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-network-not-applied"].Success.Should().BeTrue();
        result["test-network-not-applied"].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GetSettingStatesAsync_ApplyPerMonitor_DelegatesToIsSettingApplied()
    {
        // ApplyPerMonitor settings bypass batch values and delegate
        // to IsSettingApplied for correct sub-key expansion.
        var setting = new SettingDefinition
        {
            Id = "test-monitor",
            Name = "Monitor Setting",
            Description = "Tests ApplyPerMonitor detection",
            InputType = InputType.Toggle,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\MonitorDataStore",
                    ValueName = "AutoColorManagementEnabled",
                    EnabledValue = [1],
                    DisabledValue = [0],
                    ValueType = RegistryValueKind.DWord,
                    ApplyPerMonitor = true,
                    RecommendedValue = null,
                    DefaultValue = null
                },
            },
        };

        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>());
        _mockRegistry.Setup(r => r.IsSettingApplied(
                It.Is<RegistrySetting>(rs => rs.ApplyPerMonitor)))
            .Returns(true);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-monitor"].Success.Should().BeTrue();
        result["test-monitor"].IsEnabled.Should().BeTrue();
        _mockRegistry.Verify(r => r.IsSettingApplied(
            It.Is<RegistrySetting>(rs => rs.ApplyPerMonitor)), Times.Once);
    }

    [Fact]
    public async Task GetSettingStatesAsync_ApplyPerMonitor_NotApplied_ReturnsFalse()
    {
        var setting = new SettingDefinition
        {
            Id = "test-monitor-not-applied",
            Name = "Monitor Not Applied",
            Description = "Tests ApplyPerMonitor when not applied",
            InputType = InputType.Toggle,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\MonitorDataStore",
                    ValueName = "AutoColorManagementEnabled",
                    EnabledValue = [1],
                    DisabledValue = [0],
                    ValueType = RegistryValueKind.DWord,
                    ApplyPerMonitor = true,
                    RecommendedValue = null,
                    DefaultValue = null
                },
            },
        };

        _mockRegistry.Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>());
        _mockRegistry.Setup(r => r.IsSettingApplied(
                It.Is<RegistrySetting>(rs => rs.ApplyPerMonitor)))
            .Returns(false);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["test-monitor-not-applied"].Success.Should().BeTrue();
        result["test-monitor-not-applied"].IsEnabled.Should().BeFalse();
    }

    // ── Inverted-policy (EnabledValue = [null]) state reading ──

    [Fact]
    public async Task InvertedPolicy_KeyAbsent_ReportsEnabled()
    {
        var setting = new SettingDefinition
        {
            Id = "inverted-policy-key-absent",
            Name = "Inverted Policy",
            Description = "",
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Test",
                    ValueName = "BlockThing",
                    ValueType = RegistryValueKind.DWord,
                    EnabledValue = new object?[] { null },
                    DisabledValue = new object?[] { 1 },
                    RecommendedValue = null,
                    DefaultValue = null,
                    IsGroupPolicy = true,
                },
            },
        };

        _mockRegistry
            .Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?>
            {
                { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Test\BlockThing", null }
            });

        _mockRegistry
            .Setup(r => r.IsRegistryValueInEnabledState(
                It.Is<RegistrySetting>(rs => rs.ValueName == "BlockThing"),
                It.IsAny<object?>(),
                false))
            .Returns(true);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result.Should().ContainKey("inverted-policy-key-absent");
        result["inverted-policy-key-absent"].IsEnabled.Should().BeTrue(
            because: "EnabledValue = [null] means key absent IS the enabled state");
    }

    [Fact]
    public async Task InvertedPolicy_KeyPresentWithDisabledValue_ReportsDisabled()
    {
        var setting = new SettingDefinition
        {
            Id = "inverted-policy-blocked",
            Name = "Inverted Policy",
            Description = "",
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Test",
                    ValueName = "BlockThing",
                    ValueType = RegistryValueKind.DWord,
                    EnabledValue = new object?[] { null },
                    DisabledValue = new object?[] { 1 },
                    RecommendedValue = null,
                    DefaultValue = null,
                    IsGroupPolicy = true,
                },
            },
        };

        _mockRegistry
            .Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?> { { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Test\BlockThing", 1 } });

        _mockRegistry
            .Setup(r => r.IsRegistryValueInEnabledState(
                It.IsAny<RegistrySetting>(), 1, true))
            .Returns(false);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["inverted-policy-blocked"].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task HybridPolicy_EnabledValueOneOrNull_KeyAbsent_ReportsEnabled()
    {
        var setting = new SettingDefinition
        {
            Id = "hybrid-policy",
            Name = "Hybrid",
            Description = "",
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Test",
                    ValueName = "FlagThing",
                    ValueType = RegistryValueKind.DWord,
                    EnabledValue = new object?[] { 1, null },
                    DisabledValue = new object?[] { 0 },
                    RecommendedValue = null,
                    DefaultValue = null,
                    IsGroupPolicy = true,
                },
            },
        };

        _mockRegistry
            .Setup(r => r.GetBatchValues(It.IsAny<IEnumerable<(string, string?)>>()))
            .Returns(new Dictionary<string, object?> { { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Test\FlagThing", null } });

        _mockRegistry
            .Setup(r => r.IsRegistryValueInEnabledState(
                It.IsAny<RegistrySetting>(), It.IsAny<object?>(), false))
            .Returns(true);

        var result = await _service.GetSettingStatesAsync(new[] { setting });

        result["hybrid-policy"].IsEnabled.Should().BeTrue();
    }
}

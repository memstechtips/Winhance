using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class PowerCfgApplierTests
{
    private readonly Mock<IPowerSettingsQueryService> _mockPowerQuery = new();
    private readonly Mock<IHardwareDetectionService> _mockHardware = new();
    private readonly Mock<IComboBoxResolver> _mockComboBox = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly PowerCfgApplier _applier;

    public PowerCfgApplierTests()
    {
        _applier = new PowerCfgApplier(
            _mockPowerQuery.Object,
            _mockHardware.Object,
            _mockComboBox.Object,
            _mockLog.Object);
    }

    private static SettingDefinition CreatePowerSetting(
        string id,
        InputType inputType = InputType.Toggle,
        PowerModeSupport powerMode = PowerModeSupport.Both,
        string? units = null) => new()
    {
        Id = id,
        Name = $"Power Setting {id}",
        Description = $"Description for {id}",
        InputType = inputType,
        NumericRange = units != null
            ? new NumericRangeMetadata { MinValue = 0, MaxValue = 100, Units = units }
            : null,
        PowerCfgSettings = new[]
        {
            new PowerCfgSetting
            {
                SubgroupGuid = "238c9fa8-0aad-41ed-83f4-97be242c8f20",
                SettingGuid = "29f6c1db-86da-48c5-9fdb-f2b67b1f44da",
                PowerModeSupport = powerMode,
                Units = units,
            }
        }
    };

    // ---------------------------------------------------------------
    // Null/Empty PowerCfgSettings - returns success immediately
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyPowerCfgSettingsAsync_NullPowerCfgSettings_ReturnsSuccess()
    {
        var setting = new SettingDefinition
        {
            Id = "no-power",
            Name = "No Power",
            Description = "Test",
            PowerCfgSettings = null,
        };

        var result = await _applier.ApplyPowerCfgSettingsAsync(setting, true, null);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyPowerCfgSettingsAsync_EmptyPowerCfgSettings_ReturnsSuccess()
    {
        var setting = new SettingDefinition
        {
            Id = "empty-power",
            Name = "Empty Power",
            Description = "Test",
            PowerCfgSettings = Array.Empty<PowerCfgSetting>(),
        };

        var result = await _applier.ApplyPowerCfgSettingsAsync(setting, true, null);

        result.Success.Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Test Case 1: Both AC/DC mode applied
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyPowerCfgSettingsAsync_BothMode_Toggle_QueriesCurrentValues()
    {
        // Arrange
        var setting = CreatePowerSetting("both-toggle", InputType.Toggle, PowerModeSupport.Both);
        _mockHardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(true);
        _mockPowerQuery
            .Setup(q => q.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((0, 0));

        // Act: P/Invoke will likely fail in test env, but we verify the pre-flight interactions
        var result = await _applier.ApplyPowerCfgSettingsAsync(setting, true, null);

        // Assert: Returns success (the method always returns Succeeded)
        result.Success.Should().BeTrue();
        _mockHardware.Verify(h => h.HasBatteryAsync(), Times.Once);
        _mockPowerQuery.Verify(q => q.GetPowerSettingACDCValuesAsync(
            It.Is<PowerCfgSetting>(p => p.PowerModeSupport == PowerModeSupport.Both)),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Test Case 2: ACOnly mode - only AC applied
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyPowerCfgSettingsAsync_ACOnlyMode_Toggle_QueriesCurrentValues()
    {
        // Arrange
        var setting = CreatePowerSetting("ac-only", InputType.Toggle, PowerModeSupport.ACOnly);
        _mockHardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(true);
        _mockPowerQuery
            .Setup(q => q.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((0, 0));

        // Act
        var result = await _applier.ApplyPowerCfgSettingsAsync(setting, true, null);

        // Assert
        result.Success.Should().BeTrue();
        _mockPowerQuery.Verify(q => q.GetPowerSettingACDCValuesAsync(
            It.Is<PowerCfgSetting>(p => p.PowerModeSupport == PowerModeSupport.ACOnly)),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Test Case 3: DCOnly mode - only DC applied
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyPowerCfgSettingsAsync_DCOnlyMode_Toggle_QueriesCurrentValues()
    {
        // Arrange
        var setting = CreatePowerSetting("dc-only", InputType.Toggle, PowerModeSupport.DCOnly);
        _mockHardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(true);
        _mockPowerQuery
            .Setup(q => q.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((0, 0));

        // Act
        var result = await _applier.ApplyPowerCfgSettingsAsync(setting, true, null);

        // Assert
        result.Success.Should().BeTrue();
        _mockPowerQuery.Verify(q => q.GetPowerSettingACDCValuesAsync(
            It.Is<PowerCfgSetting>(p => p.PowerModeSupport == PowerModeSupport.DCOnly)),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Test Case 4: Disable mode restores defaults (enable=false => value=0)
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyPowerCfgSettingsAsync_DisableToggle_UsesZeroValue()
    {
        // Arrange: enable=false for Toggle should produce valueToApply=0
        var setting = CreatePowerSetting("disable-toggle", InputType.Toggle, PowerModeSupport.Both);
        _mockHardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(true);
        _mockPowerQuery
            .Setup(q => q.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((1, 1));

        // Act
        var result = await _applier.ApplyPowerCfgSettingsAsync(setting, enable: false, value: null);

        // Assert: The method should log with value 0
        result.Success.Should().BeTrue();
        _mockLog.Verify(l => l.Log(
            LogLevel.Info,
            It.Is<string>(msg => msg.Contains("disable-toggle") && msg.Contains("value: 0")),
            null), Times.Once);
    }

    [Fact]
    public async Task ApplyPowerCfgSettingsAsync_EnableToggle_UsesOneValue()
    {
        // Arrange: enable=true for Toggle should produce valueToApply=1
        var setting = CreatePowerSetting("enable-toggle", InputType.Toggle, PowerModeSupport.Both);
        _mockHardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(true);
        _mockPowerQuery
            .Setup(q => q.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((0, 0));

        // Act
        var result = await _applier.ApplyPowerCfgSettingsAsync(setting, enable: true, value: null);

        // Assert: The method should log with value 1
        result.Success.Should().BeTrue();
        _mockLog.Verify(l => l.Log(
            LogLevel.Info,
            It.Is<string>(msg => msg.Contains("enable-toggle") && msg.Contains("value: 1")),
            null), Times.Once);
    }

    // ---------------------------------------------------------------
    // Test Case 5: Selection input type maps value correctly
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyPowerCfgSettingsAsync_SelectionInput_CallsComboBoxResolver()
    {
        // Arrange: Selection with int index
        var setting = CreatePowerSetting("selection-power", InputType.Selection, PowerModeSupport.Both);
        _mockHardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(true);
        _mockComboBox
            .Setup(c => c.GetValueFromIndex(It.IsAny<SettingDefinition>(), 2))
            .Returns(42);
        _mockPowerQuery
            .Setup(q => q.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((0, 0));

        // Act
        var result = await _applier.ApplyPowerCfgSettingsAsync(setting, enable: true, value: 2);

        // Assert: ComboBoxResolver was called to map index 2 to its real value
        result.Success.Should().BeTrue();
        _mockComboBox.Verify(c => c.GetValueFromIndex(
            It.Is<SettingDefinition>(s => s.Id == "selection-power"), 2), Times.Once);
    }

    [Fact]
    public async Task ApplyPowerCfgSettingsAsync_SelectionSeparate_WithTuple_ResolvesACDCSeparately()
    {
        // Arrange: Selection with Separate PowerModeSupport and ValueTuple
        var setting = CreatePowerSetting("sel-separate", InputType.Selection, PowerModeSupport.Separate);
        _mockHardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(true);
        _mockComboBox
            .Setup(c => c.GetValueFromIndex(It.IsAny<SettingDefinition>(), 1))
            .Returns(10);
        _mockComboBox
            .Setup(c => c.GetValueFromIndex(It.IsAny<SettingDefinition>(), 3))
            .Returns(30);
        _mockPowerQuery
            .Setup(q => q.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((0, 0));

        var tupleValue = (1, 3);

        // Act
        var result = await _applier.ApplyPowerCfgSettingsAsync(setting, enable: true, value: tupleValue);

        // Assert: Resolver called with both AC (index 1) and DC (index 3)
        result.Success.Should().BeTrue();
        _mockComboBox.Verify(c => c.GetValueFromIndex(
            It.Is<SettingDefinition>(s => s.Id == "sel-separate"), 1), Times.Once);
        _mockComboBox.Verify(c => c.GetValueFromIndex(
            It.Is<SettingDefinition>(s => s.Id == "sel-separate"), 3), Times.Once);
    }

    [Fact]
    public async Task ApplyPowerCfgSettingsAsync_SelectionSeparate_WithDictionary_ResolvesACDCSeparately()
    {
        // Arrange: Selection with Separate PowerModeSupport and Dictionary
        var setting = CreatePowerSetting("sel-dict", InputType.Selection, PowerModeSupport.Separate);
        _mockHardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(true);
        _mockComboBox
            .Setup(c => c.GetValueFromIndex(It.IsAny<SettingDefinition>(), 2))
            .Returns(20);
        _mockComboBox
            .Setup(c => c.GetValueFromIndex(It.IsAny<SettingDefinition>(), 4))
            .Returns(40);
        _mockPowerQuery
            .Setup(q => q.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((0, 0));

        var dictValue = new Dictionary<string, object?>
        {
            ["ACValue"] = 2,
            ["DCValue"] = 4,
        };

        // Act
        var result = await _applier.ApplyPowerCfgSettingsAsync(setting, enable: true, value: dictValue);

        // Assert: Resolver called for both AC and DC indices
        result.Success.Should().BeTrue();
        _mockComboBox.Verify(c => c.GetValueFromIndex(
            It.Is<SettingDefinition>(s => s.Id == "sel-dict"), 2), Times.Once);
        _mockComboBox.Verify(c => c.GetValueFromIndex(
            It.Is<SettingDefinition>(s => s.Id == "sel-dict"), 4), Times.Once);
    }

    // ---------------------------------------------------------------
    // Test Case 6: No battery present - skips DC
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyPowerCfgSettingsAsync_NoBattery_BothMode_StillCallsHasBattery()
    {
        // Arrange: No battery => DC write should be skipped inside ExecutePowerCfgSettings
        var setting = CreatePowerSetting("no-battery-both", InputType.Toggle, PowerModeSupport.Both);
        _mockHardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(false);
        _mockPowerQuery
            .Setup(q => q.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((0, 0));

        // Act
        var result = await _applier.ApplyPowerCfgSettingsAsync(setting, true, null);

        // Assert: Battery check was performed; method succeeds
        result.Success.Should().BeTrue();
        _mockHardware.Verify(h => h.HasBatteryAsync(), Times.Once);
    }

    [Fact]
    public async Task ApplyPowerCfgSettingsAsync_NoBattery_DCOnlyMode_StillCallsHasBattery()
    {
        // Arrange: No battery with DCOnly - the inner switch will skip writing
        var setting = CreatePowerSetting("no-battery-dc", InputType.Toggle, PowerModeSupport.DCOnly);
        _mockHardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(false);
        _mockPowerQuery
            .Setup(q => q.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((0, 0));

        // Act
        var result = await _applier.ApplyPowerCfgSettingsAsync(setting, true, null);

        // Assert
        result.Success.Should().BeTrue();
        _mockHardware.Verify(h => h.HasBatteryAsync(), Times.Once);
    }

    [Fact]
    public async Task ApplyPowerCfgSettingsAsync_NoBattery_SeparateSelection_StillResolvesValues()
    {
        // Arrange: No battery with Separate mode selection
        var setting = CreatePowerSetting("no-bat-sep", InputType.Selection, PowerModeSupport.Separate);
        _mockHardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(false);
        _mockComboBox
            .Setup(c => c.GetValueFromIndex(It.IsAny<SettingDefinition>(), It.IsAny<int>()))
            .Returns(5);
        _mockPowerQuery
            .Setup(q => q.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((0, 0));

        var tupleValue = (0, 1);

        // Act
        var result = await _applier.ApplyPowerCfgSettingsAsync(setting, true, tupleValue);

        // Assert: Both AC and DC values are resolved even though DC will be skipped at write time
        result.Success.Should().BeTrue();
        _mockComboBox.Verify(c => c.GetValueFromIndex(It.IsAny<SettingDefinition>(), 0), Times.Once);
        _mockComboBox.Verify(c => c.GetValueFromIndex(It.IsAny<SettingDefinition>(), 1), Times.Once);
    }

    // ---------------------------------------------------------------
    // Additional edge case tests
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyPowerCfgSettingsAsync_NumericRange_NullValue_ReturnsSuccessWithoutApplying()
    {
        // Arrange: NumericRange with null value falls into the "old config format" skip path
        var setting = CreatePowerSetting("numeric-null", InputType.NumericRange, PowerModeSupport.Both);
        _mockHardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(true);

        // Act
        var result = await _applier.ApplyPowerCfgSettingsAsync(setting, true, value: null);

        // Assert: Skips without calling ExecutePowerCfgSettings
        result.Success.Should().BeTrue();
        _mockLog.Verify(l => l.Log(
            LogLevel.Debug,
            It.Is<string>(msg => msg.Contains("Skipping") && msg.Contains("no value provided")),
            null), Times.Once);

        // Power query should NOT be called since we skip early
        _mockPowerQuery.Verify(
            q => q.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplyPowerCfgSettingsAsync_UnsupportedInputType_Throws()
    {
        // Arrange: Action input type with non-null value is not supported
        var setting = CreatePowerSetting("action-type", InputType.Action, PowerModeSupport.Both);
        _mockHardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(true);

        // Act
        var action = () => _applier.ApplyPowerCfgSettingsAsync(setting, true, value: "unsupported");

        // Assert
        await action.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*Action*not supported*");
    }

    [Fact]
    public async Task ApplyPowerCfgSettingsAsync_NumericRangeSeparate_WithDictionary_ResolvesACDC()
    {
        // Arrange: NumericRange with Separate mode and dictionary values
        var setting = CreatePowerSetting(
            "numeric-separate",
            InputType.NumericRange,
            PowerModeSupport.Separate,
            units: null);
        _mockHardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(true);
        _mockPowerQuery
            .Setup(q => q.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((0, 0));

        var dictValue = new Dictionary<string, object?>
        {
            ["ACValue"] = 5,
            ["DCValue"] = 10,
        };

        // Act
        var result = await _applier.ApplyPowerCfgSettingsAsync(setting, true, dictValue);

        // Assert
        result.Success.Should().BeTrue();
        _mockLog.Verify(l => l.Log(
            LogLevel.Info,
            It.Is<string>(msg => msg.Contains("numeric-separate") && msg.Contains("separate AC/DC NumericRange")),
            null), Times.Once);
    }

    [Fact]
    public async Task ApplyPowerCfgSettingsAsync_SelectionInput_LogsCorrectValue()
    {
        // Arrange: Verify that the resolved value is logged
        var setting = CreatePowerSetting("sel-log", InputType.Selection, PowerModeSupport.Both);
        _mockHardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(true);
        _mockComboBox
            .Setup(c => c.GetValueFromIndex(It.IsAny<SettingDefinition>(), 0))
            .Returns(99);
        _mockPowerQuery
            .Setup(q => q.GetPowerSettingACDCValuesAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((0, 0));

        // Act
        var result = await _applier.ApplyPowerCfgSettingsAsync(setting, true, 0);

        // Assert: The log message should contain the resolved value 99
        result.Success.Should().BeTrue();
        _mockLog.Verify(l => l.Log(
            LogLevel.Info,
            It.Is<string>(msg => msg.Contains("sel-log") && msg.Contains("value: 99")),
            null), Times.Once);
    }
}

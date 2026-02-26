using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class TooltipDataServiceTests
{
    private readonly Mock<IWindowsRegistryService> _mockRegistry = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly TooltipDataService _service;

    public TooltipDataServiceTests()
    {
        _service = new TooltipDataService(_mockRegistry.Object, _mockLog.Object);
    }

    private static SettingDefinition CreateSetting(
        string id,
        IReadOnlyList<RegistrySetting>? registrySettings = null,
        IReadOnlyList<ScheduledTaskSetting>? scheduledTaskSettings = null,
        IReadOnlyList<PowerCfgSetting>? powerCfgSettings = null,
        IReadOnlyDictionary<string, object>? customProperties = null)
    {
        var setting = new SettingDefinition
        {
            Id = id,
            Name = $"Setting {id}",
            Description = $"Description for {id}",
        };

        if (registrySettings != null)
            setting = setting with { RegistrySettings = registrySettings };
        if (scheduledTaskSettings != null)
            setting = setting with { ScheduledTaskSettings = scheduledTaskSettings };
        if (powerCfgSettings != null)
            setting = setting with { PowerCfgSettings = powerCfgSettings };
        if (customProperties != null)
            setting = setting with { CustomProperties = customProperties };

        return setting;
    }

    private static RegistrySetting CreateRegistrySetting(
        string keyPath = @"HKLM\SOFTWARE\Test",
        string valueName = "TestValue",
        int? binaryByteIndex = null,
        byte? bitMask = null,
        bool applyPerNetworkInterface = false,
        string? compositeStringKey = null) => new()
    {
        KeyPath = keyPath,
        ValueName = valueName,
        ValueType = RegistryValueKind.DWord,
        BinaryByteIndex = binaryByteIndex,
        BitMask = bitMask,
        ApplyPerNetworkInterface = applyPerNetworkInterface,
        CompositeStringKey = compositeStringKey,
    };

    // ---------------------------------------------------------------
    // GetTooltipDataAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetTooltipDataAsync_SettingWithRegistryOperations_ReturnsTooltipData()
    {
        var regSetting = CreateRegistrySetting();
        var setting = CreateSetting("reg-setting", registrySettings: new[] { regSetting });

        _mockRegistry
            .Setup(r => r.GetValue(regSetting.KeyPath, regSetting.ValueName!))
            .Returns(1);

        var result = await _service.GetTooltipDataAsync(new[] { setting });

        result.Should().ContainKey("reg-setting");
        result["reg-setting"].SettingId.Should().Be("reg-setting");
        result["reg-setting"].DisplayValue.Should().Be("1");
    }

    [Fact]
    public async Task GetTooltipDataAsync_SettingWithNoOperations_ReturnsEmpty()
    {
        var setting = CreateSetting("empty-setting");

        var result = await _service.GetTooltipDataAsync(new[] { setting });

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTooltipDataAsync_MultipleSettings_ReturnsDataForEach()
    {
        var reg1 = CreateRegistrySetting(keyPath: @"HKLM\SOFTWARE\Test1");
        var reg2 = CreateRegistrySetting(keyPath: @"HKLM\SOFTWARE\Test2");
        var setting1 = CreateSetting("s1", registrySettings: new[] { reg1 });
        var setting2 = CreateSetting("s2", registrySettings: new[] { reg2 });

        _mockRegistry
            .Setup(r => r.GetValue(@"HKLM\SOFTWARE\Test1", "TestValue"))
            .Returns(42);
        _mockRegistry
            .Setup(r => r.GetValue(@"HKLM\SOFTWARE\Test2", "TestValue"))
            .Returns("hello");

        var result = await _service.GetTooltipDataAsync(new[] { setting1, setting2 });

        result.Should().HaveCount(2);
        result["s1"].DisplayValue.Should().Be("42");
        result["s2"].DisplayValue.Should().Be("hello");
    }

    [Fact]
    public async Task GetTooltipDataAsync_RegistryValueNull_DisplaysNotSet()
    {
        var regSetting = CreateRegistrySetting();
        var setting = CreateSetting("null-val", registrySettings: new[] { regSetting });

        _mockRegistry
            .Setup(r => r.GetValue(regSetting.KeyPath, regSetting.ValueName!))
            .Returns((object?)null);

        var result = await _service.GetTooltipDataAsync(new[] { setting });

        result.Should().ContainKey("null-val");
        result["null-val"].DisplayValue.Should().Be("(not set)");
    }

    [Fact]
    public async Task GetTooltipDataAsync_SettingWithScheduledTasks_ReturnsTooltipWithTasks()
    {
        var tasks = new[]
        {
            new ScheduledTaskSetting { Id = "task1", TaskPath = @"\Microsoft\Windows\Test" }
        };
        var setting = CreateSetting("task-setting", scheduledTaskSettings: tasks);

        var result = await _service.GetTooltipDataAsync(new[] { setting });

        result.Should().ContainKey("task-setting");
        result["task-setting"].ScheduledTaskSettings.Should().HaveCount(1);
        result["task-setting"].DisplayValue.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTooltipDataAsync_SettingWithPowerCfg_ReturnsTooltipWithPowerCfg()
    {
        var powerSettings = new[]
        {
            new PowerCfgSetting { SettingGUIDAlias = "test-guid" }
        };
        var setting = CreateSetting("power-setting", powerCfgSettings: powerSettings);

        var result = await _service.GetTooltipDataAsync(new[] { setting });

        result.Should().ContainKey("power-setting");
        result["power-setting"].PowerCfgSettings.Should().HaveCount(1);
    }

    // ---------------------------------------------------------------
    // DisableTooltip
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetTooltipDataAsync_SettingWithDisableTooltip_ReturnsNull()
    {
        var customProps = new Dictionary<string, object>
        {
            { CustomPropertyKeys.DisableTooltip, true }
        };
        var regSetting = CreateRegistrySetting();
        var setting = CreateSetting("disabled-tooltip",
            registrySettings: new[] { regSetting },
            customProperties: customProps);

        var result = await _service.GetTooltipDataAsync(new[] { setting });

        result.Should().NotContainKey("disabled-tooltip");
    }

    [Fact]
    public async Task GetTooltipDataAsync_DisableTooltipFalse_StillReturnsData()
    {
        var customProps = new Dictionary<string, object>
        {
            { CustomPropertyKeys.DisableTooltip, false }
        };
        var regSetting = CreateRegistrySetting();
        var setting = CreateSetting("not-disabled",
            registrySettings: new[] { regSetting },
            customProperties: customProps);

        _mockRegistry
            .Setup(r => r.GetValue(regSetting.KeyPath, regSetting.ValueName!))
            .Returns(99);

        var result = await _service.GetTooltipDataAsync(new[] { setting });

        result.Should().ContainKey("not-disabled");
        result["not-disabled"].DisplayValue.Should().Be("99");
    }

    // ---------------------------------------------------------------
    // Binary value with byte index
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetTooltipDataAsync_BinaryValueWithByteIndex_ReturnsTargetByte()
    {
        var regSetting = CreateRegistrySetting(binaryByteIndex: 2);
        var setting = CreateSetting("binary-setting", registrySettings: new[] { regSetting });

        _mockRegistry
            .Setup(r => r.GetValue(regSetting.KeyPath, regSetting.ValueName!))
            .Returns(new byte[] { 0x00, 0x01, 0xAB, 0xFF });

        var result = await _service.GetTooltipDataAsync(new[] { setting });

        result.Should().ContainKey("binary-setting");
        // 0xAB = 171 in decimal
        result["binary-setting"].DisplayValue.Should().Be("171");
    }

    [Fact]
    public async Task GetTooltipDataAsync_BinaryValueWithByteIndexAndBitMask_ReturnsOneOrZero()
    {
        var regSetting = CreateRegistrySetting(binaryByteIndex: 0, bitMask: 0x04);
        var setting = CreateSetting("bitmask-setting", registrySettings: new[] { regSetting });

        // byte[0] = 0x07 => 0x07 & 0x04 = 0x04 != 0 => "1"
        _mockRegistry
            .Setup(r => r.GetValue(regSetting.KeyPath, regSetting.ValueName!))
            .Returns(new byte[] { 0x07, 0x00 });

        var result = await _service.GetTooltipDataAsync(new[] { setting });

        result.Should().ContainKey("bitmask-setting");
        result["bitmask-setting"].DisplayValue.Should().Be("1");
    }

    [Fact]
    public async Task GetTooltipDataAsync_BinaryValueWithBitMaskNotSet_ReturnsZero()
    {
        var regSetting = CreateRegistrySetting(binaryByteIndex: 0, bitMask: 0x08);
        var setting = CreateSetting("bitmask-zero", registrySettings: new[] { regSetting });

        // byte[0] = 0x07 => 0x07 & 0x08 = 0 => "0"
        _mockRegistry
            .Setup(r => r.GetValue(regSetting.KeyPath, regSetting.ValueName!))
            .Returns(new byte[] { 0x07, 0x00 });

        var result = await _service.GetTooltipDataAsync(new[] { setting });

        result["bitmask-zero"].DisplayValue.Should().Be("0");
    }

    [Fact]
    public async Task GetTooltipDataAsync_BinaryValueEmptyArray_ReturnsEmpty()
    {
        var regSetting = CreateRegistrySetting(binaryByteIndex: 0);
        var setting = CreateSetting("empty-binary", registrySettings: new[] { regSetting });

        _mockRegistry
            .Setup(r => r.GetValue(regSetting.KeyPath, regSetting.ValueName!))
            .Returns(new byte[] { });

        var result = await _service.GetTooltipDataAsync(new[] { setting });

        result["empty-binary"].DisplayValue.Should().Be("(empty)");
    }

    [Fact]
    public async Task GetTooltipDataAsync_BinaryValueNoBinaryByteIndex_ReturnsSpaceJoined()
    {
        var regSetting = CreateRegistrySetting(); // no BinaryByteIndex
        var setting = CreateSetting("raw-binary", registrySettings: new[] { regSetting });

        _mockRegistry
            .Setup(r => r.GetValue(regSetting.KeyPath, regSetting.ValueName!))
            .Returns(new byte[] { 0x0A, 0x0B, 0x0C });

        var result = await _service.GetTooltipDataAsync(new[] { setting });

        result["raw-binary"].DisplayValue.Should().Be("10 11 12");
    }

    // ---------------------------------------------------------------
    // ApplyPerNetworkInterface
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetTooltipDataAsync_PerNetworkInterface_ReadsFromFirstSubKey()
    {
        var regSetting = CreateRegistrySetting(
            keyPath: @"HKLM\SYSTEM\Interfaces",
            applyPerNetworkInterface: true);
        var setting = CreateSetting("nic-setting", registrySettings: new[] { regSetting });

        _mockRegistry
            .Setup(r => r.GetSubKeyNames(@"HKLM\SYSTEM\Interfaces"))
            .Returns(new[] { "{GUID-1}", "{GUID-2}" });
        _mockRegistry
            .Setup(r => r.GetValue(@"HKLM\SYSTEM\Interfaces\{GUID-1}", "TestValue"))
            .Returns(1);

        var result = await _service.GetTooltipDataAsync(new[] { setting });

        result.Should().ContainKey("nic-setting");
        result["nic-setting"].DisplayValue.Should().Be("1");
    }

    [Fact]
    public async Task GetTooltipDataAsync_PerNetworkInterface_NoSubKeys_DisplaysNotSet()
    {
        var regSetting = CreateRegistrySetting(
            keyPath: @"HKLM\SYSTEM\Interfaces",
            applyPerNetworkInterface: true);
        var setting = CreateSetting("nic-empty", registrySettings: new[] { regSetting });

        _mockRegistry
            .Setup(r => r.GetSubKeyNames(@"HKLM\SYSTEM\Interfaces"))
            .Returns(Array.Empty<string>());

        var result = await _service.GetTooltipDataAsync(new[] { setting });

        result["nic-empty"].DisplayValue.Should().Be("(not set)");
    }

    // ---------------------------------------------------------------
    // RefreshTooltipDataAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task RefreshTooltipDataAsync_ReturnsUpdatedData()
    {
        var regSetting = CreateRegistrySetting();
        var setting = CreateSetting("refresh-me", registrySettings: new[] { regSetting });

        _mockRegistry
            .Setup(r => r.GetValue(regSetting.KeyPath, regSetting.ValueName!))
            .Returns(77);

        var result = await _service.RefreshTooltipDataAsync("refresh-me", setting);

        result.Should().NotBeNull();
        result!.SettingId.Should().Be("refresh-me");
        result.DisplayValue.Should().Be("77");
    }

    [Fact]
    public async Task RefreshTooltipDataAsync_NoRegistrySettings_ReturnsNull()
    {
        var setting = CreateSetting("no-ops");

        var result = await _service.RefreshTooltipDataAsync("no-ops", setting);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshTooltipDataAsync_DisableTooltip_ReturnsNull()
    {
        var customProps = new Dictionary<string, object>
        {
            { CustomPropertyKeys.DisableTooltip, true }
        };
        var regSetting = CreateRegistrySetting();
        var setting = CreateSetting("disabled",
            registrySettings: new[] { regSetting },
            customProperties: customProps);

        var result = await _service.RefreshTooltipDataAsync("disabled", setting);

        result.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // RefreshMultipleTooltipDataAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task RefreshMultipleTooltipDataAsync_HandlesMultipleSettings()
    {
        var reg1 = CreateRegistrySetting(keyPath: @"HKLM\SOFTWARE\A");
        var reg2 = CreateRegistrySetting(keyPath: @"HKLM\SOFTWARE\B");
        var s1 = CreateSetting("multi-1", registrySettings: new[] { reg1 });
        var s2 = CreateSetting("multi-2", registrySettings: new[] { reg2 });

        _mockRegistry.Setup(r => r.GetValue(@"HKLM\SOFTWARE\A", "TestValue")).Returns(10);
        _mockRegistry.Setup(r => r.GetValue(@"HKLM\SOFTWARE\B", "TestValue")).Returns(20);

        var result = await _service.RefreshMultipleTooltipDataAsync(new[] { s1, s2 });

        result.Should().HaveCount(2);
        result["multi-1"].DisplayValue.Should().Be("10");
        result["multi-2"].DisplayValue.Should().Be("20");
    }

    [Fact]
    public async Task RefreshMultipleTooltipDataAsync_SkipsSettingsWithNoOperations()
    {
        var regSetting = CreateRegistrySetting();
        var s1 = CreateSetting("has-ops", registrySettings: new[] { regSetting });
        var s2 = CreateSetting("no-ops");

        _mockRegistry
            .Setup(r => r.GetValue(regSetting.KeyPath, regSetting.ValueName!))
            .Returns(5);

        var result = await _service.RefreshMultipleTooltipDataAsync(new[] { s1, s2 });

        result.Should().HaveCount(1);
        result.Should().ContainKey("has-ops");
        result.Should().NotContainKey("no-ops");
    }

    [Fact]
    public async Task RefreshMultipleTooltipDataAsync_EmptyInput_ReturnsEmpty()
    {
        var result = await _service.RefreshMultipleTooltipDataAsync(
            Array.Empty<SettingDefinition>());

        result.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // IndividualRegistryValues tracking
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetTooltipDataAsync_MultipleRegistrySettings_TracksIndividualValues()
    {
        var reg1 = CreateRegistrySetting(keyPath: @"HKLM\SOFTWARE\A", valueName: "Val1");
        var reg2 = CreateRegistrySetting(keyPath: @"HKLM\SOFTWARE\B", valueName: "Val2");
        var setting = CreateSetting("multi-reg", registrySettings: new[] { reg1, reg2 });

        _mockRegistry.Setup(r => r.GetValue(@"HKLM\SOFTWARE\A", "Val1")).Returns(100);
        _mockRegistry.Setup(r => r.GetValue(@"HKLM\SOFTWARE\B", "Val2")).Returns(200);

        var result = await _service.GetTooltipDataAsync(new[] { setting });

        var data = result["multi-reg"];
        data.IndividualRegistryValues.Should().HaveCount(2);
        data.IndividualRegistryValues[reg1].Should().Be("100");
        data.IndividualRegistryValues[reg2].Should().Be("200");
        // DisplayValue should match the primary (first) registry setting
        data.DisplayValue.Should().Be("100");
    }

    // ---------------------------------------------------------------
    // Constructor guard clauses
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_NullRegistryService_ThrowsArgumentNull()
    {
        var action = () => new TooltipDataService(null!, _mockLog.Object);
        action.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("windowsRegistryService");
    }

    [Fact]
    public void Constructor_NullLogService_ThrowsArgumentNull()
    {
        var action = () => new TooltipDataService(_mockRegistry.Object, null!);
        action.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("logService");
    }
}

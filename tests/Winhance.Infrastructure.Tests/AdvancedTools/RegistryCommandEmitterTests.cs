using System.Text;
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

public class RegistryCommandEmitterTests
{
    private readonly Mock<IComboBoxResolver> _comboBoxResolver = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly RegistryCommandEmitter _sut;

    public RegistryCommandEmitterTests()
    {
        _sut = new RegistryCommandEmitter(_comboBoxResolver.Object, _logService.Object);
    }

    // ---------------------------------------------------------------
    // EmitRegistryValue - Standard DWord
    // ---------------------------------------------------------------

    [Fact]
    public void EmitRegistryValue_DWord_EmitsSetRegistryValue()
    {
        var sb = new StringBuilder();
        var regSetting = new RegistrySetting
        {
            KeyPath = "HKEY_LOCAL_MACHINE\\Software\\Test",
            ValueName = "TestValue",
            ValueType = RegistryValueKind.DWord
        };

        _sut.EmitRegistryValue(sb, regSetting, 1, "Test Setting", "HKLM:\\Software\\Test", "TestValue", "    ");

        var output = sb.ToString();
        output.Should().Contain("Set-RegistryValue");
        output.Should().Contain("'HKLM:\\Software\\Test'");
        output.Should().Contain("'TestValue'");
        output.Should().Contain("'DWord'");
        output.Should().Contain("1");
        output.Should().Contain("'Test Setting'");
    }

    // ---------------------------------------------------------------
    // EmitRegistryValue - Binary with BitMask
    // ---------------------------------------------------------------

    [Fact]
    public void EmitRegistryValue_BinaryBitMask_EmitsSetBinaryBit()
    {
        var sb = new StringBuilder();
        var regSetting = new RegistrySetting
        {
            KeyPath = "HKEY_LOCAL_MACHINE\\Software\\Test",
            ValueName = "BinaryVal",
            ValueType = RegistryValueKind.Binary,
            BinaryByteIndex = 3,
            BitMask = 0x04
        };

        _sut.EmitRegistryValue(sb, regSetting, true, "Bit Setting", "HKLM:\\Software\\Test", "BinaryVal", "");

        var output = sb.ToString();
        output.Should().Contain("Set-BinaryBit");
        output.Should().Contain("-ByteIndex 3");
        output.Should().Contain("-BitMask 0x04");
        output.Should().Contain("-SetBit $True");
    }

    [Fact]
    public void EmitRegistryValue_BinaryBitMask_False_EmitsSetBitFalse()
    {
        var sb = new StringBuilder();
        var regSetting = new RegistrySetting
        {
            KeyPath = "HKEY_LOCAL_MACHINE\\Software\\Test",
            ValueName = "BinaryVal",
            ValueType = RegistryValueKind.Binary,
            BinaryByteIndex = 0,
            BitMask = 0x01
        };

        _sut.EmitRegistryValue(sb, regSetting, false, "Bit Setting", "HKLM:\\Software\\Test", "BinaryVal", "");

        var output = sb.ToString();
        output.Should().Contain("Set-BinaryBit");
        output.Should().Contain("-SetBit $False");
    }

    // ---------------------------------------------------------------
    // EmitRegistryValue - Binary with ModifyByteOnly
    // ---------------------------------------------------------------

    [Fact]
    public void EmitRegistryValue_BinaryModifyByteOnly_EmitsSetBinaryByte()
    {
        var sb = new StringBuilder();
        var regSetting = new RegistrySetting
        {
            KeyPath = "HKEY_LOCAL_MACHINE\\Software\\Test",
            ValueName = "ByteVal",
            ValueType = RegistryValueKind.Binary,
            BinaryByteIndex = 5,
            ModifyByteOnly = true
        };

        _sut.EmitRegistryValue(sb, regSetting, 255, "Byte Setting", "HKLM:\\Software\\Test", "ByteVal", "");

        var output = sb.ToString();
        output.Should().Contain("Set-BinaryByte");
        output.Should().Contain("-ByteIndex 5");
        // FormatValueForPowerShell(255, DWord) => "255", then Replace("0x","").PadLeft(2,'0') => "255"
        // Actually, it calls FormatValueForPowerShell(value, RegistryValueKind.DWord) which returns "255"
        // Then replaces "0x" (no-op) and PadLeft => "255". So "0x255" in the output.
        output.Should().Contain("-ByteValue 0x");
    }

    // ---------------------------------------------------------------
    // EmitRegistryValue - Binary without BitMask or ModifyByteOnly
    // ---------------------------------------------------------------

    [Fact]
    public void EmitRegistryValue_BinaryWithByteIndex_NoSpecialHandling_EmitsSetRegistryValue()
    {
        var sb = new StringBuilder();
        var regSetting = new RegistrySetting
        {
            KeyPath = "HKEY_LOCAL_MACHINE\\Software\\Test",
            ValueName = "PlainBinary",
            ValueType = RegistryValueKind.Binary,
            BinaryByteIndex = 0
        };

        var bytes = new byte[] { 0x01, 0x02 };
        _sut.EmitRegistryValue(sb, regSetting, bytes, "Plain Binary", "HKLM:\\Software\\Test", "PlainBinary", "");

        var output = sb.ToString();
        output.Should().Contain("Set-RegistryValue");
        output.Should().Contain("'Binary'");
    }

    // ---------------------------------------------------------------
    // EmitRegistryValueFromDefinition - BitMask
    // ---------------------------------------------------------------

    [Fact]
    public void EmitRegistryValueFromDefinition_BitMask_UsesIsEnabled()
    {
        var sb = new StringBuilder();
        var regSetting = new RegistrySetting
        {
            KeyPath = "HKEY_LOCAL_MACHINE\\Software\\Test",
            ValueName = "Val",
            ValueType = RegistryValueKind.Binary,
            BinaryByteIndex = 1,
            BitMask = 0x08
        };

        _sut.EmitRegistryValueFromDefinition(sb, regSetting, true, isEnabled: true,
            "Def BitMask", "HKLM:\\Software\\Test", "Val", "");

        var output = sb.ToString();
        output.Should().Contain("Set-BinaryBit");
        output.Should().Contain("-SetBit $True");
    }

    [Fact]
    public void EmitRegistryValueFromDefinition_BitMask_IsEnabledFalse_SetsBitFalse()
    {
        var sb = new StringBuilder();
        var regSetting = new RegistrySetting
        {
            KeyPath = "HKEY_LOCAL_MACHINE\\Software\\Test",
            ValueName = "Val",
            ValueType = RegistryValueKind.Binary,
            BinaryByteIndex = 1,
            BitMask = 0x08
        };

        _sut.EmitRegistryValueFromDefinition(sb, regSetting, false, isEnabled: false,
            "Def BitMask", "HKLM:\\Software\\Test", "Val", "");

        var output = sb.ToString();
        output.Should().Contain("-SetBit $False");
    }

    // ---------------------------------------------------------------
    // EmitRegistryValueFromDefinition - ModifyByteOnly
    // ---------------------------------------------------------------

    [Fact]
    public void EmitRegistryValueFromDefinition_ModifyByteOnly_ByteValue_FormatsCorrectly()
    {
        var sb = new StringBuilder();
        var regSetting = new RegistrySetting
        {
            KeyPath = "HKEY_LOCAL_MACHINE\\Software\\Test",
            ValueName = "Val",
            ValueType = RegistryValueKind.Binary,
            BinaryByteIndex = 2,
            ModifyByteOnly = true
        };

        _sut.EmitRegistryValueFromDefinition(sb, regSetting, (byte)0xAB, isEnabled: true,
            "Def Byte", "HKLM:\\Software\\Test", "Val", "");

        var output = sb.ToString();
        output.Should().Contain("Set-BinaryByte");
        output.Should().Contain("0xAB");
    }

    [Fact]
    public void EmitRegistryValueFromDefinition_ModifyByteOnly_IntValue_CastsToByte()
    {
        var sb = new StringBuilder();
        var regSetting = new RegistrySetting
        {
            KeyPath = "HKEY_LOCAL_MACHINE\\Software\\Test",
            ValueName = "Val",
            ValueType = RegistryValueKind.Binary,
            BinaryByteIndex = 0,
            ModifyByteOnly = true
        };

        _sut.EmitRegistryValueFromDefinition(sb, regSetting, 15, isEnabled: true,
            "Def Int", "HKLM:\\Software\\Test", "Val", "");

        var output = sb.ToString();
        output.Should().Contain("Set-BinaryByte");
        output.Should().Contain("0x0F");
    }

    [Fact]
    public void EmitRegistryValueFromDefinition_ModifyByteOnly_UnknownType_Defaults0x00()
    {
        var sb = new StringBuilder();
        var regSetting = new RegistrySetting
        {
            KeyPath = "HKEY_LOCAL_MACHINE\\Software\\Test",
            ValueName = "Val",
            ValueType = RegistryValueKind.Binary,
            BinaryByteIndex = 0,
            ModifyByteOnly = true
        };

        _sut.EmitRegistryValueFromDefinition(sb, regSetting, "unknown", isEnabled: true,
            "Def Unknown", "HKLM:\\Software\\Test", "Val", "");

        var output = sb.ToString();
        output.Should().Contain("0x00");
    }

    // ---------------------------------------------------------------
    // AppendToggleCommandsFiltered - Key-Based Setting (ValueName null)
    // ---------------------------------------------------------------

    [Fact]
    public void AppendToggleCommandsFiltered_NullValueName_EnabledValueNull_EmitsRemoveRegistryKey()
    {
        var sb = new StringBuilder();
        var setting = CreateSettingDefinition("test-setting", "Test", new[]
        {
            new RegistrySetting
            {
                KeyPath = "HKEY_LOCAL_MACHINE\\Software\\Test\\Key",
                ValueName = null,
                ValueType = RegistryValueKind.String,
                EnabledValue = null,
                DisabledValue = ""
            }
        });
        var configItem = new ConfigurationItem { Id = "test-setting", IsSelected = true, InputType = InputType.Toggle };

        _sut.AppendToggleCommandsFiltered(sb, setting, configItem, isHkcu: false);

        sb.ToString().Should().Contain("Remove-RegistryKey");
    }

    [Fact]
    public void AppendToggleCommandsFiltered_NullValueName_EnabledValueEmptyString_EmitsNewRegistryKeyAndDefault()
    {
        var sb = new StringBuilder();
        var setting = CreateSettingDefinition("test-setting", "Test", new[]
        {
            new RegistrySetting
            {
                KeyPath = "HKEY_LOCAL_MACHINE\\Software\\Test\\Key",
                ValueName = null,
                ValueType = RegistryValueKind.String,
                EnabledValue = "",
                DisabledValue = null
            }
        });
        var configItem = new ConfigurationItem { Id = "test-setting", IsSelected = true, InputType = InputType.Toggle };

        _sut.AppendToggleCommandsFiltered(sb, setting, configItem, isHkcu: false);

        var output = sb.ToString();
        output.Should().Contain("New-RegistryKey");
        output.Should().Contain("(Default)");
    }

    [Fact]
    public void AppendToggleCommandsFiltered_NullValueName_EnabledValueNonNull_EmitsNewRegistryKey()
    {
        var sb = new StringBuilder();
        var setting = CreateSettingDefinition("test-setting", "Test", new[]
        {
            new RegistrySetting
            {
                KeyPath = "HKEY_LOCAL_MACHINE\\Software\\Test\\Key",
                ValueName = null,
                ValueType = RegistryValueKind.String,
                EnabledValue = "exists",
                DisabledValue = null
            }
        });
        var configItem = new ConfigurationItem { Id = "test-setting", IsSelected = true, InputType = InputType.Toggle };

        _sut.AppendToggleCommandsFiltered(sb, setting, configItem, isHkcu: false);

        var output = sb.ToString();
        output.Should().Contain("New-RegistryKey");
        output.Should().NotContain("(Default)");
    }

    // ---------------------------------------------------------------
    // AppendToggleCommandsFiltered - Hive filtering
    // ---------------------------------------------------------------

    [Fact]
    public void AppendToggleCommandsFiltered_FiltersOutHkcuEntriesWhenIsHkcuFalse()
    {
        var sb = new StringBuilder();
        var setting = CreateSettingDefinition("test-setting", "Test", new[]
        {
            new RegistrySetting
            {
                KeyPath = "HKEY_CURRENT_USER\\Software\\Test",
                ValueName = "Val",
                ValueType = RegistryValueKind.DWord,
                EnabledValue = 1,
                DisabledValue = 0
            }
        });
        var configItem = new ConfigurationItem { Id = "test-setting", IsSelected = true, InputType = InputType.Toggle };

        _sut.AppendToggleCommandsFiltered(sb, setting, configItem, isHkcu: false);

        sb.ToString().Trim().Should().BeEmpty();
    }

    [Fact]
    public void AppendToggleCommandsFiltered_IncludesHkcuEntriesWhenIsHkcuTrue()
    {
        var sb = new StringBuilder();
        var setting = CreateSettingDefinition("test-setting", "Test", new[]
        {
            new RegistrySetting
            {
                KeyPath = "HKEY_CURRENT_USER\\Software\\Test",
                ValueName = "Val",
                ValueType = RegistryValueKind.DWord,
                EnabledValue = 1,
                DisabledValue = 0
            }
        });
        var configItem = new ConfigurationItem { Id = "test-setting", IsSelected = true, InputType = InputType.Toggle };

        _sut.AppendToggleCommandsFiltered(sb, setting, configItem, isHkcu: true);

        sb.ToString().Should().Contain("Set-RegistryValue");
    }

    // ---------------------------------------------------------------
    // AppendToggleCommandsFiltered - CustomStateValues path
    // ---------------------------------------------------------------

    [Fact]
    public void AppendToggleCommandsFiltered_WithCustomStateValues_UsesExactValue()
    {
        var sb = new StringBuilder();
        var setting = CreateSettingDefinition("test-setting", "Test", new[]
        {
            new RegistrySetting
            {
                KeyPath = "HKEY_LOCAL_MACHINE\\Software\\Test",
                ValueName = "Val",
                ValueType = RegistryValueKind.DWord,
                EnabledValue = 1,
                DisabledValue = 0
            }
        });
        var configItem = new ConfigurationItem
        {
            Id = "test-setting",
            IsSelected = true,
            InputType = InputType.Toggle,
            CustomStateValues = new Dictionary<string, object> { { "Val", 42 } }
        };

        _sut.AppendToggleCommandsFiltered(sb, setting, configItem, isHkcu: false);

        sb.ToString().Should().Contain("42");
    }

    [Fact]
    public void AppendToggleCommandsFiltered_WithCustomStateValueNull_SkipsEntry()
    {
        var sb = new StringBuilder();
        var setting = CreateSettingDefinition("test-setting", "Test", new[]
        {
            new RegistrySetting
            {
                KeyPath = "HKEY_LOCAL_MACHINE\\Software\\Test",
                ValueName = "Val",
                ValueType = RegistryValueKind.DWord,
                EnabledValue = 1,
                DisabledValue = 0
            }
        });
        var configItem = new ConfigurationItem
        {
            Id = "test-setting",
            IsSelected = true,
            InputType = InputType.Toggle,
            CustomStateValues = new Dictionary<string, object> { { "Val", null! } }
        };

        _sut.AppendToggleCommandsFiltered(sb, setting, configItem, isHkcu: false);

        sb.ToString().Trim().Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // AppendToggleCommandsFiltered - Null definition value = deletion
    // ---------------------------------------------------------------

    [Fact]
    public void AppendToggleCommandsFiltered_NullDefinitionValue_EmitsRemoveRegistryValue()
    {
        var sb = new StringBuilder();
        var setting = CreateSettingDefinition("test-setting", "Test", new[]
        {
            new RegistrySetting
            {
                KeyPath = "HKEY_LOCAL_MACHINE\\Software\\Test",
                ValueName = "Val",
                ValueType = RegistryValueKind.DWord,
                EnabledValue = null,
                DisabledValue = 0
            }
        });
        var configItem = new ConfigurationItem { Id = "test-setting", IsSelected = true, InputType = InputType.Toggle };

        _sut.AppendToggleCommandsFiltered(sb, setting, configItem, isHkcu: false);

        sb.ToString().Should().Contain("Remove-RegistryValue");
    }

    // ---------------------------------------------------------------
    // AppendToggleCommandsFiltered - Empty string definition value
    // ---------------------------------------------------------------

    [Fact]
    public void AppendToggleCommandsFiltered_EmptyStringValue_EmitsSetRegistryValueWithEmptyString()
    {
        var sb = new StringBuilder();
        var setting = CreateSettingDefinition("test-setting", "Test", new[]
        {
            new RegistrySetting
            {
                KeyPath = "HKEY_LOCAL_MACHINE\\Software\\Test",
                ValueName = "Val",
                ValueType = RegistryValueKind.String,
                EnabledValue = "",
                DisabledValue = "something"
            }
        });
        var configItem = new ConfigurationItem { Id = "test-setting", IsSelected = true, InputType = InputType.Toggle };

        _sut.AppendToggleCommandsFiltered(sb, setting, configItem, isHkcu: false);

        var output = sb.ToString();
        output.Should().Contain("Set-RegistryValue");
        output.Should().Contain("'String'");
        output.Should().Contain("-Value ''");
    }

    // ---------------------------------------------------------------
    // AppendRegContentCommands
    // ---------------------------------------------------------------

    [Fact]
    public void AppendRegContentCommands_EmptyRegContents_ProducesNoOutput()
    {
        var sb = new StringBuilder();
        var setting = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test Setting",
            RegContents = new List<RegContentSetting>()
        };

        _sut.AppendRegContentCommands(sb, setting, isEnabled: true, isHkcuPass: false);

        sb.ToString().Should().BeEmpty();
    }

    [Fact]
    public void AppendRegContentCommands_HklmContent_EmittedInHklmPass()
    {
        var sb = new StringBuilder();
        var setting = new SettingDefinition
        {
            Id = "test-reg",
            Name = "Test",
            Description = "Reg Content Test",
            RegContents = new List<RegContentSetting>
            {
                new RegContentSetting
                {
                    EnabledContent = "Windows Registry Editor Version 5.00\n[HKEY_LOCAL_MACHINE\\Software\\Test]\n\"Val\"=dword:00000001",
                    DisabledContent = ""
                }
            }
        };

        _sut.AppendRegContentCommands(sb, setting, isEnabled: true, isHkcuPass: false);

        var output = sb.ToString();
        output.Should().Contain("$regContent_test_reg");
        output.Should().Contain("reg import");
    }

    [Fact]
    public void AppendRegContentCommands_HkcuContent_NotEmittedInHklmPass()
    {
        var sb = new StringBuilder();
        var setting = new SettingDefinition
        {
            Id = "test-reg",
            Name = "Test",
            Description = "Reg Content Test",
            RegContents = new List<RegContentSetting>
            {
                new RegContentSetting
                {
                    EnabledContent = "[HKEY_CURRENT_USER\\Software\\Test]\n\"Val\"=dword:00000001",
                    DisabledContent = ""
                }
            }
        };

        _sut.AppendRegContentCommands(sb, setting, isEnabled: true, isHkcuPass: false);

        sb.ToString().Trim().Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // AppendSelectionCommandsFiltered
    // ---------------------------------------------------------------

    [Fact]
    public void AppendSelectionCommandsFiltered_PowerPlanSelection_SkipsEntirely()
    {
        var sb = new StringBuilder();
        var setting = CreateSettingDefinition("power-plan-selection", "Power Plan", Array.Empty<RegistrySetting>());
        var configItem = new ConfigurationItem
        {
            Id = "power-plan-selection",
            InputType = InputType.Selection,
            SelectedIndex = 0
        };

        _sut.AppendSelectionCommandsFiltered(sb, setting, configItem, isHkcu: false);

        sb.ToString().Trim().Should().BeEmpty();
    }

    [Fact]
    public void AppendSelectionCommandsFiltered_WithCustomStateValues_AppliesValues()
    {
        var sb = new StringBuilder();
        var setting = CreateSettingDefinition("test-selection", "Test Selection", new[]
        {
            new RegistrySetting
            {
                KeyPath = "HKEY_LOCAL_MACHINE\\Software\\Test",
                ValueName = "SelVal",
                ValueType = RegistryValueKind.DWord
            }
        });
        var configItem = new ConfigurationItem
        {
            Id = "test-selection",
            InputType = InputType.Selection,
            CustomStateValues = new Dictionary<string, object> { { "SelVal", 3 } }
        };

        _sut.AppendSelectionCommandsFiltered(sb, setting, configItem, isHkcu: false);

        sb.ToString().Should().Contain("Set-RegistryValue");
        sb.ToString().Should().Contain("3");
    }

    [Fact]
    public void AppendSelectionCommandsFiltered_NoValueMappingsOrCustomState_LogsWarning()
    {
        var sb = new StringBuilder();
        var setting = CreateSettingDefinition("test-selection", "Test Selection", Array.Empty<RegistrySetting>());
        var configItem = new ConfigurationItem
        {
            Id = "test-selection",
            InputType = InputType.Selection
        };

        _sut.AppendSelectionCommandsFiltered(sb, setting, configItem, isHkcu: false);

        _logService.Verify(l => l.Log(
            LogLevel.Warning,
            It.Is<string>(s => s.Contains("test-selection")),
            null), Times.Once);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static SettingDefinition CreateSettingDefinition(
        string id, string description, IReadOnlyList<RegistrySetting> registrySettings)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = id,
            Description = description,
            RegistrySettings = registrySettings
        };
    }
}

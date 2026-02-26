using System.Collections.ObjectModel;
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ComboBoxResolverTests
{
    private readonly Mock<ISystemSettingsDiscoveryService> _discoveryService;
    private readonly ComboBoxResolver _sut;

    public ComboBoxResolverTests()
    {
        _discoveryService = new Mock<ISystemSettingsDiscoveryService>();
        _sut = new ComboBoxResolver(_discoveryService.Object);
    }

    #region GetValueFromIndex

    [Fact]
    public void GetValueFromIndex_WithDefinedIndex_ReturnsCorrectValue()
    {
        // Mapping: index 0 -> {"TestValue": 10}, index 1 -> {"TestValue": 20}
        var mappings = new Dictionary<int, Dictionary<string, object?>>
        {
            { 0, new Dictionary<string, object?> { { "TestValue", 10 } } },
            { 1, new Dictionary<string, object?> { { "TestValue", 20 } } },
        };
        var setting = CreateSelectionSetting("test", mappings);

        var result = _sut.GetValueFromIndex(setting, 1);

        result.Should().Be(20);
    }

    [Fact]
    public void GetValueFromIndex_WithCustomStateIndex_ReturnsZero()
    {
        var mappings = new Dictionary<int, Dictionary<string, object?>>
        {
            { 0, new Dictionary<string, object?> { { "TestValue", 10 } } },
        };
        var setting = CreateSelectionSetting("test", mappings);

        var result = _sut.GetValueFromIndex(setting, ComboBoxConstants.CustomStateIndex);

        result.Should().Be(0);
    }

    [Fact]
    public void GetValueFromIndex_WithNoValueMappings_ReturnsIndexAsValue()
    {
        var setting = CreateBasicSetting("test");

        var result = _sut.GetValueFromIndex(setting, 5);

        result.Should().Be(5);
    }

    [Fact]
    public void GetValueFromIndex_IndexNotInMappings_ReturnsIndex()
    {
        var mappings = new Dictionary<int, Dictionary<string, object?>>
        {
            { 0, new Dictionary<string, object?> { { "TestValue", 10 } } },
        };
        var setting = CreateSelectionSetting("test", mappings);

        var result = _sut.GetValueFromIndex(setting, 99);

        result.Should().Be(99);
    }

    #endregion

    #region ResolveRawValuesToIndex

    [Fact]
    public void ResolveRawValuesToIndex_MapsRawValuesToCorrectIndex()
    {
        var mappings = new Dictionary<int, Dictionary<string, object?>>
        {
            { 0, new Dictionary<string, object?> { { "TestValue", 0 } } },
            { 1, new Dictionary<string, object?> { { "TestValue", 1 } } },
            { 2, new Dictionary<string, object?> { { "TestValue", 2 } } },
        };
        var setting = CreateSelectionSettingWithRegistry("test", mappings, "TestValue");

        var rawValues = new Dictionary<string, object?> { { "TestValue", 2 } };

        var result = _sut.ResolveRawValuesToIndex(setting, rawValues);

        result.Should().Be(2);
    }

    [Fact]
    public void ResolveRawValuesToIndex_WithCurrentPolicyIndex_ReturnsPolicyIndex()
    {
        var mappings = new Dictionary<int, Dictionary<string, object?>>
        {
            { 0, new Dictionary<string, object?> { { "TestValue", 0 } } },
            { 1, new Dictionary<string, object?> { { "TestValue", 1 } } },
        };
        var setting = CreateSelectionSettingWithRegistry("test", mappings, "TestValue");

        var rawValues = new Dictionary<string, object?>
        {
            { "CurrentPolicyIndex", 1 },
            { "TestValue", 0 },
        };

        var result = _sut.ResolveRawValuesToIndex(setting, rawValues);

        // CurrentPolicyIndex takes priority
        result.Should().Be(1);
    }

    [Fact]
    public void ResolveRawValuesToIndex_NoMatchAndSupportsCustomState_ReturnsCustomStateIndex()
    {
        var mappings = new Dictionary<int, Dictionary<string, object?>>
        {
            { 0, new Dictionary<string, object?> { { "TestValue", 0 } } },
            { 1, new Dictionary<string, object?> { { "TestValue", 1 } } },
        };

        var customProps = new Dictionary<string, object>
        {
            { CustomPropertyKeys.ValueMappings, mappings },
            { CustomPropertyKeys.SupportsCustomState, true },
        };

        var setting = new SettingDefinition
        {
            Id = "test",
            Name = "Test Setting",
            Description = "Test",
            InputType = InputType.Selection,
            CustomProperties = new ReadOnlyDictionary<string, object>(customProps),
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKLM\Test",
                    ValueName = "TestValue",
                    ValueType = RegistryValueKind.DWord,
                },
            },
        };

        // Raw value 99 doesn't match any mapping
        var rawValues = new Dictionary<string, object?> { { "TestValue", 99 } };

        var result = _sut.ResolveRawValuesToIndex(setting, rawValues);

        result.Should().Be(ComboBoxConstants.CustomStateIndex);
    }

    [Fact]
    public void ResolveRawValuesToIndex_NoMappingsProperty_ReturnsZero()
    {
        var setting = CreateBasicSetting("test");
        var rawValues = new Dictionary<string, object?> { { "TestValue", 1 } };

        var result = _sut.ResolveRawValuesToIndex(setting, rawValues);

        result.Should().Be(0);
    }

    [Fact]
    public void ResolveRawValuesToIndex_UsesDefaultValue_WhenRawValueMissing()
    {
        var mappings = new Dictionary<int, Dictionary<string, object?>>
        {
            { 0, new Dictionary<string, object?> { { "TestValue", 0 } } },
            { 1, new Dictionary<string, object?> { { "TestValue", 42 } } },
        };

        var setting = new SettingDefinition
        {
            Id = "test",
            Name = "Test Setting",
            Description = "Test",
            InputType = InputType.Selection,
            CustomProperties = new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object> { { CustomPropertyKeys.ValueMappings, mappings } }),
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKLM\Test",
                    ValueName = "TestValue",
                    ValueType = RegistryValueKind.DWord,
                    DefaultValue = 42,
                },
            },
        };

        // No raw values provided for "TestValue"
        var rawValues = new Dictionary<string, object?>();

        var result = _sut.ResolveRawValuesToIndex(setting, rawValues);

        // DefaultValue of 42 should match index 1
        result.Should().Be(1);
    }

    #endregion

    #region ResolveIndexToRawValues

    [Fact]
    public void ResolveIndexToRawValues_MapsIndexToCorrectRawValues()
    {
        var mappings = new Dictionary<int, Dictionary<string, object?>>
        {
            { 0, new Dictionary<string, object?> { { "ValueA", 10 }, { "ValueB", 20 } } },
            { 1, new Dictionary<string, object?> { { "ValueA", 30 }, { "ValueB", 40 } } },
        };
        var setting = CreateSelectionSetting("test", mappings);

        var result = _sut.ResolveIndexToRawValues(setting, 1);

        result.Should().ContainKey("ValueA").WhoseValue.Should().Be(30);
        result.Should().ContainKey("ValueB").WhoseValue.Should().Be(40);
    }

    [Fact]
    public void ResolveIndexToRawValues_UnknownIndex_ReturnsEmptyDictionary()
    {
        var mappings = new Dictionary<int, Dictionary<string, object?>>
        {
            { 0, new Dictionary<string, object?> { { "ValueA", 10 } } },
        };
        var setting = CreateSelectionSetting("test", mappings);

        var result = _sut.ResolveIndexToRawValues(setting, 99);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ResolveIndexToRawValues_NoMappingsProperty_ReturnsEmptyDictionary()
    {
        var setting = CreateBasicSetting("test");

        var result = _sut.ResolveIndexToRawValues(setting, 0);

        result.Should().BeEmpty();
    }

    #endregion

    #region GetIndexFromDisplayName

    [Fact]
    public void GetIndexFromDisplayName_KnownName_ReturnsCorrectIndex()
    {
        var displayNames = new[] { "Off", "Low", "Medium", "High" };
        var setting = CreateSettingWithDisplayNames("test", displayNames);

        var result = _sut.GetIndexFromDisplayName(setting, "Medium");

        result.Should().Be(2);
    }

    [Fact]
    public void GetIndexFromDisplayName_CaseInsensitiveMatch_ReturnsCorrectIndex()
    {
        var displayNames = new[] { "Off", "Low", "Medium", "High" };
        var setting = CreateSettingWithDisplayNames("test", displayNames);

        var result = _sut.GetIndexFromDisplayName(setting, "medium");

        result.Should().Be(2);
    }

    [Fact]
    public void GetIndexFromDisplayName_UnknownName_ReturnsZero()
    {
        var displayNames = new[] { "Off", "Low", "Medium", "High" };
        var setting = CreateSettingWithDisplayNames("test", displayNames);

        var result = _sut.GetIndexFromDisplayName(setting, "Ultra");

        result.Should().Be(0);
    }

    [Fact]
    public void GetIndexFromDisplayName_NoDisplayNamesProperty_ReturnsZero()
    {
        var setting = CreateBasicSetting("test");

        var result = _sut.GetIndexFromDisplayName(setting, "Anything");

        result.Should().Be(0);
    }

    [Fact]
    public void GetIndexFromDisplayName_FirstElement_ReturnsZero()
    {
        var displayNames = new[] { "Off", "On" };
        var setting = CreateSettingWithDisplayNames("test", displayNames);

        var result = _sut.GetIndexFromDisplayName(setting, "Off");

        result.Should().Be(0);
    }

    #endregion

    #region ResolveCurrentValueAsync

    [Fact]
    public async Task ResolveCurrentValueAsync_WithSelectionAndValueMappings_ReturnsResolvedIndex()
    {
        var mappings = new Dictionary<int, Dictionary<string, object?>>
        {
            { 0, new Dictionary<string, object?> { { "TestValue", 0 } } },
            { 1, new Dictionary<string, object?> { { "TestValue", 1 } } },
        };
        var setting = CreateSelectionSettingWithRegistry("test", mappings, "TestValue");

        var existingRawValues = new Dictionary<string, object?> { { "TestValue", 1 } };

        var result = await _sut.ResolveCurrentValueAsync(setting, existingRawValues);

        result.Should().Be(1);
    }

    [Fact]
    public async Task ResolveCurrentValueAsync_WithRegistrySettingsOnly_ReturnsFirstRawValue()
    {
        var setting = new SettingDefinition
        {
            Id = "test",
            Name = "Test Setting",
            Description = "Test",
            InputType = InputType.Toggle,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKLM\Test",
                    ValueName = "TestValue",
                    ValueType = RegistryValueKind.DWord,
                },
            },
        };

        var existingRawValues = new Dictionary<string, object?> { { "TestValue", 42 } };

        var result = await _sut.ResolveCurrentValueAsync(setting, existingRawValues);

        result.Should().Be(42);
    }

    [Fact]
    public async Task ResolveCurrentValueAsync_WithNoExistingValues_QueriesDiscoveryService()
    {
        var setting = new SettingDefinition
        {
            Id = "test",
            Name = "Test Setting",
            Description = "Test",
            InputType = InputType.Toggle,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKLM\Test",
                    ValueName = "TestValue",
                    ValueType = RegistryValueKind.DWord,
                },
            },
        };

        var discoveredValues = new Dictionary<string, Dictionary<string, object?>>
        {
            {
                "test", new Dictionary<string, object?> { { "TestValue", 7 } }
            },
        };

        _discoveryService
            .Setup(d => d.GetRawSettingsValuesAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .ReturnsAsync(discoveredValues);

        var result = await _sut.ResolveCurrentValueAsync(setting);

        result.Should().Be(7);
        _discoveryService.Verify(
            d => d.GetRawSettingsValuesAsync(It.IsAny<IEnumerable<SettingDefinition>>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveCurrentValueAsync_WithScheduledTaskSettings_ReturnsTaskEnabled()
    {
        var setting = new SettingDefinition
        {
            Id = "test",
            Name = "Test Setting",
            Description = "Test",
            InputType = InputType.Toggle,
            ScheduledTaskSettings = new[]
            {
                new ScheduledTaskSetting
                {
                    TaskPath = @"\Microsoft\Windows\Test",
                },
            },
        };

        var existingRawValues = new Dictionary<string, object?> { { "ScheduledTaskEnabled", true } };

        var result = await _sut.ResolveCurrentValueAsync(setting, existingRawValues);

        result.Should().Be(true);
    }

    [Fact]
    public async Task ResolveCurrentValueAsync_WithNoSettings_ReturnsNull()
    {
        var setting = CreateBasicSetting("test");
        var existingRawValues = new Dictionary<string, object?>();

        var result = await _sut.ResolveCurrentValueAsync(setting, existingRawValues);

        result.Should().BeNull();
    }

    #endregion

    #region Helpers

    private static SettingDefinition CreateBasicSetting(string id)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = $"Setting {id}",
            Description = $"Description for {id}",
        };
    }

    private static SettingDefinition CreateSelectionSetting(
        string id,
        Dictionary<int, Dictionary<string, object?>> mappings)
    {
        var customProps = new Dictionary<string, object>
        {
            { CustomPropertyKeys.ValueMappings, mappings },
        };

        return new SettingDefinition
        {
            Id = id,
            Name = $"Setting {id}",
            Description = $"Description for {id}",
            InputType = InputType.Selection,
            CustomProperties = new ReadOnlyDictionary<string, object>(customProps),
        };
    }

    private static SettingDefinition CreateSelectionSettingWithRegistry(
        string id,
        Dictionary<int, Dictionary<string, object?>> mappings,
        string valueName)
    {
        var customProps = new Dictionary<string, object>
        {
            { CustomPropertyKeys.ValueMappings, mappings },
        };

        return new SettingDefinition
        {
            Id = id,
            Name = $"Setting {id}",
            Description = $"Description for {id}",
            InputType = InputType.Selection,
            CustomProperties = new ReadOnlyDictionary<string, object>(customProps),
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKLM\Test",
                    ValueName = valueName,
                    ValueType = RegistryValueKind.DWord,
                },
            },
        };
    }

    private static SettingDefinition CreateSettingWithDisplayNames(string id, string[] displayNames)
    {
        var customProps = new Dictionary<string, object>
        {
            { CustomPropertyKeys.ComboBoxDisplayNames, displayNames },
        };

        return new SettingDefinition
        {
            Id = id,
            Name = $"Setting {id}",
            Description = $"Description for {id}",
            InputType = InputType.Selection,
            CustomProperties = new ReadOnlyDictionary<string, object>(customProps),
        };
    }

    #endregion
}

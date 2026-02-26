using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ComboBoxSetupServiceTests
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IComboBoxResolver> _mockResolver = new();
    private readonly Mock<IPowerPlanComboBoxService> _mockPowerPlan = new();
    private readonly Mock<ISystemSettingsDiscoveryService> _mockDiscovery = new();
    private readonly ComboBoxSetupService _sut;

    public ComboBoxSetupServiceTests()
    {
        _sut = new ComboBoxSetupService(
            _mockLog.Object,
            _mockResolver.Object,
            _mockPowerPlan.Object,
            _mockDiscovery.Object);
    }

    private static SettingDefinition CreateSelectionSetting(
        string id,
        string[]? displayNames = null,
        Dictionary<int, Dictionary<string, object?>>? valueMappings = null)
    {
        var customProps = new Dictionary<string, object>();
        if (displayNames != null)
        {
            customProps[CustomPropertyKeys.ComboBoxDisplayNames] = displayNames;
        }
        if (valueMappings != null)
        {
            customProps[CustomPropertyKeys.ValueMappings] = valueMappings;
        }

        return new SettingDefinition
        {
            Id = id,
            Name = $"Setting {id}",
            Description = $"Description for {id}",
            InputType = InputType.Selection,
            CustomProperties = new ReadOnlyDictionary<string, object>(customProps),
        };
    }

    private static SettingDefinition CreateToggleSetting(string id) => new()
    {
        Id = id,
        Name = $"Setting {id}",
        Description = $"Description for {id}",
        InputType = InputType.Toggle,
    };

    // ── SetupComboBoxOptionsAsync ──

    [Fact]
    public async Task SetupComboBoxOptionsAsync_StandardSelection_ReturnsOptionsWithCorrectIndex()
    {
        // Arrange
        var displayNames = new[] { "Low", "Medium", "High" };
        var valueMappings = new Dictionary<int, Dictionary<string, object?>>
        {
            { 0, new Dictionary<string, object?> { { "Quality", 1 } } },
            { 1, new Dictionary<string, object?> { { "Quality", 2 } } },
            { 2, new Dictionary<string, object?> { { "Quality", 3 } } },
        };
        var setting = CreateSelectionSetting("quality-level", displayNames, valueMappings);

        // currentValue is an int index, so resolver/discovery are not called
        int currentValue = 1;

        // Act
        var result = await _sut.SetupComboBoxOptionsAsync(setting, currentValue);

        // Assert
        result.Success.Should().BeTrue();
        result.Options.Should().HaveCount(3);
        result.Options[0].DisplayText.Should().Be("Low");
        result.Options[1].DisplayText.Should().Be("Medium");
        result.Options[2].DisplayText.Should().Be("High");
        result.SelectedValue.Should().Be(1);
    }

    [Fact]
    public async Task SetupComboBoxOptionsAsync_PowerPlanSetting_DelegatesToPowerPlanService()
    {
        // Arrange
        var setting = CreateSelectionSetting("power-plan-selection");
        var expectedResult = new ComboBoxSetupResult
        {
            Success = true,
            SelectedValue = 2,
        };
        expectedResult.Options.Add(new ComboBoxOption("Balanced", 0));
        expectedResult.Options.Add(new ComboBoxOption("High Performance", 1));
        expectedResult.Options.Add(new ComboBoxOption("Ultimate", 2));

        _mockPowerPlan
            .Setup(p => p.SetupPowerPlanComboBoxAsync(setting, 2))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _sut.SetupComboBoxOptionsAsync(setting, 2);

        // Assert
        result.Should().BeSameAs(expectedResult);
        _mockPowerPlan.Verify(
            p => p.SetupPowerPlanComboBoxAsync(setting, 2), Times.Once);
    }

    [Fact]
    public async Task SetupComboBoxOptionsAsync_CurrentValueResolvesCorrectIndex()
    {
        // Arrange -- currentValue is not an int, so the service calls discovery + resolver
        var displayNames = new[] { "Off", "On" };
        var valueMappings = new Dictionary<int, Dictionary<string, object?>>
        {
            { 0, new Dictionary<string, object?> { { "Enabled", 0 } } },
            { 1, new Dictionary<string, object?> { { "Enabled", 1 } } },
        };
        var setting = CreateSelectionSetting("toggle-combo", displayNames, valueMappings);

        var rawSettingValues = new Dictionary<string, object?> { { "Enabled", 1 } };
        var discoveryResult = new Dictionary<string, Dictionary<string, object?>>
        {
            { "toggle-combo", rawSettingValues },
        };
        _mockDiscovery
            .Setup(d => d.GetRawSettingsValuesAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .ReturnsAsync(discoveryResult);
        _mockResolver
            .Setup(r => r.ResolveRawValuesToIndex(setting, rawSettingValues))
            .Returns(1);

        // Pass null as currentValue to trigger the discovery path
        object? currentValue = null;

        // Act
        var result = await _sut.SetupComboBoxOptionsAsync(setting, currentValue);

        // Assert
        result.Success.Should().BeTrue();
        result.SelectedValue.Should().Be(1);
        result.Options.Should().HaveCount(2);
        result.Options[0].DisplayText.Should().Be("Off");
        result.Options[1].DisplayText.Should().Be("On");
        _mockDiscovery.Verify(
            d => d.GetRawSettingsValuesAsync(It.IsAny<IEnumerable<SettingDefinition>>()), Times.Once);
        _mockResolver.Verify(
            r => r.ResolveRawValuesToIndex(setting, rawSettingValues), Times.Once);
    }

    [Fact]
    public async Task SetupComboBoxOptionsAsync_NoOptions_ReturnsErrorResult()
    {
        // Arrange -- Selection type but no custom properties (empty dictionary)
        var setting = new SettingDefinition
        {
            Id = "empty-combo",
            Name = "Empty ComboBox",
            Description = "No options",
            InputType = InputType.Selection,
        };

        // Act
        var result = await _sut.SetupComboBoxOptionsAsync(setting, 0);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty-combo");
        result.Options.Should().BeEmpty();
    }

    [Fact]
    public async Task SetupComboBoxOptionsAsync_NonSelectionInputType_ReturnsError()
    {
        // Arrange
        var setting = CreateToggleSetting("not-a-combo");

        // Act
        var result = await _sut.SetupComboBoxOptionsAsync(setting, null);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not a ComboBox");
    }

    // ── ResolveIndexFromRawValuesAsync ──

    [Fact]
    public async Task ResolveIndexFromRawValuesAsync_WithMatchingValues_ReturnsCorrectIndex()
    {
        // Arrange
        var setting = CreateSelectionSetting("resolve-test");
        var rawValues = new Dictionary<string, object?> { { "Key1", 42 } };
        _mockResolver
            .Setup(r => r.ResolveRawValuesToIndex(setting, rawValues))
            .Returns(2);

        // Act
        var index = await _sut.ResolveIndexFromRawValuesAsync(setting, rawValues);

        // Assert
        index.Should().Be(2);
        _mockResolver.Verify(
            r => r.ResolveRawValuesToIndex(setting, rawValues), Times.Once);
    }

    [Fact]
    public async Task ResolveIndexFromRawValuesAsync_NoMatch_ResolverReturnsDefault()
    {
        // Arrange
        var setting = CreateSelectionSetting("no-match");
        var rawValues = new Dictionary<string, object?> { { "Key1", 999 } };
        _mockResolver
            .Setup(r => r.ResolveRawValuesToIndex(setting, rawValues))
            .Returns(0);

        // Act
        var index = await _sut.ResolveIndexFromRawValuesAsync(setting, rawValues);

        // Assert
        index.Should().Be(0);
    }

    [Fact]
    public async Task ResolveIndexFromRawValuesAsync_PowerPlan_DelegatesToPowerPlanService()
    {
        // Arrange
        var setting = CreateSelectionSetting("power-plan-selection");
        var rawValues = new Dictionary<string, object?> { { "ActivePlan", "guid" } };
        _mockPowerPlan
            .Setup(p => p.ResolveIndexFromRawValuesAsync(setting, rawValues))
            .ReturnsAsync(3);

        // Act
        var index = await _sut.ResolveIndexFromRawValuesAsync(setting, rawValues);

        // Assert
        index.Should().Be(3);
        _mockPowerPlan.Verify(
            p => p.ResolveIndexFromRawValuesAsync(setting, rawValues), Times.Once);
        _mockResolver.Verify(
            r => r.ResolveRawValuesToIndex(It.IsAny<SettingDefinition>(), It.IsAny<Dictionary<string, object?>>()), Times.Never);
    }

    [Fact]
    public async Task ResolveIndexFromRawValuesAsync_ResolverThrows_ReturnsZeroAndLogsWarning()
    {
        // Arrange
        var setting = CreateSelectionSetting("throw-test");
        var rawValues = new Dictionary<string, object?>();
        _mockResolver
            .Setup(r => r.ResolveRawValuesToIndex(setting, rawValues))
            .Throws(new InvalidOperationException("Bad data"));

        // Act
        var index = await _sut.ResolveIndexFromRawValuesAsync(setting, rawValues);

        // Assert
        index.Should().Be(0);
        _mockLog.Verify(
            l => l.Log(LogLevel.Warning,
                It.Is<string>(s => s.Contains("throw-test") && s.Contains("Bad data")),
                It.IsAny<Exception?>()),
            Times.Once);
    }
}

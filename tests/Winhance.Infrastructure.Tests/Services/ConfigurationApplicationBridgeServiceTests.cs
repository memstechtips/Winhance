using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ConfigurationApplicationBridgeServiceTests
{
    private readonly Mock<ISettingApplicationService> _mockSettingApp = new();
    private readonly Mock<ICompatibleSettingsRegistry> _mockRegistry = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly ConfigurationApplicationBridgeService _service;

    public ConfigurationApplicationBridgeServiceTests()
    {
        _service = new ConfigurationApplicationBridgeService(
            _mockSettingApp.Object,
            _mockRegistry.Object,
            _mockLog.Object);
    }

    private static SettingDefinition CreateSetting(string id, InputType inputType = InputType.Toggle, bool requiresConfirmation = false) => new()
    {
        Id = id,
        Name = $"Setting {id}",
        Description = $"Description for {id}",
        InputType = inputType,
        RequiresConfirmation = requiresConfirmation,
    };

    private static ConfigurationItem CreateItem(string id, bool? isSelected = true) => new()
    {
        Id = id,
        Name = $"Item {id}",
        IsSelected = isSelected,
    };

    private void SetupRegistryWithSettings(params SettingDefinition[] settings)
    {
        var dict = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            ["TestFeature"] = settings
        };
        _mockRegistry
            .Setup(x => x.GetAllFilteredSettings())
            .Returns(dict);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_NullSection_ReturnsFalse()
    {
        // Act
        var result = await _service.ApplyConfigurationSectionAsync(null!, "TestSection");

        // Assert
        result.Should().BeFalse();
        _mockLog.Verify(
            x => x.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("empty or null")), null),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_EmptySection_ReturnsFalse()
    {
        // Arrange
        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>()
        };

        // Act
        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        // Assert
        result.Should().BeFalse();
        _mockLog.Verify(
            x => x.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("empty or null")), null),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_SectionWithItems_AppliesEach()
    {
        // Arrange
        var setting1 = CreateSetting("setting-1");
        var setting2 = CreateSetting("setting-2");
        SetupRegistryWithSettings(setting1, setting2);

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                CreateItem("setting-1"),
                CreateItem("setting-2"),
            }
        };

        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        // Assert
        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "setting-1")),
            Times.Once);
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "setting-2")),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_ItemFails_ContinuesWithOthers()
    {
        // Arrange
        var setting1 = CreateSetting("setting-1");
        var setting2 = CreateSetting("setting-2");
        SetupRegistryWithSettings(setting1, setting2);

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                CreateItem("setting-1"),
                CreateItem("setting-2"),
            }
        };

        // First setting throws, second succeeds
        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "setting-1")))
            .ThrowsAsync(new Exception("Apply failed"));

        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "setting-2")))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        // Assert - returns false because one failed, but both were attempted
        result.Should().BeFalse();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "setting-2")),
            Times.Once);
        _mockLog.Verify(
            x => x.Log(LogLevel.Error, It.Is<string>(s => s.Contains("Failed to apply")), null),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_ConfirmationHandlerInvoked_ForConfirmationItems()
    {
        // Arrange
        var setting = CreateSetting("confirm-setting", requiresConfirmation: true);
        SetupRegistryWithSettings(setting);

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                CreateItem("confirm-setting", isSelected: true),
            }
        };

        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        bool handlerCalled = false;
        Func<string, object?, SettingDefinition, Task<(bool confirmed, bool checkboxResult)>> handler =
            (id, value, def) =>
            {
                handlerCalled = true;
                id.Should().Be("confirm-setting");
                def.RequiresConfirmation.Should().BeTrue();
                return Task.FromResult((confirmed: true, checkboxResult: false));
            };

        // Act
        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection", handler);

        // Assert
        result.Should().BeTrue();
        handlerCalled.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "confirm-setting")),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_ConfirmationDenied_SkipsSetting()
    {
        // Arrange
        var setting = CreateSetting("confirm-setting", requiresConfirmation: true);
        SetupRegistryWithSettings(setting);

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                CreateItem("confirm-setting", isSelected: true),
            }
        };

        Func<string, object?, SettingDefinition, Task<(bool confirmed, bool checkboxResult)>> handler =
            (id, value, def) => Task.FromResult((confirmed: false, checkboxResult: false));

        // Act
        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection", handler);

        // Assert - counts as "applied" (skipped by user) so overall succeeds
        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()), Times.Never);
        _mockLog.Verify(
            x => x.Log(LogLevel.Info, It.Is<string>(s => s.Contains("User skipped")), null),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_NoConfirmationHandler_SkipsConfirmation()
    {
        // Arrange - setting requires confirmation but no handler is provided
        var setting = CreateSetting("confirm-setting", requiresConfirmation: true);
        SetupRegistryWithSettings(setting);

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                CreateItem("confirm-setting", isSelected: true),
            }
        };

        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act - no confirmationHandler passed (null)
        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection", null);

        // Assert - setting is applied directly without confirmation
        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "confirm-setting")),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_ItemWithEmptyId_IsSkippedDuringWaveBuilding()
    {
        // Arrange
        var setting = CreateSetting("good-setting");
        SetupRegistryWithSettings(setting);

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                new() { Id = "", Name = "Empty ID item", IsSelected = true },
                CreateItem("good-setting"),
            }
        };

        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        // Assert - empty ID item is filtered out during wave building, good-setting is applied
        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "good-setting")),
            Times.Once);
        // The empty ID item was silently filtered out during wave building
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "")),
            Times.Never);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_SettingNotInRegistry_SkippedAsOsIncompatible()
    {
        // Arrange - registry has no settings matching the item
        _mockRegistry
            .Setup(x => x.GetAllFilteredSettings())
            .Returns(new Dictionary<string, IEnumerable<SettingDefinition>>());

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                CreateItem("unknown-setting"),
            }
        };

        // Act
        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        // Assert - skipped items don't count as failures
        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()), Times.Never);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_ActionSetting_AppliesWithCommandString()
    {
        // Arrange
        var setting = CreateSetting("action-setting", inputType: InputType.Action) with
        {
            ActionCommand = "run-action"
        };
        SetupRegistryWithSettings(setting);

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                CreateItem("action-setting", isSelected: true),
            }
        };

        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        // Assert
        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
                r.SettingId == "action-setting" &&
                r.CommandString == "run-action" &&
                r.Enable == false &&
                r.SkipValuePrerequisites == true)),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_ActionSettingNotSelected_SkipsExecution()
    {
        // Arrange
        var setting = CreateSetting("action-setting", inputType: InputType.Action) with
        {
            ActionCommand = "run-action"
        };
        SetupRegistryWithSettings(setting);

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                CreateItem("action-setting", isSelected: false),
            }
        };

        // Act
        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        // Assert - Action settings with IsSelected=false skip the action branch entirely
        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()), Times.Never);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_SelectionSetting_PassesSelectedIndex()
    {
        // Arrange
        var setting = CreateSetting("select-setting", inputType: InputType.Selection);
        SetupRegistryWithSettings(setting);

        var item = new ConfigurationItem
        {
            Id = "select-setting",
            Name = "Select Setting",
            IsSelected = true,
            InputType = InputType.Selection,
            SelectedIndex = 2,
        };

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem> { item }
        };

        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        // Assert
        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
                r.SettingId == "select-setting" &&
                r.Value != null &&
                (int)r.Value == 2)),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_ConfirmationWithCheckboxResult_PassesCheckboxResult()
    {
        // Arrange
        var setting = CreateSetting("checkbox-setting", requiresConfirmation: true);
        SetupRegistryWithSettings(setting);

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                CreateItem("checkbox-setting", isSelected: true),
            }
        };

        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        Func<string, object?, SettingDefinition, Task<(bool confirmed, bool checkboxResult)>> handler =
            (id, value, def) => Task.FromResult((confirmed: true, checkboxResult: true));

        // Act
        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection", handler);

        // Assert
        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
                r.SettingId == "checkbox-setting" &&
                r.CheckboxResult == true)),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_AllItemsFail_ReturnsFalse()
    {
        // Arrange
        var setting1 = CreateSetting("fail-1");
        var setting2 = CreateSetting("fail-2");
        SetupRegistryWithSettings(setting1, setting2);

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                CreateItem("fail-1"),
                CreateItem("fail-2"),
            }
        };

        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ThrowsAsync(new Exception("Boom"));

        // Act
        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_DependentSettings_ProcessedInWaves()
    {
        // Arrange - setting-2 depends on setting-1
        var setting1 = CreateSetting("setting-1");
        var setting2 = CreateSetting("setting-2") with
        {
            Dependencies = new List<SettingDependency>
            {
                new()
                {
                    DependencyType = SettingDependencyType.RequiresEnabled,
                    DependentSettingId = "setting-2",
                    RequiredSettingId = "setting-1",
                }
            }
        };
        SetupRegistryWithSettings(setting1, setting2);

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                CreateItem("setting-1"),
                CreateItem("setting-2"),
            }
        };

        var applyOrder = new List<string>();
        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .Callback<ApplySettingRequest>(r => applyOrder.Add(r.SettingId))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        // Assert
        result.Should().BeTrue();
        applyOrder.Should().ContainInOrder("setting-1", "setting-2");
        _mockLog.Verify(
            x => x.Log(LogLevel.Info, It.Is<string>(s => s.Contains("parallel wave(s)")), null),
            Times.Once);
    }
}

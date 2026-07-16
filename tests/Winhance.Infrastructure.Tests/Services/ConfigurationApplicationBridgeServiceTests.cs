using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ConfigurationApplicationBridgeServiceTests
{
    private readonly Mock<ISettingApplicationService> _mockSettingApp = new();
    private readonly Mock<ICatalogSettingsRegistry> _mockRegistry = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly ConfigImportState _importState = new();
    private readonly ConfigurationApplicationBridgeService _service;

    public ConfigurationApplicationBridgeServiceTests()
    {
        _service = new ConfigurationApplicationBridgeService(
            _mockSettingApp.Object,
            _mockRegistry.Object,
            _mockLog.Object,
            _importState);
    }

    // Synthetic catalog Settings: since Slice 6 the bridge reads the registry-returned Setting directly
    // (no live-catalog re-pairing), so tests construct exactly the shape they exercise - the derived
    // Control comes from the shape (2 Enabled/Disabled states = Toggle, 0 states = Action, other states =
    // Selection, Numeric = Slider).
    private static Setting CreateSetting(string id, ControlKind kind = ControlKind.Toggle, bool requiresConfirmation = false) => new()
    {
        Id = id,
        Display = new Display { Name = $"Setting {id}", Description = $"Description for {id}" },
        States = kind switch
        {
            ControlKind.Toggle => new[]
            {
                new SettingState { Label = "Enabled" },
                new SettingState { Label = "Disabled" },
            },
            ControlKind.Selection => new[]
            {
                new SettingState { Label = "Option A" },
                new SettingState { Label = "Option B" },
                new SettingState { Label = "Option C" },
            },
            _ => System.Array.Empty<SettingState>(),
        },
        Numeric = kind == ControlKind.Slider ? new Numeric { Min = 0, Max = 3600 } : null,
        Apply = new ApplyBehavior { RequiresConfirmation = requiresConfirmation },
    };

    private static Setting CreatePowerCfgNumericRangeSetting(string id, string units) => new()
    {
        Id = id,
        Display = new Display { Name = $"Setting {id}", Description = $"Description for {id}" },
        Numeric = new Numeric { Min = 0, Max = 3600, Units = units },
        Targets = new Target[]
        {
            new PowerCfgTarget(
                "Power",
                "00000000-0000-0000-0000-000000000000",
                "00000000-0000-0000-0000-000000000000",
                PowerModeSupport.Separate),
        },
    };

    private static ConfigurationItem CreateItem(string id, bool? isSelected = true) => new()
    {
        Id = id,
        Name = $"Item {id}",
        IsSelected = isSelected,
    };

    private void SetupRegistryWithSettings(params Setting[] settings)
    {
        var byId = settings.ToDictionary(s => s.Id);
        _mockRegistry
            .Setup(x => x.GetById(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns((string id, bool _) => byId.TryGetValue(id, out var s) ? s : null);
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
        Func<string, object?, Task<(bool confirmed, bool checkboxResult)>> handler =
            (id, value) =>
            {
                handlerCalled = true;
                id.Should().Be("confirm-setting");
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

        Func<string, object?, Task<(bool confirmed, bool checkboxResult)>> handler =
            (id, value) => Task.FromResult((confirmed: false, checkboxResult: false));

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
        // Arrange - the catalog registry resolves no Setting for the item's id
        _mockRegistry
            .Setup(x => x.GetById(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns((Setting?)null);

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
    public async Task ApplyConfigurationSectionAsync_SelectedActionSetting_AppliesViaCatalogPath()
    {
        // Arrange
        var setting = CreateSetting("act-sel", kind: ControlKind.Action);
        SetupRegistryWithSettings(setting);

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                CreateItem("act-sel", isSelected: true),
            }
        };

        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        // Assert — selected Action setting routes through catalog path with Enable = true
        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
                r.SettingId == "act-sel" && r.Enable == true)),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_UnselectedActionSetting_IsSkipped()
    {
        // Arrange
        var setting = CreateSetting("act-sel", kind: ControlKind.Action);
        SetupRegistryWithSettings(setting);

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                CreateItem("act-sel", isSelected: false),
            }
        };

        // Act
        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        // Assert — unselected Action setting is skipped entirely (no reverse semantic)
        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()), Times.Never);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_SelectionSetting_PassesSelectedIndex()
    {
        // Arrange
        var setting = CreateSetting("select-setting", kind: ControlKind.Selection);
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

        Func<string, object?, Task<(bool confirmed, bool checkboxResult)>> handler =
            (id, value) => Task.FromResult((confirmed: true, checkboxResult: true));

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
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Links = new[] { new Link("setting-1", LinkKind.Requires, "Enabled") },
                },
                new SettingState { Label = "Disabled" },
            },
        };
        SetupRegistryWithSettings(setting1, setting2);

        // The dependent is listed BEFORE its prerequisite: BuildDependencyWaves adds a prerequisite
        // to processedIds within the same pass, so an in-order prerequisite would coalesce both items
        // into ONE wave and the mocked synchronous applies would reproduce list order vacuously.
        // Reversed, the Requires-Link is what forces wave 2 AND flips the apply order.
        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                CreateItem("setting-2"),
                CreateItem("setting-1"),
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
        // Pin the wave COUNT: with the Requires-Link ignored both items would share one wave and the
        // mocked synchronous applies would still produce the same order, so ContainInOrder alone is
        // not enough - "2 parallel wave(s)" proves the dependency actually split the waves.
        _mockLog.Verify(
            x => x.Log(LogLevel.Info, It.Is<string>(s => s.Contains("2 parallel wave(s)")), null),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_PowerCfgNumericRange_ConvertsSystemUnitsToDisplay()
    {
        // Arrange - config stores AC/DC values in SYSTEM units (seconds). The PowerCfgApplier
        // converts display->system itself, so the bridge must hand it DISPLAY units.
        // 600 seconds with "Minutes" display units => 10 minutes.
        var setting = CreatePowerCfgNumericRangeSetting("power-harddisk-timeout", units: "Minutes");
        SetupRegistryWithSettings(setting);

        var item = new ConfigurationItem
        {
            Id = "power-harddisk-timeout",
            Name = "Hard disk timeout",
            IsSelected = true,
            InputType = InputType.NumericRange,
            PowerSettings = new Dictionary<string, object>
            {
                ["ACValue"] = 300,
                ["DCValue"] = 600,
            },
        };

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem> { item }
        };

        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        var result = await _service.ApplyConfigurationSectionAsync(section, "Power");

        // Assert - 300s -> 5 min (AC), 600s -> 10 min (DC)
        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
                r.SettingId == "power-harddisk-timeout" &&
                r.Value is Dictionary<string, object?> &&
                Convert.ToInt32(((Dictionary<string, object?>)r.Value!)["ACValue"]) == 5 &&
                Convert.ToInt32(((Dictionary<string, object?>)r.Value!)["DCValue"]) == 10)),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_NonPowerNumericRange_PassesValueUnchanged()
    {
        // Arrange - a Slider setting with NO PowerCfgTarget must not be unit-converted.
        var setting = CreateSetting("plain-numeric", kind: ControlKind.Slider);
        SetupRegistryWithSettings(setting);

        var item = new ConfigurationItem
        {
            Id = "plain-numeric",
            Name = "Plain numeric",
            IsSelected = true,
            InputType = InputType.NumericRange,
            PowerSettings = new Dictionary<string, object>
            {
                ["Value"] = 600,
            },
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

        // Assert - value passes through unchanged (no PowerCfg conversion)
        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
                r.SettingId == "plain-numeric" &&
                r.Value != null &&
                Convert.ToInt32(r.Value) == 600)),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_SectionWithPowerItems_SetsImportSuppliesPowerValues()
    {
        // Arrange - a section carrying an individual PowerCfg item alongside the plan selection
        var powerItemSetting = CreatePowerCfgNumericRangeSetting("power-harddisk-timeout", units: "Minutes");
        var planSetting = CreateSetting("power-plan-selection", kind: ControlKind.Selection);
        SetupRegistryWithSettings(powerItemSetting, planSetting);

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                new()
                {
                    Id = "power-harddisk-timeout",
                    Name = "Hard disk timeout",
                    IsSelected = true,
                    InputType = InputType.NumericRange,
                    PowerSettings = new Dictionary<string, object> { ["ACValue"] = 300, ["DCValue"] = 600 },
                },
                new()
                {
                    Id = "power-plan-selection",
                    Name = "Power plan",
                    IsSelected = true,
                    InputType = InputType.Selection,
                    PowerPlanGuid = "57696e68-616e-6365-506f-776572000000",
                    PowerPlanName = "Winhance Power Plan",
                },
            }
        };

        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        await _service.ApplyConfigurationSectionAsync(section, "Power");

        // Assert
        _importState.ImportSuppliesPowerValues.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_SectionWithoutPowerItems_LeavesImportSuppliesPowerValuesFalse()
    {
        // Arrange - a non-power section: no item carries PowerSettings
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
        await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        // Assert
        _importState.ImportSuppliesPowerValues.Should().BeFalse();
    }
}

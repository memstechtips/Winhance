using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.TestSupport;
using Xunit;

using Winhance.Core.Features.Common.Localization;
namespace Winhance.Infrastructure.Tests.Services;

public class ConfigurationApplicationBridgeServiceTests
{
    private readonly Mock<ISettingApplicationService> _mockSettingApp = new();
    private readonly Mock<ICatalogSettingsRegistry> _mockRegistry = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly ConfigImportState _importState = new();
    private readonly Mock<IFileStore> _mockFileStore = new();
    private readonly ConfigurationApplicationBridgeService _service;

    public ConfigurationApplicationBridgeServiceTests()
    {
        _service = new ConfigurationApplicationBridgeService(
            _mockSettingApp.Object,
            _mockRegistry.Object,
            _mockLog.Object,
            _importState,
            _mockFileStore.Object);
    }

    // Synthetic catalog Settings: the bridge reads the registry-returned Setting directly, so tests
    // construct exactly the shape they exercise - the derived Control comes from the shape (2
    // Enabled/Disabled states = Toggle, 2 Checked/Unchecked = CheckBox, 0 states = Action, other states = Selection,
    // Numeric = Slider).
    private static Setting CreateSetting(string id, ControlKind kind = ControlKind.Toggle, bool requiresConfirmation = false) => new()
    {
        Id = id,
        Display = new Display { Name = TestKeys.Of($"Setting {id}"), Description = TestKeys.Of($"Description for {id}")},
        States = kind switch
        {
            ControlKind.Toggle => new[]
            {
                new SettingState { Label = LocKey.Common.Enabled },
                new SettingState { Label = LocKey.Common.Disabled },
            },
            ControlKind.CheckBox => new[]
            {
                new SettingState { Label = LocKey.Common.Checked },
                new SettingState { Label = LocKey.Common.Unchecked },
            },
            ControlKind.Selection => new[]
            {
                new SettingState { Label = TestKeys.Of("Option A") },
                new SettingState { Label = TestKeys.Of("Option B") },
                new SettingState { Label = TestKeys.Of("Option C") },
            },
            _ => System.Array.Empty<SettingState>(),
        },
        Numeric = kind == ControlKind.Slider ? new Numeric { Min = 0, Max = 3600 } : null,
        Apply = new ApplyBehavior { RequiresConfirmation = requiresConfirmation },
    };

    private static Setting CreatePowerCfgNumericRangeSetting(string id, string units) => new()
    {
        Id = id,
        Display = new Display { Name = TestKeys.Of($"Setting {id}"), Description = TestKeys.Of($"Description for {id}")},
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
            .Setup(x => x.GetById(It.IsAny<string>(), It.IsAny<CatalogScope>()))
            .Returns<string, CatalogScope>((id, _) => byId.TryGetValue(id, out var s) ? s : null);
    }

    private const string CarriedDestination = @"C:\Windows\Web\Wallpaper\Winhance\mine.jpg";

    private static ConfigurationItem CarryingItem(string id)
    {
        var item = CreateItem(id);
        item.SelectedKey = @"D:\mine.jpg";
        item.SelectedKeyLabel = "mine.jpg";
        item.File = new CarriedFileItem { Destination = CarriedDestination, Base64 = "QUJD" };
        return item;
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_ACarriedFile_LandsOnThisPcBeforeTheChoiceIsApplied()
    {
        var setting = FakeOptionProvider.SettingFor("theme-wallpaper-picture");
        SetupRegistryWithSettings(setting);
        _mockFileStore
            .Setup(s => s.MaterializeAsync(It.IsAny<SelectionSet>()))
            .ReturnsAsync((SelectionSet set) => set with
            {
                Settings = [new SettingChoice(setting.Id, new ChoiceValue.Keyed(CarriedDestination, "mine.jpg"))],
            });
        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var result = await _service.ApplyConfigurationSectionAsync(
            new ConfigSection { Items = [CarryingItem(setting.Id)] }, "TestSection");

        result.Should().BeTrue();
        _mockFileStore.Verify(
            s => s.MaterializeAsync(It.Is<SelectionSet>(set =>
                set.Files.Count == 1 && set.Files[0].Destination == CarriedDestination && set.Files[0].Base64 == "QUJD")),
            Times.Once);
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(
                r => r.SettingId == setting.Id && Equals(r.Value, CarriedDestination))),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_TheSameCarryingItemListedTwice_LandsOneFileAndAppliesIt()
    {
        var setting = FakeOptionProvider.SettingFor("theme-wallpaper-picture");
        SetupRegistryWithSettings(setting);
        _mockFileStore
            .Setup(s => s.MaterializeAsync(It.IsAny<SelectionSet>()))
            .ReturnsAsync((SelectionSet set) => set with
            {
                Settings = [.. set.Settings.Select(choice =>
                    choice with { Value = new ChoiceValue.Keyed(CarriedDestination, "mine.jpg") })],
            });
        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var result = await _service.ApplyConfigurationSectionAsync(
            new ConfigSection { Items = [CarryingItem(setting.Id), CarryingItem(setting.Id)] }, "TestSection");

        result.Should().BeTrue();
        _mockFileStore.Verify(
            s => s.MaterializeAsync(It.Is<SelectionSet>(set => set.Files.Count == 1)),
            Times.Once);
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(
                r => r.SettingId == setting.Id && Equals(r.Value, CarriedDestination))),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_ASectionThatCarriesNoFiles_NeverReachesTheFileStore()
    {
        var setting = CreateSetting("plain-toggle");
        SetupRegistryWithSettings(setting);
        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        await _service.ApplyConfigurationSectionAsync(
            new ConfigSection { Items = [CreateItem(setting.Id)] }, "TestSection");

        _mockFileStore.Verify(s => s.MaterializeAsync(It.IsAny<SelectionSet>()), Times.Never);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_NullSection_ReturnsFalse()
    {
        var result = await _service.ApplyConfigurationSectionAsync(null!, "TestSection");

        result.Should().BeFalse();
        _mockLog.Verify(
            x => x.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("empty or null")), null, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_EmptySection_ReturnsFalse()
    {
        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>()
        };

        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        result.Should().BeFalse();
        _mockLog.Verify(
            x => x.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("empty or null")), null, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_SectionWithItems_AppliesEach()
    {
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

        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

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
            .Setup(x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "setting-1")))
            .ThrowsAsync(new Exception("Apply failed"));

        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "setting-2")))
            .ReturnsAsync(OperationResult.Succeeded());

        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        result.Should().BeFalse();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "setting-2")),
            Times.Once);
        _mockLog.Verify(
            x => x.Log(LogLevel.Error, It.Is<string>(s => s.Contains("Failed to apply")), null, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_ConfirmationHandlerInvoked_ForConfirmationItems()
    {
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

        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection", handler);

        result.Should().BeTrue();
        handlerCalled.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "confirm-setting")),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_ConfirmationDenied_SkipsSetting()
    {
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

        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection", handler);

        // A user-skipped setting counts as "applied", so overall succeeds
        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()), Times.Never);
        _mockLog.Verify(
            x => x.Log(LogLevel.Info, It.Is<string>(s => s.Contains("User skipped")), null, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_NoConfirmationHandler_SkipsConfirmation()
    {
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

        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection", null);

        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "confirm-setting")),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_ItemWithEmptyId_IsSkippedDuringWaveBuilding()
    {
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

        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "good-setting")),
            Times.Once);
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "")),
            Times.Never);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_SettingNotInRegistry_SkippedAsOsIncompatible()
    {
        _mockRegistry
            .Setup(x => x.GetById(It.IsAny<string>(), It.IsAny<CatalogScope>()))
            .Returns((Setting?)null);

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                CreateItem("unknown-setting"),
            }
        };

        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        // Skipped items don't count as failures
        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()), Times.Never);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_SelectedActionSetting_AppliesViaCatalogPath()
    {
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

        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
                r.SettingId == "act-sel" && r.Enable == true)),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_UnselectedActionSetting_IsSkipped()
    {
        var setting = CreateSetting("act-sel", kind: ControlKind.Action);
        SetupRegistryWithSettings(setting);

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                CreateItem("act-sel", isSelected: false),
            }
        };

        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        // An unselected Action is skipped entirely: there is no reverse semantic
        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()), Times.Never);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_SelectionSetting_PassesSelectedIndex()
    {
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

        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

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

        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection", handler);

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

        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_DependentSettings_ProcessedInWaves()
    {
        var setting1 = CreateSetting("setting-1");
        var setting2 = CreateSetting("setting-2") with
        {
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("setting-1", LinkKind.Requires, LocKey.Common.Enabled) },
                },
                new SettingState { Label = LocKey.Common.Disabled },
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

        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        result.Should().BeTrue();
        applyOrder.Should().ContainInOrder("setting-1", "setting-2");
        // Pin the wave COUNT: with the Requires-Link ignored both items would share one wave and the
        // mocked synchronous applies would still produce the same order, so ContainInOrder alone is
        // not enough - "2 parallel wave(s)" proves the dependency actually split the waves.
        _mockLog.Verify(
            x => x.Log(LogLevel.Info, It.Is<string>(s => s.Contains("2 parallel wave(s)")), null, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_PowerCfgNumericRange_ConvertsSystemUnitsToDisplay()
    {
        // Config stores AC/DC values in SYSTEM units (seconds). The PowerCfgApplier
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

        var result = await _service.ApplyConfigurationSectionAsync(section, "Power");

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

        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

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

        await _service.ApplyConfigurationSectionAsync(section, "Power");

        _importState.ImportSuppliesPowerValues.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_SectionWithoutPowerItems_LeavesImportSuppliesPowerValuesFalse()
    {
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

        await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        _importState.ImportSuppliesPowerValues.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_ToggleEraSelectionItem_StillDefaultsToIndexZeroWithWarning()
    {
        var setting = CreateSetting("select-setting", kind: ControlKind.Selection);
        SetupRegistryWithSettings(setting);

        var item = new ConfigurationItem
        {
            Id = "select-setting",
            Name = "Select Setting",
            IsSelected = false,
            InputType = InputType.Toggle,
        };

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem> { item }
        };

        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var result = await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        result.Should().BeTrue();
        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
                r.SettingId == "select-setting" &&
                r.Value != null &&
                (int)r.Value == 0)),
            Times.Once);
        _mockLog.Verify(
            x => x.Log(LogLevel.Warning, It.Is<string>(m => m.Contains("defaulting to option index 0")), null, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_CustomStateValuesFromJson_ArePassedAsPlainValues()
    {
        var setting = CreateSetting("select-setting", kind: ControlKind.Selection);
        SetupRegistryWithSettings(setting);

        var item = new ConfigurationItem
        {
            Id = "select-setting",
            Name = "Select Setting",
            InputType = InputType.Selection,
            CustomStateValues = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>("{\"Mode\":7}"),
        };

        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem> { item }
        };

        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        await _service.ApplyConfigurationSectionAsync(section, "TestSection");

        _mockSettingApp.Verify(
            x => x.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
                r.Value is Dictionary<string, object> &&
                ((Dictionary<string, object>)r.Value)["Mode"] is int &&
                (int)((Dictionary<string, object>)r.Value)["Mode"] == 7)),
            Times.Once);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_PowerPlanItem_AppliesTheSchemeGuid()
    {
        const string guid = "57696e68-616e-6365-506f-776572000000";
        var plan = new Setting
        {
            Id = "power-plan-selection",
            Display = new Display { Name = TestKeys.Of("Power plan"), Description = TestKeys.Of("Power plan") },
            Options = new(OptionSource.PowerPlans),
        };
        SetupRegistryWithSettings(plan);
        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                new()
                {
                    Id = "power-plan-selection",
                    Name = "Power Plan",
                    IsSelected = true,
                    InputType = InputType.Selection,
                    SelectedKey = guid,
                    SelectedKeyLabel = "Winhance Power Plan",
                },
            }
        };
        ApplySettingRequest? capturedPlan = null;
        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .Callback<ApplySettingRequest>(r => capturedPlan = r)
            .ReturnsAsync(OperationResult.Succeeded());

        await _service.ApplyConfigurationSectionAsync(section, "Power");

        capturedPlan.Should().NotBeNull();
        capturedPlan!.Value.Should().Be(guid);
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_KeyedItem_AppliesTheKeyItself()
    {
        SetupRegistryWithSettings(FakeOptionProvider.SettingFor());
        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                new()
                {
                    Id = "fake-keyed",
                    Name = "Fake keyed",
                    IsSelected = true,
                    InputType = InputType.Selection,
                    SelectedKey = "beta",
                    SelectedKeyLabel = "Beta zone",
                },
            }
        };
        ApplySettingRequest? captured = null;
        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .Callback<ApplySettingRequest>(r => captured = r)
            .ReturnsAsync(OperationResult.Succeeded());

        await _service.ApplyConfigurationSectionAsync(section, "Region");

        captured.Should().NotBeNull();
        captured!.Value.Should().Be("beta");
    }

    [Fact]
    public async Task ApplyConfigurationSectionAsync_AKeyThisPcRefuses_FailsTheItemAndNamesNothingItself()
    {
        var setting = FakeOptionProvider.SettingFor();
        SetupRegistryWithSettings(setting);
        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                new()
                {
                    Id = setting.Id,
                    Name = "Fake keyed",
                    IsSelected = true,
                    InputType = InputType.Selection,
                    SelectedKey = "delta",
                    SelectedKeyLabel = "Delta zone",
                },
            }
        };
        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Failed("delta is not offered here"));

        var result = await _service.ApplyConfigurationSectionAsync(section, "Region");

        result.Should().BeFalse();
        _importState.TakeNotApplied().Should().BeEmpty();
    }

    [Theory]
    [InlineData(ControlKind.Toggle)]
    [InlineData(ControlKind.Action)]
    public async Task ApplyConfigurationSectionAsync_AnApplyThatReportsFailure_FailsTheItemButIsNotNamedToTheUser(ControlKind kind)
    {
        var setting = CreateSetting("reports-failure", kind);
        SetupRegistryWithSettings(setting);
        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Failed("1/1 apply operation(s) failed"));

        var result = await _service.ApplyConfigurationSectionAsync(
            new ConfigSection { Items = [CreateItem(setting.Id)] }, "TestSection");

        result.Should().BeFalse();
        _importState.TakeNotApplied().Should().BeEmpty();
    }

    private static Setting CreateAlbumSetting() => new()
    {
        Id = "theme-wallpaper-album",
        Display = new Display { Name = TestKeys.Of("Album"), Description = TestKeys.Of("The folder the slideshow plays") },
        Targets = new Target[] { new DesktopSlideshowTarget("album") },
        TextBox = new(new TextRule("^.{1,}$", UpperCase: false, TestKeys.Of("Anything.")), Picker: PickerKind.Folder),
    };

    // An empty box is an answer too (it clears the setting); a null would reach the apply as a shape nothing audits.
    [Theory]
    [InlineData(@"D:\Pictures\Holiday")]
    [InlineData("")]
    public async Task ApplyConfigurationSectionAsync_TextBoxItem_AppliesTheTypedText(string typed)
    {
        SetupRegistryWithSettings(CreateAlbumSetting());
        var section = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                new()
                {
                    Id = "theme-wallpaper-album",
                    Name = "Album",
                    IsSelected = true,
                    InputType = InputType.TextBox,
                    Text = typed,
                },
            }
        };
        ApplySettingRequest? captured = null;
        _mockSettingApp
            .Setup(x => x.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .Callback<ApplySettingRequest>(r => captured = r)
            .ReturnsAsync(OperationResult.Succeeded());

        await _service.ApplyConfigurationSectionAsync(section, "WindowsTheme");

        captured.Should().NotBeNull();
        captured!.Value.Should().Be(typed);
    }
}

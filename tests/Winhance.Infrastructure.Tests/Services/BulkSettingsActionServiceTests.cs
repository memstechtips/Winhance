using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class BulkSettingsActionServiceTests
{
    private readonly Mock<IDomainServiceRouter> _mockRouter = new();
    private readonly Mock<IWindowsVersionService> _mockVersionService = new();
    private readonly Mock<ISettingApplicationService> _mockAppService = new();
    private readonly Mock<IProcessRestartManager> _mockProcessRestartManager = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly BulkSettingsActionService _service;

    public BulkSettingsActionServiceTests()
    {
        _service = new BulkSettingsActionService(
            _mockRouter.Object,
            _mockVersionService.Object,
            _mockAppService.Object,
            _mockProcessRestartManager.Object,
            _mockLog.Object);

        // Default OS setup: Windows 11, build 22621
        _mockVersionService.Setup(v => v.IsWindows11()).Returns(true);
        _mockVersionService.Setup(v => v.GetWindowsBuildNumber()).Returns(22621);

        // Default: ApplySettingAsync succeeds
        _mockAppService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static SettingDefinition CreateToggleSetting(
        string id,
        object? recommendedValue,
        object? defaultValue = null,
        object?[]? enabledValue = null,
        object?[]? disabledValue = null,
        bool isGroupPolicy = false) => new()
    {
        Id = id,
        Name = $"Setting {id}",
        Description = $"Description for {id}",
        InputType = InputType.Toggle,
        RegistrySettings = new[]
        {
            new RegistrySetting
            {
                KeyPath = @"HKLM\Software\Test",
                ValueName = "TestValue",
                ValueType = RegistryValueKind.DWord,
                RecommendedValue = recommendedValue,
                DefaultValue = defaultValue,
                EnabledValue = enabledValue ?? (recommendedValue != null ? [recommendedValue] : null),
                DisabledValue = disabledValue,
                IsGroupPolicy = isGroupPolicy,
            }
        }
    };

    private static SettingDefinition CreateSelectionSetting(
        string id,
        string recommendedOption,
        string? defaultOption,
        Dictionary<string, int> comboBoxOptions)
    {
        // Sort option names alphabetically; their index becomes the selection index.
        var sortedNames = comboBoxOptions.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var options = new List<Winhance.Core.Features.Common.Models.ComboBoxOption>(sortedNames.Length);
        for (int i = 0; i < sortedNames.Length; i++)
        {
            var name = sortedNames[i];
            options.Add(new Winhance.Core.Features.Common.Models.ComboBoxOption
            {
                DisplayName = name,
                IsRecommended = name == recommendedOption,
                IsDefault = defaultOption != null && name == defaultOption,
                ValueMappings = new Dictionary<string, object?> { { "TestValue", comboBoxOptions[name] } },
            });
        }

        return new SettingDefinition
        {
            Id = id,
            Name = $"Setting {id}",
            Description = $"Description for {id}",
            InputType = InputType.Selection,
            ComboBox = new ComboBoxMetadata { Options = options },
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKLM\Software\Test",
                    ValueName = "TestValue",
                    ValueType = RegistryValueKind.DWord,
                    IsPrimary = true,
                    RecommendedValue = null,
                    DefaultValue = null,
                }
            }
        };
    }

    private static SettingDefinition CreateNumericSetting(
        string id,
        object? recommendedValue,
        object? defaultValue = null) => new()
    {
        Id = id,
        Name = $"Setting {id}",
        Description = $"Description for {id}",
        InputType = InputType.NumericRange,
        RegistrySettings = new[]
        {
            new RegistrySetting
            {
                KeyPath = @"HKLM\Software\Test",
                ValueName = "NumericValue",
                ValueType = RegistryValueKind.DWord,
                RecommendedValue = recommendedValue,
                DefaultValue = defaultValue,
            }
        }
    };

    private void SetupDomainWithSettings(
        string settingId,
        IEnumerable<SettingDefinition> settings,
        string domainName = "TestDomain")
    {
        var mockDomain = new Mock<IDomainService>();
        mockDomain.Setup(d => d.DomainName).Returns(domainName);
        mockDomain.Setup(d => d.GetSettingsAsync()).ReturnsAsync(settings);
        _mockRouter.Setup(r => r.GetDomainService(settingId)).Returns(mockDomain.Object);
    }

    // ---------------------------------------------------------------
    // Test 1: ApplyRecommendedAsync applies recommended values to all settings
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyRecommendedAsync_AppliesRecommendedValues_ToAllSettings()
    {
        // Arrange — both fixtures populate EnabledValue and DisabledValue so the unified
        // toggle-state algorithm can map RecommendedValue → Enable.
        var setting1 = CreateToggleSetting("setting-a", recommendedValue: 1,
            enabledValue: [1], disabledValue: [0]);
        var setting2 = CreateToggleSetting("setting-b", recommendedValue: 0,
            enabledValue: [1], disabledValue: [0]);

        SetupDomainWithSettings("setting-a", new[] { setting1 }, "DomainA");
        SetupDomainWithSettings("setting-b", new[] { setting2 }, "DomainB");

        // Act
        var applied = await _service.ApplyRecommendedAsync(new[] { "setting-a", "setting-b" });

        // Assert: bulk Toggle apply mirrors per-card HandleToggleAsync — passes only
        // SettingId + Enable, no Value (apply pipeline derives it from EnabledValue/DisabledValue).
        applied.Should().Be(2);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "setting-a" &&
            r.Enable == true &&
            r.SkipValuePrerequisites == true
        )), Times.Once);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "setting-b" &&
            r.Enable == false &&
            r.SkipValuePrerequisites == true
        )), Times.Once);
    }

    // ---------------------------------------------------------------
    // Test 2: ApplyRecommendedAsync skips settings incompatible with the current OS
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyRecommendedAsync_SkipsIncompatibleOS_Settings()
    {
        // Arrange: running Windows 11, so a Windows-10-only setting should be skipped
        var win10OnlySetting = new SettingDefinition
        {
            Id = "win10-only",
            Name = "Win10 Only",
            Description = "Test",
            InputType = InputType.Toggle,
            IsWindows10Only = true,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKLM\Software\Test",
                    ValueName = "Win10Val",
                    ValueType = RegistryValueKind.DWord,
                    RecommendedValue = 1,
                    EnabledValue = [1],
                    DefaultValue = null
                }
            }
        };

        var compatibleSetting = CreateToggleSetting("compatible", recommendedValue: 1, enabledValue: [1]);

        SetupDomainWithSettings("win10-only", new[] { win10OnlySetting, compatibleSetting }, "SharedDomain");
        SetupDomainWithSettings("compatible", new[] { win10OnlySetting, compatibleSetting }, "SharedDomain");

        // Act
        var applied = await _service.ApplyRecommendedAsync(new[] { "win10-only", "compatible" });

        // Assert: only the compatible setting was applied
        applied.Should().Be(1);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "compatible"
        )), Times.Once);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "win10-only"
        )), Times.Never);
    }

    // ---------------------------------------------------------------
    // Test 3: ApplyRecommendedAsync — note on "already at recommended value"
    //
    // The service does NOT currently skip settings that are already at
    // their recommended value; it applies unconditionally.  This test
    // verifies that existing behaviour: both settings are applied even
    // when they conceptually "match" their recommended value already.
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyRecommendedAsync_DoesNotSkipSettings_AlreadyAtRecommendedValue()
    {
        // Arrange: two settings that are "already" at their recommended value.
        // Both fixtures supply Enabled and Disabled value arrays so the unified
        // toggle-state algorithm resolves both Recommended targets.
        var setting1 = CreateToggleSetting("already-rec-a", recommendedValue: 1,
            enabledValue: [1], disabledValue: [0]);
        var setting2 = CreateToggleSetting("already-rec-b", recommendedValue: 0,
            enabledValue: [1], disabledValue: [0]);

        SetupDomainWithSettings("already-rec-a", new[] { setting1 }, "DomainA");
        SetupDomainWithSettings("already-rec-b", new[] { setting2 }, "DomainB");

        // Act
        var applied = await _service.ApplyRecommendedAsync(new[] { "already-rec-a", "already-rec-b" });

        // Assert: both are still applied (no early-exit optimisation exists)
        applied.Should().Be(2);
        _mockAppService.Verify(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()), Times.Exactly(2));
    }

    // ---------------------------------------------------------------
    // Test 4: ResetToDefaultsAsync applies default values to all settings
    // ---------------------------------------------------------------

    [Fact]
    public async Task ResetToDefaultsAsync_AppliesDefaultValues_ToAllSettings()
    {
        // Arrange: setting-a default=1 (enabled), setting-b default=0 (disabled).
        // Both fixtures must populate EnabledValue and DisabledValue so the unified
        // toggle-state algorithm can map DefaultValue → Enable. (Real catalog settings
        // always supply both arrays.)
        var settingA = CreateToggleSetting("reset-a", recommendedValue: 0, defaultValue: 1,
            enabledValue: [1], disabledValue: [0]);
        var settingB = CreateToggleSetting("reset-b", recommendedValue: 1, defaultValue: 0,
            enabledValue: [1], disabledValue: [0]);

        SetupDomainWithSettings("reset-a", new[] { settingA }, "DomainA");
        SetupDomainWithSettings("reset-b", new[] { settingB }, "DomainB");

        // Act
        var applied = await _service.ResetToDefaultsAsync(new[] { "reset-a", "reset-b" });

        // Assert: bulk Toggle apply mirrors per-card HandleToggleAsync — passes only
        // SettingId + Enable + ResetToDefault, no Value (apply pipeline derives it).
        applied.Should().Be(2);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "reset-a" &&
            r.Enable == true &&
            r.ResetToDefault == true &&
            r.SkipValuePrerequisites == true
        )), Times.Once);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "reset-b" &&
            r.Enable == false &&
            r.ResetToDefault == true &&
            r.SkipValuePrerequisites == true
        )), Times.Once);
    }

    // ---------------------------------------------------------------
    // Test 5: ResetToDefaultsAsync sets Enable=false for group policy
    //         keys whose DefaultValue is null
    // ---------------------------------------------------------------

    [Fact]
    public async Task ResetToDefaultsAsync_HandlesNullDefaultValue_ForGroupPolicyKeys()
    {
        // Arrange: group policy setting with no DefaultValue. The Windows default for
        // a GP key is "key absent" — expressed by the null sentinel in DisabledValue.
        // The unified toggle-state algorithm reads that sentinel and resolves Default → false.
        var gpSetting = CreateToggleSetting(
            "gp-setting",
            recommendedValue: 1,
            defaultValue: null,
            enabledValue: [1],
            disabledValue: [null],
            isGroupPolicy: true);

        SetupDomainWithSettings("gp-setting", new[] { gpSetting }, "PolicyDomain");

        // Act
        var applied = await _service.ResetToDefaultsAsync(new[] { "gp-setting" });

        // Assert: Enable=false, ResetToDefault=true. Bulk Toggle apply no longer passes Value;
        // the apply pipeline derives the registry write from DisabledValue (= [null] → delete).
        applied.Should().Be(1);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "gp-setting" &&
            r.Enable == false &&
            r.ResetToDefault == true &&
            r.SkipValuePrerequisites == true
        )), Times.Once);
    }

    // ---------------------------------------------------------------
    // Test 6: GetAffectedCountAsync returns the correct count,
    //         excluding settings that have neither a recommended value
    //         nor a default/group-policy entry (nothing would change).
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAffectedCountAsync_ReturnsCorrectCount_ExcludingAlreadyMatching()
    {
        // Arrange — the count must agree with the apply path. After unification the
        // apply path's toggle-state resolver is the only judge of "would this change":
        //   settingWithRec        – RecommendedValue=1, EnabledValue=[1]    → recommended yes
        //   settingWithDef        – DefaultValue=0, EnabledValue=[1], DisabledValue=[0] → default yes
        //   settingNoValues       – neither                                  → both no
        //   settingGpKeyAbsent    – GP, no DefaultValue, DisabledValue=[null] → default yes (key-absent sentinel)
        //   settingGpUnresolvable – GP, no DefaultValue, no null sentinel    → default no (silently skipped at apply too)

        var settingWithRec = CreateToggleSetting("has-rec", recommendedValue: 1);
        var settingWithDef = CreateToggleSetting("has-def", recommendedValue: null, defaultValue: 0,
            enabledValue: [1], disabledValue: [0]);
        var settingNoValues = new SettingDefinition
        {
            Id = "no-values",
            Name = "No Values",
            Description = "Test",
            InputType = InputType.Toggle,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKLM\Software\Test",
                    ValueName = "Empty",
                    ValueType = RegistryValueKind.DWord,
                    RecommendedValue = null,
                    DefaultValue = null,
                    IsGroupPolicy = false,
                }
            }
        };
        var settingGpKeyAbsent = CreateToggleSetting("gp-key-absent",
            recommendedValue: null, defaultValue: null,
            enabledValue: [1], disabledValue: [null], isGroupPolicy: true);
        var settingGpUnresolvable = CreateToggleSetting("gp-unresolvable",
            recommendedValue: null, defaultValue: null, isGroupPolicy: true);

        var allIds = new[] { "has-rec", "has-def", "no-values", "gp-key-absent", "gp-unresolvable" };

        SetupDomainWithSettings("has-rec",          new[] { settingWithRec },        "D1");
        SetupDomainWithSettings("has-def",          new[] { settingWithDef },        "D2");
        SetupDomainWithSettings("no-values",        new[] { settingNoValues },       "D3");
        SetupDomainWithSettings("gp-key-absent",    new[] { settingGpKeyAbsent },    "D4");
        SetupDomainWithSettings("gp-unresolvable",  new[] { settingGpUnresolvable }, "D5");

        // Act
        var recCount     = await _service.GetAffectedCountAsync(allIds, BulkActionType.ApplyRecommended);
        var defaultCount = await _service.GetAffectedCountAsync(allIds, BulkActionType.ResetToDefaults);

        // Assert
        // ApplyRecommended → has-rec only (everything else has null RecommendedValue + nothing else recommends).
        recCount.Should().Be(1);

        // ResetToDefaults → has-def (DefaultValue=0 maps via DisabledValue) + gp-key-absent
        // (null sentinel in DisabledValue maps Default → Enable=false). gp-unresolvable
        // is GP-only with no DefaultValue and no null sentinel, so the unified algorithm
        // returns null and the apply path skips it; the counter MUST agree, hence 2 not 3.
        defaultCount.Should().Be(2);
    }

    // ---------------------------------------------------------------
    // Test 7: ApplyRecommendedAsync handles Toggle, Selection, and
    //         Numeric input types correctly
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyRecommendedAsync_HandlesToggle_Selection_Numeric_InputTypes()
    {
        // Toggle: recommendedValue=1, enabledValue=[1] → Enable=true
        var toggleSetting = CreateToggleSetting("toggle-input", recommendedValue: 1, enabledValue: [1]);

        // Selection: recommended option "Medium" (alphabetical index 2)
        // Sorted: "High"=0, "Low"=1, "Medium"=2
        var comboBoxOptions = new Dictionary<string, int>
        {
            ["High"]   = 3,
            ["Low"]    = 1,
            ["Medium"] = 2,
        };
        var selectionSetting = CreateSelectionSetting("selection-input", "Medium", null, comboBoxOptions);

        // Numeric: recommendedValue=75
        var numericSetting = CreateNumericSetting("numeric-input", recommendedValue: 75);

        SetupDomainWithSettings("toggle-input",    new[] { toggleSetting },    "D1");
        SetupDomainWithSettings("selection-input", new[] { selectionSetting }, "D2");
        SetupDomainWithSettings("numeric-input",   new[] { numericSetting },   "D3");

        // Act
        var applied = await _service.ApplyRecommendedAsync(
            new[] { "toggle-input", "selection-input", "numeric-input" });

        // Assert: all three applied
        applied.Should().Be(3);

        // Toggle: Enable=true. Bulk Toggle apply mirrors per-card HandleToggleAsync —
        // no Value param; the apply pipeline derives the registry write from EnabledValue.
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "toggle-input" &&
            r.Enable == true &&
            r.SkipValuePrerequisites == true
        )), Times.Once);

        // Selection: Enable=true, Value=2 (index of "Medium" in alphabetical order)
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "selection-input" &&
            r.Enable == true &&
            r.Value != null && r.Value.Equals(2) &&
            r.SkipValuePrerequisites == true
        )), Times.Once);

        // Numeric: Enable=true, Value=75
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "numeric-input" &&
            r.Enable == true &&
            r.Value != null && r.Value.Equals(75) &&
            r.SkipValuePrerequisites == true
        )), Times.Once);
    }
}

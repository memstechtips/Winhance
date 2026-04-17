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
        // Arrange
        var setting1 = CreateToggleSetting("setting-a", recommendedValue: 1, enabledValue: [1]);
        var setting2 = CreateToggleSetting("setting-b", recommendedValue: 0, enabledValue: [1]);

        SetupDomainWithSettings("setting-a", new[] { setting1 }, "DomainA");
        SetupDomainWithSettings("setting-b", new[] { setting2 }, "DomainB");

        // Act
        var applied = await _service.ApplyRecommendedAsync(new[] { "setting-a", "setting-b" });

        // Assert
        applied.Should().Be(2);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "setting-a" &&
            r.Enable == true &&
            r.Value != null && r.Value.Equals(1) &&
            r.SkipValuePrerequisites == true
        )), Times.Once);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "setting-b" &&
            r.Enable == false &&
            r.Value != null && r.Value.Equals(0) &&
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
        // Arrange: two settings that are "already" at their recommended value
        var setting1 = CreateToggleSetting("already-rec-a", recommendedValue: 1, enabledValue: [1]);
        var setting2 = CreateToggleSetting("already-rec-b", recommendedValue: 0, enabledValue: [1]);

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
        // Arrange: setting-a default=1 (enabled), setting-b default=0 (disabled)
        var settingA = CreateToggleSetting("reset-a", recommendedValue: 0, defaultValue: 1, enabledValue: [1]);
        var settingB = CreateToggleSetting("reset-b", recommendedValue: 1, defaultValue: 0, enabledValue: [1]);

        SetupDomainWithSettings("reset-a", new[] { settingA }, "DomainA");
        SetupDomainWithSettings("reset-b", new[] { settingB }, "DomainB");

        // Act
        var applied = await _service.ResetToDefaultsAsync(new[] { "reset-a", "reset-b" });

        // Assert
        applied.Should().Be(2);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "reset-a" &&
            r.Enable == true &&
            r.Value != null && r.Value.Equals(1) &&
            r.ResetToDefault == true &&
            r.SkipValuePrerequisites == true
        )), Times.Once);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "reset-b" &&
            r.Enable == false &&
            r.Value != null && r.Value.Equals(0) &&
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
        // Arrange: group policy setting with no DefaultValue
        var gpSetting = CreateToggleSetting(
            "gp-setting",
            recommendedValue: 1,
            defaultValue: null,
            enabledValue: [1],
            isGroupPolicy: true);

        SetupDomainWithSettings("gp-setting", new[] { gpSetting }, "PolicyDomain");

        // Act
        var applied = await _service.ResetToDefaultsAsync(new[] { "gp-setting" });

        // Assert: Enable=false, Value=null, ResetToDefault=true
        applied.Should().Be(1);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "gp-setting" &&
            r.Enable == false &&
            r.Value == null &&
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
        // Arrange:
        //   settingWithRec   – has RecommendedValue   → counted for ApplyRecommended
        //   settingWithDef   – has DefaultValue        → counted for ResetToDefaults
        //   settingNoValues  – neither                 → excluded from both counts
        //   settingGP        – IsGroupPolicy, no Def   → counted for ResetToDefaults

        var settingWithRec = CreateToggleSetting("has-rec", recommendedValue: 1);
        var settingWithDef = CreateToggleSetting("has-def", recommendedValue: null, defaultValue: 0);
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
        var settingGP = CreateToggleSetting("gp-only", recommendedValue: null, defaultValue: null, isGroupPolicy: true);

        var allIds = new[] { "has-rec", "has-def", "no-values", "gp-only" };

        SetupDomainWithSettings("has-rec",    new[] { settingWithRec },  "D1");
        SetupDomainWithSettings("has-def",    new[] { settingWithDef },  "D2");
        SetupDomainWithSettings("no-values",  new[] { settingNoValues }, "D3");
        SetupDomainWithSettings("gp-only",    new[] { settingGP },       "D4");

        // Act
        var recCount     = await _service.GetAffectedCountAsync(allIds, BulkActionType.ApplyRecommended);
        var defaultCount = await _service.GetAffectedCountAsync(allIds, BulkActionType.ResetToDefaults);

        // Assert
        // ApplyRecommended counts settings with a RecommendedValue → 1 (has-rec)
        recCount.Should().Be(1);

        // ResetToDefaults counts settings with DefaultValue OR IsGroupPolicy → 3 (has-def + gp-only + has-rec has no default... wait)
        // has-def  → DefaultValue=0    → yes
        // gp-only  → IsGroupPolicy     → yes
        // no-values → neither          → no
        // has-rec  → no DefaultValue, not GP → no
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

        // Toggle: Enable=true, Value=1
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "toggle-input" &&
            r.Enable == true &&
            r.Value != null && r.Value.Equals(1) &&
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

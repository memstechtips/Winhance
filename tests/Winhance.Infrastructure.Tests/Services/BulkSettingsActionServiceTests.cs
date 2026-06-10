using System;
using System.Collections.Generic;
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
    private readonly Mock<ICompatibleSettingsRegistry> _mockRegistry = new();
    private readonly Mock<IWindowsVersionService> _mockVersionService = new();
    private readonly Mock<ISettingApplicationService> _mockAppService = new();
    private readonly Mock<IProcessRestartManager> _mockProcessRestartManager = new();
    private readonly Mock<IRecommendedSettingsApplier> _mockRecommendedApplier = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IChangeHistoryService> _mockChangeHistory = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly BulkSettingsActionService _service;

    public BulkSettingsActionServiceTests()
    {
        // Default OS setup: Windows 11, build 22621
        _mockVersionService.Setup(v => v.IsWindows11()).Returns(true);
        _mockVersionService.Setup(v => v.GetWindowsBuildNumber()).Returns(22621);
        _mockVersionService.Setup(v => v.GetWindowsBuildRevision()).Returns(0);

        // Default: ApplySettingAsync succeeds
        _mockAppService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Default: SuppressRestarts and FlushCoalescedRestartsAsync succeed
        _mockProcessRestartManager
            .Setup(p => p.SuppressRestarts())
            .Returns(Mock.Of<System.IDisposable>());
        _mockProcessRestartManager
            .Setup(p => p.FlushCoalescedRestartsAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .Returns(Task.CompletedTask);

        // Default: ApplyRecommendedToSettingsAsync returns empty list
        _mockRecommendedApplier
            .Setup(r => r.ApplyRecommendedToSettingsAsync(
                It.IsAny<IReadOnlyList<SettingDefinition>>(),
                It.IsAny<ISettingApplicationService>(),
                It.IsAny<IProgress<TaskProgressDetail>>()))
            .ReturnsAsync(new List<SettingDefinition>());

        // Default: GetString returns the key; BeginBatch returns a no-op disposable
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        _mockChangeHistory
            .Setup(h => h.BeginBatch(It.IsAny<string>()))
            .Returns(Mock.Of<IDisposable>());

        _service = new BulkSettingsActionService(
            _mockRegistry.Object,
            _mockVersionService.Object,
            _mockAppService.Object,
            _mockProcessRestartManager.Object,
            _mockRecommendedApplier.Object,
            _mockLog.Object,
            _mockChangeHistory.Object,
            _mockLocalizationService.Object);
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
        // domainName is retained for call-site readability but unused — the registry's
        // GetById is O(1) and domain-agnostic.
        _ = domainName;
        var match = settings.FirstOrDefault(s => s.Id == settingId);
        _mockRegistry.Setup(r => r.GetById(settingId)).Returns(match);
    }

    // ---------------------------------------------------------------
    // Test 1: ApplyRecommendedAsync delegates to IRecommendedSettingsApplier
    //         and then flushes exactly once.
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyRecommendedAsync_DelegatesToApplier_ThenFlushesOnce()
    {
        // Arrange: two settings resolved from the registry
        var setting1 = CreateToggleSetting("setting-a", recommendedValue: 1,
            enabledValue: [1], disabledValue: [0]);
        var setting2 = CreateToggleSetting("setting-b", recommendedValue: 0,
            enabledValue: [1], disabledValue: [0]);

        SetupDomainWithSettings("setting-a", new[] { setting1 }, "DomainA");
        SetupDomainWithSettings("setting-b", new[] { setting2 }, "DomainB");

        // Configure the applier mock to return both settings as "applied"
        _mockRecommendedApplier
            .Setup(r => r.ApplyRecommendedToSettingsAsync(
                It.IsAny<IReadOnlyList<SettingDefinition>>(),
                It.IsAny<ISettingApplicationService>(),
                It.IsAny<IProgress<TaskProgressDetail>>()))
            .ReturnsAsync(new List<SettingDefinition> { setting1, setting2 });

        // Act
        var applied = await _service.ApplyRecommendedAsync(new[] { "setting-a", "setting-b" });

        // Assert: delegated to the applier with the resolved settings list
        _mockRecommendedApplier.Verify(r => r.ApplyRecommendedToSettingsAsync(
            It.Is<IReadOnlyList<SettingDefinition>>(list =>
                list.Count == 2 &&
                list.Any(s => s.Id == "setting-a") &&
                list.Any(s => s.Id == "setting-b")),
            _mockAppService.Object,
            It.IsAny<IProgress<TaskProgressDetail>>()), Times.Once);

        // Assert: flushed exactly once with the applied list
        _mockProcessRestartManager.Verify(p =>
            p.FlushCoalescedRestartsAsync(It.IsAny<IEnumerable<SettingDefinition>>()),
            Times.Once);

        // Assert: count reflects applied settings returned by the applier
        applied.Should().Be(2);
    }

    // ---------------------------------------------------------------
    // Test 2: ApplyRecommendedAsync — OS-incompatible settings excluded
    //         before handing off to the applier (ResolveSettingsAsync).
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyRecommendedAsync_SkipsIncompatibleOS_BeforeDelegating()
    {
        // Arrange: running Windows 11; win10-only setting is OS-filtered out in
        // ResolveSettingsAsync before the applier is called.
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

        SetupDomainWithSettings("win10-only",  new[] { win10OnlySetting }, "D1");
        SetupDomainWithSettings("compatible",  new[] { compatibleSetting }, "D2");

        _mockRecommendedApplier
            .Setup(r => r.ApplyRecommendedToSettingsAsync(
                It.IsAny<IReadOnlyList<SettingDefinition>>(),
                It.IsAny<ISettingApplicationService>(),
                It.IsAny<IProgress<TaskProgressDetail>>()))
            .ReturnsAsync((IReadOnlyList<SettingDefinition> passed, ISettingApplicationService _, IProgress<TaskProgressDetail> _) =>
                (IReadOnlyList<SettingDefinition>)passed.ToList());

        // Act
        await _service.ApplyRecommendedAsync(new[] { "win10-only", "compatible" });

        // Assert: the applier is called only with the compatible setting
        _mockRecommendedApplier.Verify(r => r.ApplyRecommendedToSettingsAsync(
            It.Is<IReadOnlyList<SettingDefinition>>(list =>
                list.Count == 1 && list[0].Id == "compatible"),
            _mockAppService.Object,
            It.IsAny<IProgress<TaskProgressDetail>>()), Times.Once);
    }

    // ---------------------------------------------------------------
    // Test 3: ApplyRecommendedAsync flushes once even when all skipped
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyRecommendedAsync_NothingApplied_StillFlushesOnce()
    {
        // Applier returns empty (nothing recommended) — flush still called once.
        var setting = CreateToggleSetting("no-rec", recommendedValue: null);
        SetupDomainWithSettings("no-rec", new[] { setting }, "D1");

        _mockRecommendedApplier
            .Setup(r => r.ApplyRecommendedToSettingsAsync(
                It.IsAny<IReadOnlyList<SettingDefinition>>(),
                It.IsAny<ISettingApplicationService>(),
                It.IsAny<IProgress<TaskProgressDetail>>()))
            .ReturnsAsync(new List<SettingDefinition>());

        await _service.ApplyRecommendedAsync(new[] { "no-rec" });

        _mockProcessRestartManager.Verify(
            p => p.FlushCoalescedRestartsAsync(It.IsAny<IEnumerable<SettingDefinition>>()),
            Times.Once);
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
    // Test 7: ResetToDefaultsAsync wraps applies in a change-history batch
    // ---------------------------------------------------------------

    [Fact]
    public async Task ResetToDefaultsAsync_WrapsAppliesInChangeHistoryBatch()
    {
        // Arrange: one resettable toggle setting
        var setting = CreateToggleSetting("reset-batch", recommendedValue: 0, defaultValue: 1,
            enabledValue: [1], disabledValue: [0]);
        SetupDomainWithSettings("reset-batch", new[] { setting }, "Domain");

        // Act
        await _service.ResetToDefaultsAsync(new[] { "reset-batch" });

        // Assert: a batch was opened with the expected header key
        _mockChangeHistory.Verify(h => h.BeginBatch("QuickActions_ResetDefaults"), Times.Once);
    }

    // ---------------------------------------------------------------
    // Test 8: ApplyRecommendedAsync passes all resolved settings to the
    //         applier (Toggle + Selection + Numeric)
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyRecommendedAsync_PassesAllResolvedTypes_ToApplier()
    {
        var toggleSetting = CreateToggleSetting("toggle-input", recommendedValue: 1, enabledValue: [1]);

        var comboBoxOptions = new Dictionary<string, int>
        {
            ["High"]   = 3,
            ["Low"]    = 1,
            ["Medium"] = 2,
        };
        var selectionSetting = CreateSelectionSetting("selection-input", "Medium", null, comboBoxOptions);
        var numericSetting   = CreateNumericSetting("numeric-input", recommendedValue: 75);

        SetupDomainWithSettings("toggle-input",    new[] { toggleSetting },    "D1");
        SetupDomainWithSettings("selection-input", new[] { selectionSetting }, "D2");
        SetupDomainWithSettings("numeric-input",   new[] { numericSetting },   "D3");

        IReadOnlyList<SettingDefinition>? captured = null;
        _mockRecommendedApplier
            .Setup(r => r.ApplyRecommendedToSettingsAsync(
                It.IsAny<IReadOnlyList<SettingDefinition>>(),
                It.IsAny<ISettingApplicationService>(),
                It.IsAny<IProgress<TaskProgressDetail>>()))
            .Callback<IReadOnlyList<SettingDefinition>, ISettingApplicationService, IProgress<TaskProgressDetail>>(
                (list, _, _) => captured = list)
            .ReturnsAsync(new List<SettingDefinition> { toggleSetting, selectionSetting, numericSetting });

        var applied = await _service.ApplyRecommendedAsync(
            new[] { "toggle-input", "selection-input", "numeric-input" });

        applied.Should().Be(3);
        captured.Should().NotBeNull();
        captured!.Should().HaveCount(3);
        captured.Should().Contain(s => s.Id == "toggle-input");
        captured.Should().Contain(s => s.Id == "selection-input");
        captured.Should().Contain(s => s.Id == "numeric-input");
    }
}

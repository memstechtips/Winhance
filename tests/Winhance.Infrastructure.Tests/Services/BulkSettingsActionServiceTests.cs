using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
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

    // Slice C: the reset loop + affected-count pair each setting to its catalog Setting and read
    // the build-aware WindowsDefault/Recommended roles, so tests use REAL catalog toggle ids (a
    // synthetic fake-id def no longer pairs). TestBuild matches the mocked version service (Win11, 22621).
    private static readonly WinBuild TestBuild = new(22621);

    private static string CatalogToggleWithDefault(bool direction) =>
        SettingCatalog.All.First(s => CatalogToggleState.GetDefault(s, TestBuild) == direction).Id;

    private static string CatalogToggleWithDefaultAny() =>
        SettingCatalog.All.First(s => CatalogToggleState.GetDefault(s, TestBuild).HasValue).Id;

    private static string CatalogToggleWithRecommendation() =>
        SettingCatalog.All.First(s => CatalogToggleState.GetRecommended(s, TestBuild).HasValue).Id;

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
    // Test 2b: stateless Actions are excluded from BOTH bulk ops
    //          (ResolveSettingsAsync), so they are never bulk-reset or
    //          bulk-recommended (Marco 2026-07-03).
    // ---------------------------------------------------------------

    private static SettingDefinition CreateActionSetting(string id) => new()
    {
        Id = id,
        Name = "Clean",
        Description = "One-shot action",
        InputType = InputType.Action,
        RegistrySettings = new[]
        {
            new RegistrySetting
            {
                KeyPath = @"HKCU\Software\Test",
                ValueName = "Favorites",
                ValueType = RegistryValueKind.Binary,
                EnabledValue = [new byte[0]],
                DisabledValue = [null],
                RecommendedValue = null,
                DefaultValue = null,
            }
        }
    };

    [Fact]
    public async Task ApplyRecommendedAsync_SkipsActions_BeforeDelegating()
    {
        // A stateless Action is filtered out in ResolveSettingsAsync before the applier is called.
        var action = CreateActionSetting("clean-action");
        var toggle = CreateToggleSetting("compatible", recommendedValue: 1, enabledValue: [1]);
        SetupDomainWithSettings("clean-action", new[] { action }, "D1");
        SetupDomainWithSettings("compatible", new[] { toggle }, "D2");

        _mockRecommendedApplier
            .Setup(r => r.ApplyRecommendedToSettingsAsync(
                It.IsAny<IReadOnlyList<SettingDefinition>>(),
                It.IsAny<ISettingApplicationService>(),
                It.IsAny<IProgress<TaskProgressDetail>>()))
            .ReturnsAsync((IReadOnlyList<SettingDefinition> passed, ISettingApplicationService _, IProgress<TaskProgressDetail> _) =>
                (IReadOnlyList<SettingDefinition>)passed.ToList());

        await _service.ApplyRecommendedAsync(new[] { "clean-action", "compatible" });

        // The applier receives only the toggle - the Action was excluded.
        _mockRecommendedApplier.Verify(r => r.ApplyRecommendedToSettingsAsync(
            It.Is<IReadOnlyList<SettingDefinition>>(list =>
                list.Count == 1 && list[0].Id == "compatible"),
            _mockAppService.Object,
            It.IsAny<IProgress<TaskProgressDetail>>()), Times.Once);
    }

    [Fact]
    public async Task ResetToDefaultsAsync_SkipsActions()
    {
        // A stateless Action is excluded from bulk reset (no ApplySettingAsync for it); a REAL catalog toggle still resets.
        var toggleId = CatalogToggleWithDefaultAny();
        var action = CreateActionSetting("clean-action");
        var toggle = CreateToggleSetting(toggleId, recommendedValue: 0, defaultValue: 1,
            enabledValue: [1], disabledValue: [0]);
        SetupDomainWithSettings("clean-action", new[] { action }, "D1");
        SetupDomainWithSettings(toggleId, new[] { toggle }, "D2");

        var applied = await _service.ResetToDefaultsAsync(new[] { "clean-action", toggleId });

        applied.Should().Be(1); // only the toggle
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == toggleId && r.ResetToDefault == true)), Times.Once);
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "clean-action")), Times.Never);
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
        // Two REAL catalog toggles: one whose Windows default on this build is Enabled, one Disabled.
        // Slice C: the build-aware reset engine resolves the per-OS default and the bulk loop forwards
        // it as Enable. CatalogToggleState.GetDefault == the old per-def default (conformance-proven).
        var enabledDefaultId = CatalogToggleWithDefault(true);
        var disabledDefaultId = CatalogToggleWithDefault(false);
        var settingA = CreateToggleSetting(enabledDefaultId, recommendedValue: 0, defaultValue: 1,
            enabledValue: [1], disabledValue: [0]);
        var settingB = CreateToggleSetting(disabledDefaultId, recommendedValue: 1, defaultValue: 0,
            enabledValue: [1], disabledValue: [0]);

        SetupDomainWithSettings(enabledDefaultId, new[] { settingA }, "DomainA");
        SetupDomainWithSettings(disabledDefaultId, new[] { settingB }, "DomainB");

        // Act
        var applied = await _service.ResetToDefaultsAsync(new[] { enabledDefaultId, disabledDefaultId });

        // Assert: each toggle resets; Enable carries the catalog-resolved default direction; no Value.
        applied.Should().Be(2);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == enabledDefaultId &&
            r.Enable == true &&
            r.ResetToDefault == true &&
            r.SkipValuePrerequisites == true
        )), Times.Once);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == disabledDefaultId &&
            r.Enable == false &&
            r.ResetToDefault == true &&
            r.SkipValuePrerequisites == true
        )), Times.Once);
    }

    // ---------------------------------------------------------------
    // Test 5: ResetToDefaultsAsync forwards a Disabled catalog default as Enable=false
    //         (the old GP key-absent resolution now lives in the converter/catalog)
    // ---------------------------------------------------------------

    [Fact]
    public async Task ResetToDefaultsAsync_DisabledDefaultToggle_ResetsWithEnableFalse()
    {
        // A REAL catalog toggle whose Windows default is Disabled resets with Enable=false. Slice C:
        // the GP "key absent = Windows default" resolution this fixture used to exercise now lives in
        // the converter/catalog (proven by RecommendedToggleStateConformanceTests); the bulk service
        // just forwards the catalog-resolved direction.
        var disabledDefaultId = CatalogToggleWithDefault(false);
        var setting = CreateToggleSetting(disabledDefaultId, recommendedValue: 1, defaultValue: 0,
            enabledValue: [1], disabledValue: [0]);
        SetupDomainWithSettings(disabledDefaultId, new[] { setting }, "PolicyDomain");

        var applied = await _service.ResetToDefaultsAsync(new[] { disabledDefaultId });

        applied.Should().Be(1);
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == disabledDefaultId &&
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
        // The count must agree with the apply path. Slice C: the toggle contribution now comes from the
        // catalog (build-aware), so a REAL catalog toggle exercises it; the ComboBox contribution is
        // unchanged and exercised by a synthetic Selection; a setting with neither is excluded. Each
        // count call uses a curated id list so every id's contribution is known.
        var recToggleId = CatalogToggleWithRecommendation();  // catalog toggle carrying a Recommended role
        var defToggleId = CatalogToggleWithDefault(false);     // catalog toggle carrying a Windows default
        var selRec = CreateSelectionSetting("sel-rec", recommendedOption: "B", defaultOption: null,
            new Dictionary<string, int> { ["A"] = 0, ["B"] = 1 });
        var selDef = CreateSelectionSetting("sel-def", recommendedOption: "none", defaultOption: "A",
            new Dictionary<string, int> { ["A"] = 0, ["B"] = 1 });
        var noValues = new SettingDefinition
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

        SetupDomainWithSettings(recToggleId, new[] { CreateToggleSetting(recToggleId, recommendedValue: 1) }, "D1");
        SetupDomainWithSettings(defToggleId, new[] { CreateToggleSetting(defToggleId, recommendedValue: null, defaultValue: 0, enabledValue: [1], disabledValue: [0]) }, "D2");
        SetupDomainWithSettings("sel-rec", new[] { selRec }, "D3");
        SetupDomainWithSettings("sel-def", new[] { selDef }, "D4");
        SetupDomainWithSettings("no-values", new[] { noValues }, "D5");

        // ApplyRecommended: recToggle (catalog Recommended role) + selRec (ComboBox IsRecommended); no-values excluded.
        var recCount = await _service.GetAffectedCountAsync(
            new[] { recToggleId, "sel-rec", "no-values" }, BulkActionType.ApplyRecommended);
        recCount.Should().Be(2);

        // ResetToDefaults: defToggle (catalog Windows default) + selDef (ComboBox IsDefault); no-values excluded.
        var defaultCount = await _service.GetAffectedCountAsync(
            new[] { defToggleId, "sel-def", "no-values" }, BulkActionType.ResetToDefaults);
        defaultCount.Should().Be(2);
    }

    // ---------------------------------------------------------------
    // Test 7: ResetToDefaultsAsync wraps applies in a change-history batch
    // ---------------------------------------------------------------

    [Fact]
    public async Task ResetToDefaultsAsync_WrapsAppliesInChangeHistoryBatch()
    {
        // Arrange: one resettable REAL catalog toggle
        var toggleId = CatalogToggleWithDefaultAny();
        var setting = CreateToggleSetting(toggleId, recommendedValue: 0, defaultValue: 1,
            enabledValue: [1], disabledValue: [0]);
        SetupDomainWithSettings(toggleId, new[] { setting }, "Domain");

        // Act
        await _service.ResetToDefaultsAsync(new[] { toggleId });

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

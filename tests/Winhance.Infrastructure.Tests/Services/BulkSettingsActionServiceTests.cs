using FluentAssertions;
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
    private static readonly string[] SettingAAndB = ["setting-a", "setting-b"];
    private static readonly string[] OsIncompatibleAndCompatible = ["os-incompatible", "compatible"];
    private static readonly string[] CleanActionAndCompatible = ["clean-action", "compatible"];
    private static readonly string[] CleanActionAndResetToggle = ["clean-action", "reset-toggle"];
    private static readonly string[] NoRecOnly = ["no-rec"];
    private static readonly string[] EnabledAndDisabledDefault = ["enabled-default", "disabled-default"];
    private static readonly string[] DisabledDefaultOnly = ["disabled-default"];
    private static readonly string[] RecommendedTrio = ["rec-toggle", "sel-rec", "no-values"];
    private static readonly string[] DefaultTrio = ["def-toggle", "sel-def", "no-values"];
    private static readonly string[] ResetToggleOnly = ["reset-toggle"];
    private static readonly string[] OneOfEachInputType = ["toggle-input", "selection-input", "numeric-input"];
    private static readonly string[] PowerPlanSelectionOnly = ["power-plan-selection"];
    private static readonly string[] SliderOnly = ["slider"];

    private readonly Mock<ICatalogSettingsRegistry> _mockRegistry = new();
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
        _mockVersionService.Setup(v => v.IsWindows11()).Returns(true);
        _mockVersionService.Setup(v => v.GetWindowsBuildNumber()).Returns(22621);
        _mockVersionService.Setup(v => v.GetWindowsBuildRevision()).Returns(0);

        _mockAppService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // The reset loop builds its restart set from catalog Settings, so the flush overload takes IEnumerable<Setting>.
        _mockProcessRestartManager
            .Setup(p => p.SuppressRestarts())
            .Returns(Mock.Of<IDisposable>());
        _mockProcessRestartManager
            .Setup(p => p.FlushCoalescedRestartsAsync(It.IsAny<IEnumerable<Setting>>()))
            .Returns(Task.CompletedTask);

        _mockRecommendedApplier
            .Setup(r => r.ApplyRecommendedToSettingsAsync(
                It.IsAny<IReadOnlyList<Setting>>(),
                It.IsAny<ISettingApplicationService>(),
                It.IsAny<IProgress<TaskProgressDetail>>()))
            .ReturnsAsync(new List<Setting>());

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

    // The reset loop + affected-count + ResolveSettingsAsync all read catalog Settings DIRECTLY. Tests
    // construct synthetic Settings with exactly the roles they exercise - no real catalog id is needed.

    private static Display Disp(string id) => new() { Name = $"Setting {id}", Description = $"Description for {id}" };

    // A toggle Setting: a Recommended role on Enabled/Disabled means recommend enabling/disabling; a
    // WindowsDefault role on Enabled/Disabled is the reset direction. null = that role absent.
    private static Setting Toggle(string id, bool? recommended = null, bool? windowsDefault = null)
    {
        var enabledRoles = new List<StateRole>();
        var disabledRoles = new List<StateRole>();
        if (recommended == true) enabledRoles.Add(new StateRole(RoleKind.Recommended));
        else if (recommended == false) disabledRoles.Add(new StateRole(RoleKind.Recommended));
        if (windowsDefault == true) enabledRoles.Add(new StateRole(RoleKind.WindowsDefault));
        else if (windowsDefault == false) disabledRoles.Add(new StateRole(RoleKind.WindowsDefault));
        return new Setting
        {
            Id = id,
            Display = Disp(id),
            States = new[]
            {
                new SettingState { Label = "Enabled", Roles = enabledRoles },
                new SettingState { Label = "Disabled", Roles = disabledRoles },
            },
        };
    }

    // A registry-style Selection Setting (>=3 non-Enabled/Disabled states => Control.Selection) with an optional
    // Recommended and/or WindowsDefault role at the given index.
    private static Setting Selection(string id, int? recommendedIndex = null, int? defaultIndex = null, int numOptions = 3)
    {
        var states = new List<SettingState>(numOptions);
        for (int i = 0; i < numOptions; i++)
        {
            var roles = new List<StateRole>();
            if (recommendedIndex == i) roles.Add(new StateRole(RoleKind.Recommended));
            if (defaultIndex == i) roles.Add(new StateRole(RoleKind.WindowsDefault));
            states.Add(new SettingState { Label = $"Option{i}", Roles = roles });
        }
        return new Setting { Id = id, Display = Disp(id), States = states };
    }

    // A numeric (slider) Setting (Control.Slider); used only to prove ResolveSettingsAsync passes it through.
    private static Setting NumericSetting(string id) =>
        new() { Id = id, Display = Disp(id), Numeric = new Numeric { Min = 0, Max = 100 } };

    // An Action Setting (no states => Control.Action) - excluded from both bulk ops.
    private static Setting ActionSetting(string id) => new() { Id = id, Display = Disp(id) };

    private void SetupRegistry(string id, Setting? setting)
        => _mockRegistry.Setup(r => r.GetById(id, It.IsAny<bool>())).Returns(setting);

    [Fact]
    public async Task ApplyRecommendedAsync_DelegatesToApplier_ThenFlushesOnce()
    {
        var setting1 = Toggle("setting-a", recommended: true);
        var setting2 = Toggle("setting-b", recommended: false);
        SetupRegistry("setting-a", setting1);
        SetupRegistry("setting-b", setting2);

        _mockRecommendedApplier
            .Setup(r => r.ApplyRecommendedToSettingsAsync(
                It.IsAny<IReadOnlyList<Setting>>(),
                It.IsAny<ISettingApplicationService>(),
                It.IsAny<IProgress<TaskProgressDetail>>()))
            .ReturnsAsync(new List<Setting> { setting1, setting2 });

        var applied = await _service.ApplyRecommendedAsync(SettingAAndB);

        _mockRecommendedApplier.Verify(r => r.ApplyRecommendedToSettingsAsync(
            It.Is<IReadOnlyList<Setting>>(list =>
                list.Count == 2 &&
                list.Any(s => s.Id == "setting-a") &&
                list.Any(s => s.Id == "setting-b")),
            _mockAppService.Object,
            It.IsAny<IProgress<TaskProgressDetail>>()), Times.Once);

        _mockProcessRestartManager.Verify(p =>
            p.FlushCoalescedRestartsAsync(It.IsAny<IEnumerable<Setting>>()),
            Times.Once);

        applied.Should().Be(2);
    }

    // The OS gate lives in the catalog registry, which returns null for an OS-incompatible id (GetById),
    // so ResolveSettingsAsync's null-skip drops it.
    [Fact]
    public async Task ApplyRecommendedAsync_SkipsRegistryExcluded_BeforeDelegating()
    {
        SetupRegistry("os-incompatible", null); // registry OS-filtered it out
        SetupRegistry("compatible", Toggle("compatible", recommended: true));

        _mockRecommendedApplier
            .Setup(r => r.ApplyRecommendedToSettingsAsync(
                It.IsAny<IReadOnlyList<Setting>>(),
                It.IsAny<ISettingApplicationService>(),
                It.IsAny<IProgress<TaskProgressDetail>>()))
            .ReturnsAsync((IReadOnlyList<Setting> passed, ISettingApplicationService _, IProgress<TaskProgressDetail> _) =>
                (IReadOnlyList<Setting>)passed.ToList());

        await _service.ApplyRecommendedAsync(OsIncompatibleAndCompatible);

        _mockRecommendedApplier.Verify(r => r.ApplyRecommendedToSettingsAsync(
            It.Is<IReadOnlyList<Setting>>(list =>
                list.Count == 1 && list[0].Id == "compatible"),
            _mockAppService.Object,
            It.IsAny<IProgress<TaskProgressDetail>>()), Times.Once);
    }

    [Fact]
    public async Task ApplyRecommendedAsync_SkipsActions_BeforeDelegating()
    {
        SetupRegistry("clean-action", ActionSetting("clean-action"));
        SetupRegistry("compatible", Toggle("compatible", recommended: true));

        _mockRecommendedApplier
            .Setup(r => r.ApplyRecommendedToSettingsAsync(
                It.IsAny<IReadOnlyList<Setting>>(),
                It.IsAny<ISettingApplicationService>(),
                It.IsAny<IProgress<TaskProgressDetail>>()))
            .ReturnsAsync((IReadOnlyList<Setting> passed, ISettingApplicationService _, IProgress<TaskProgressDetail> _) =>
                (IReadOnlyList<Setting>)passed.ToList());

        await _service.ApplyRecommendedAsync(CleanActionAndCompatible);

        _mockRecommendedApplier.Verify(r => r.ApplyRecommendedToSettingsAsync(
            It.Is<IReadOnlyList<Setting>>(list =>
                list.Count == 1 && list[0].Id == "compatible"),
            _mockAppService.Object,
            It.IsAny<IProgress<TaskProgressDetail>>()), Times.Once);
    }

    [Fact]
    public async Task ResetToDefaultsAsync_SkipsActions()
    {
        SetupRegistry("clean-action", ActionSetting("clean-action"));
        SetupRegistry("reset-toggle", Toggle("reset-toggle", windowsDefault: true));

        var applied = await _service.ResetToDefaultsAsync(CleanActionAndResetToggle);

        applied.Should().Be(1);
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "reset-toggle" && r.ResetToDefault == true)), Times.Once);
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "clean-action")), Times.Never);
    }

    [Fact]
    public async Task ApplyRecommendedAsync_NothingApplied_StillFlushesOnce()
    {
        SetupRegistry("no-rec", Toggle("no-rec"));

        _mockRecommendedApplier
            .Setup(r => r.ApplyRecommendedToSettingsAsync(
                It.IsAny<IReadOnlyList<Setting>>(),
                It.IsAny<ISettingApplicationService>(),
                It.IsAny<IProgress<TaskProgressDetail>>()))
            .ReturnsAsync(new List<Setting>());

        await _service.ApplyRecommendedAsync(NoRecOnly);

        _mockProcessRestartManager.Verify(
            p => p.FlushCoalescedRestartsAsync(It.IsAny<IEnumerable<Setting>>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetToDefaultsAsync_AppliesDefaultValues_ToAllSettings()
    {
        // The build-aware reset engine resolves the per-OS default; the bulk loop forwards the catalog-resolved direction as Enable.
        SetupRegistry("enabled-default", Toggle("enabled-default", windowsDefault: true));
        SetupRegistry("disabled-default", Toggle("disabled-default", windowsDefault: false));

        var applied = await _service.ResetToDefaultsAsync(EnabledAndDisabledDefault);

        applied.Should().Be(2);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "enabled-default" &&
            r.Enable == true &&
            r.ResetToDefault == true &&
            r.SkipValuePrerequisites == true
        )), Times.Once);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "disabled-default" &&
            r.Enable == false &&
            r.ResetToDefault == true &&
            r.SkipValuePrerequisites == true
        )), Times.Once);
    }

    [Fact]
    public async Task ResetToDefaultsAsync_DisabledDefaultToggle_ResetsWithEnableFalse()
    {
        SetupRegistry("disabled-default", Toggle("disabled-default", windowsDefault: false));

        var applied = await _service.ResetToDefaultsAsync(DisabledDefaultOnly);

        applied.Should().Be(1);
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "disabled-default" &&
            r.Enable == false &&
            r.ResetToDefault == true &&
            r.SkipValuePrerequisites == true
        )), Times.Once);
    }

    [Fact]
    public async Task GetAffectedCountAsync_ReturnsCorrectCount_ExcludingAlreadyMatching()
    {
        // recToggle carries ONLY a Recommended role, defToggle ONLY a Windows default - so a mis-dispatch
        // (wrong predicate) would change the count. selRec/selDef exercise the Selection role contribution;
        // noValues has neither and is excluded from both.
        var recToggle = Toggle("rec-toggle", recommended: true);
        var defToggle = Toggle("def-toggle", windowsDefault: false);
        var selRec = Selection("sel-rec", recommendedIndex: 1);
        var selDef = Selection("sel-def", defaultIndex: 0);
        var noValues = Toggle("no-values");

        SetupRegistry("rec-toggle", recToggle);
        SetupRegistry("def-toggle", defToggle);
        SetupRegistry("sel-rec", selRec);
        SetupRegistry("sel-def", selDef);
        SetupRegistry("no-values", noValues);

        var recCount = await _service.GetAffectedCountAsync(
            RecommendedTrio, BulkActionType.ApplyRecommended);
        recCount.Should().Be(2);

        var defaultCount = await _service.GetAffectedCountAsync(
            DefaultTrio, BulkActionType.ResetToDefaults);
        defaultCount.Should().Be(2);
    }

    [Fact]
    public async Task ResetToDefaultsAsync_WrapsAppliesInChangeHistoryBatch()
    {
        SetupRegistry("reset-toggle", Toggle("reset-toggle", windowsDefault: true));

        await _service.ResetToDefaultsAsync(ResetToggleOnly);

        _mockChangeHistory.Verify(h => h.BeginBatch("QuickActions_ResetDefaults"), Times.Once);
    }

    [Fact]
    public async Task ApplyRecommendedAsync_PassesAllResolvedTypes_ToApplier()
    {
        SetupRegistry("toggle-input", Toggle("toggle-input", recommended: true));
        SetupRegistry("selection-input", Selection("selection-input", recommendedIndex: 1));
        SetupRegistry("numeric-input", NumericSetting("numeric-input"));

        IReadOnlyList<Setting>? captured = null;
        _mockRecommendedApplier
            .Setup(r => r.ApplyRecommendedToSettingsAsync(
                It.IsAny<IReadOnlyList<Setting>>(),
                It.IsAny<ISettingApplicationService>(),
                It.IsAny<IProgress<TaskProgressDetail>>()))
            .Callback<IReadOnlyList<Setting>, ISettingApplicationService, IProgress<TaskProgressDetail>>(
                (list, _, _) => captured = list)
            .ReturnsAsync(new List<Setting>());

        await _service.ApplyRecommendedAsync(
            OneOfEachInputType);

        captured.Should().NotBeNull();
        captured!.Should().HaveCount(3);
        captured.Should().Contain(s => s.Id == "toggle-input");
        captured.Should().Contain(s => s.Id == "selection-input");
        captured.Should().Contain(s => s.Id == "numeric-input");
    }

    // Bulk reset must NOT call ApplySettingAsync for a PowerPlan setting. power-plan-selection's
    // DERIVED Control is PowerPlan (OptionSource != null): it finds no static default and falls
    // through WITHOUT applying, but is still counted via the post-chain applied++. Applying it would
    // mean a null-plan Failed reset + a spurious event, so the Times.Never assertion is the regression guard.
    [Fact]
    public async Task ResetToDefaultsAsync_PowerPlanSetting_IsNotApplied()
    {
        var powerPlan = new Setting
        {
            Id = "power-plan-selection",
            Display = Disp("power-plan-selection"),
            OptionSource = Mock.Of<IDynamicOptionSource>(),
        };
        SetupRegistry("power-plan-selection", powerPlan);

        var applied = await _service.ResetToDefaultsAsync(PowerPlanSelectionOnly);

        applied.Should().Be(1);
        _mockAppService.Verify(
            s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()), Times.Never);
    }

    // A Slider (NumericRange) still reaches the reset else branch and resets with a null value
    // (unconditional apply) - guards that the PowerPlan gate above did not also exclude real Sliders.
    [Fact]
    public async Task ResetToDefaultsAsync_SliderSetting_ReachesElseBranch()
    {
        SetupRegistry("slider", NumericSetting("slider"));

        var applied = await _service.ResetToDefaultsAsync(SliderOnly);

        applied.Should().Be(1);
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "slider" && r.ResetToDefault == true)), Times.Once);
    }
}

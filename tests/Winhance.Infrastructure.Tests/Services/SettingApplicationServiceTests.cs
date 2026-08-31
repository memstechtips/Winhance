using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.Infrastructure.Tests.Services;

public class SettingApplicationServiceTests
{
    private readonly Mock<ICatalogSettingsRegistry> _mockSettingsRegistry = new();
    private readonly Mock<ISpecialSettingHandlerRegistry> _mockSpecialHandlerRegistry = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly Mock<IRecommendedSettingsApplier> _mockRecommended = new();
    private readonly Mock<IProcessRestartManager> _mockRestart = new();
    private readonly Mock<IChangeHistoryService> _mockChangeHistory = new();
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IHardwareDetectionService> _mockHardware = new();
    private readonly Mock<IStateWriter> _mockStateWriter = new();
    private readonly Mock<IAsyncEffectRunner> _mockAsyncEffects = new();
    private readonly Mock<IWindowsVersionService> _mockVersion = new();
    private readonly Mock<ICatalogDetectionService> _mockCatalogDetection = new();
    private readonly Mock<ICatalogSettingStateProvider> _mockSettingStateProvider = new();
    private readonly Mock<IConfigImportState> _mockConfigImportState = new();
    private readonly Mock<IPowerSettingsQueryService> _mockPowerQuery = new();
    private readonly SettingApplicationService _service;

    public SettingApplicationServiceTests()
    {
        // Default: machine HAS a battery, so every existing AC/DC test keeps its current
        // "AC: x, DC: y" expectations. No-battery tests override this per-test.
        _mockHardware.Setup(h => h.HasBattery()).Returns(true);

        // Default: every IStateWriter write SUCCEEDS, so a paired setting routed through the ApplyExecutor
        // reports OperationResult.Succeeded. There is no fallback that makes an unpaired id succeed; tests
        // asserting a failure use an unpaired (null-plan) id instead of a mock override.
        _mockStateWriter.SetReturnsDefault(true);

        // Default: nothing deferred, nothing failed. Required, not optional - RunAllAsync returns a
        // reference type, so an unstubbed Moq call hands back a NULL list and the caller NREs on .Count.
        _mockAsyncEffects
            .Setup(r => r.RunAllAsync(It.IsAny<IReadOnlyList<Effect>>()))
            .ReturnsAsync(Array.Empty<string>());

        // Default: GetString echoes the key back. A key-echo is NOT the "[{key}]" miss-marker, so by default
        // ResolveLocalized treats every key as a HIT returning the key text; tests that assert on display strings
        // set explicit returns.
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        // Mirrors the stub above onto TryGetString - an unstubbed Moq answers "missing" for every key.
        _mockLocalization.MirrorTryGetString();

        // Default: a Windows 11 build. Build-agnostic settings (no Target.AppliesTo) are unaffected by this
        // value; build-gated tests override it per-test.
        _mockVersion.Setup(v => v.GetWindowsBuildNumber()).Returns(22631);
        _mockVersion.Setup(v => v.GetWindowsBuildRevision()).Returns(0);

        // Default: the catalog detection engine resolves nothing. With an empty dictionary, currentStateOf
        // returns null for every id, so the relationship resolvers fire no follow-on applies - paired settings
        // with no live-state-dependent relationships stay a no-op.
        _mockCatalogDetection
            .Setup(d => d.DetectAsync(It.IsAny<IReadOnlyCollection<Setting>>()))
            .ReturnsAsync(new Dictionary<string, CatalogDetectionResult>());

        // Default: the full-state provider (paired before-state read at the change-history receipt) finds
        // nothing, so there is no before-state by default.
        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>());

        // Default: no Winhance plan on the machine, so selecting it counts as creating it and the recommended
        // stamp runs. An unstubbed mock returns a null list, which the existence check would throw on.
        _mockPowerQuery
            .Setup(p => p.GetAvailablePowerPlansAsync())
            .ReturnsAsync(new List<PowerPlan>());

        _service = new SettingApplicationService(
            _mockSettingsRegistry.Object, _mockSpecialHandlerRegistry.Object,
            _mockLog.Object,
            _mockEventBus.Object, _mockRecommended.Object, _mockRestart.Object,
            _mockChangeHistory.Object, _mockLocalization.Object,
            _mockHardware.Object, _mockStateWriter.Object, _mockAsyncEffects.Object, _mockVersion.Object,
            _mockCatalogDetection.Object, _mockSettingStateProvider.Object, _mockPowerQuery.Object,
            _mockConfigImportState.Object);
    }

    // The catalog Setting the funnel resolves for an id. REAL catalog Setting for a paired id (so
    // Control/Id AND the change-history rendering are all live-catalog-correct); a fake TOGGLE-shaped Setting for
    // an unpaired test id (non-null so the not-found throw guard passes; the resolver then yields a null plan ->
    // Failed, which the unpaired-id tests assert). Rendering reads SettingCatalog.Find independently, so a fake
    // id renders no receipt. Two Enabled/Disabled states => Control == Toggle.
    private static Setting CatalogOrFake(string id) =>
        SettingCatalog.Find(id) ?? new Setting
        {
            Id = id,
            Display = new Display { Name = $"Setting {id}", Description = $"Description for {id}" },
            States = new[]
            {
                new SettingState { Label = "Enabled" },
                new SettingState { Label = "Disabled" },
            },
        };

    private void SetupSettingInRegistry(string settingId)
    {
        _mockSettingsRegistry.Setup(r => r.GetById(settingId, It.IsAny<CatalogScope>())).Returns(CatalogOrFake(settingId));
    }

    // Real catalog settings whose Control routes a given value shape through the engine to a SUCCEEDING plan.
    // The registry mock's GetById returns the REAL catalog Setting for these ids (CatalogOrFake), so routing,
    // the Control gate, AND the change-history receipt rendering all read the live catalog Setting. Each test mocks the
    // REAL catalog state labels and picks raw/apply values against the real states. SkipValuePrerequisites isolates the
    // receipt from relationship cascades.
    private const string PowerCfgSelectionId = "power-display-timeout"; // powercfg-Separate selection, 16 Template_TimeIntervals_Option_N states (Set["Power"] = 0,60,120,180,300,600,...)
    private const string PowerCfgNumericId = "power-harddisk-timeout";  // powercfg-Separate numeric slider, Minutes
    private const string PowerCfgPercentNumericId = "processor-min-state"; // powercfg-Separate numeric slider, "%" units

    // A real catalog plain-registry toggle (Enabled/Disabled states, a RegTarget). Applying it routes through the
    // engine to a plan the (defaulted-to-success) writer completes, so the funnel returns Success. GetById returns
    // this real catalog toggle Setting (Control == Toggle) for the change-history receipt.
    private static string RealPairedToggleId() => SettingCatalog.All.First(s =>
        s.Detector is null && s.OptionSource is null && s.Numeric is null
        && s.States.Any(st => st.Label == "Enabled") && s.States.Any(st => st.Label == "Disabled")
        && s.Targets.OfType<RegTarget>().Any()).Id;

    [Fact]
    public async Task ApplySettingAsync_PairedPlainToggle_RoutesThroughStateWriter()
    {
        // A real catalog plain registry toggle (no custom detector / dynamic options / numeric, with both an Enabled
        // and a Disabled state) applies through the ApplyExecutor + IStateWriter. Unpaired/fake ids (the null-plan
        // tests below) resolve to null -> a logged OperationResult.Failed.
        var paired = SettingCatalog.All.First(s =>
            s.Detector is null && s.OptionSource is null && s.Numeric is null
            && s.States.Any(st => st.Label == "Enabled") && s.States.Any(st => st.Label == "Disabled")
            && s.Targets.OfType<RegTarget>().Any());
        SetupSettingInRegistry(paired.Id);

        await _service.ApplySettingAsync(new ApplySettingRequest { SettingId = paired.Id, Enable = true });

        _mockStateWriter.Invocations.Should().NotBeEmpty("the paired toggle must apply through the state writer");
        // The engine performs no restarts itself, so the funnel must still run process/service restarts -
        // else a setting that restarts Explorer would not take effect.
        _mockRestart.Verify(r => r.HandleProcessAndServiceRestartsAsync(It.IsAny<Setting>()), Times.Once);
    }

    [Theory]
    [InlineData(22631)] // Windows 11: only the HiddenByDefault write target is live; the Win10 key-delete is gated out.
    [InlineData(19045)] // Windows 10: only the KeyExists key-delete target is live; the Win11 write is gated out.
    public async Task ApplySettingAsync_MergedThisPcToggle_AppliesOnlyTheLiveBuildTarget(int buildNumber)
    {
        // A merged This PC folder setting has two build-gated targets on the SAME key - a Windows-11
        // HiddenByDefault DWORD write and a Windows-10 key existence/delete. The funnel must thread the live build
        // so ApplyPlanBuilder emits ONLY this OS's target. (With build=null BOTH would fire.)
        const string id = "explorer-customization-thispc-folder-desktop";
        SettingCatalog.All.Should().Contain(s => s.Id == id, "the merged This PC setting must exist for this test");
        SetupSettingInRegistry(id);
        _mockVersion.Setup(v => v.GetWindowsBuildNumber()).Returns(buildNumber);

        // Disabled: Win11 writes HiddenByDefault=1; Win10 deletes the namespace key (KeyExists = Absent).
        await _service.ApplySettingAsync(new ApplySettingRequest { SettingId = id, Enable = false });

        bool isWin11 = buildNumber >= 22000;
        _mockStateWriter.Verify(
            w => w.WriteRegistry(It.IsAny<RegTarget>(), It.IsAny<string>(), It.IsAny<object>()),
            isWin11 ? Times.AtLeastOnce() : Times.Never());
        _mockStateWriter.Verify(
            w => w.DeleteRegistry(It.IsAny<RegTarget>(), It.IsAny<string>()),
            isWin11 ? Times.Never() : Times.AtLeastOnce());
    }

    [Fact]
    public async Task ApplySettingAsync_PairedSetting_FiresForwardRequiresFollowOn()
    {
        // Applying a paired setting whose target state declares a Requires Link must, via
        // RelationshipResolver.ResolveForward, recursively apply the prerequisite when it is not already
        // met. Asserted by the prerequisite's SettingAppliedEvent being published (proof the follow-on apply ran).
        var owner = SettingCatalog.All.FirstOrDefault(s =>
            s.Detector is null && s.OptionSource is null
            && s.States.Any(st => st.Label == "Disabled")
            && s.States.Any(st => st.Label == "Enabled" && st.Links.Any(l => l.Kind == LinkKind.Requires)));
        Assert.NotNull(owner);
        var req = owner!.States.First(st => st.Label == "Enabled").Links.First(l => l.Kind == LinkKind.Requires);
        Assert.Contains(SettingCatalog.All, s => s.Id == req.OtherId);

        SetupSettingInRegistry(owner.Id);
        SetupSettingInRegistry(req.OtherId);
        // Prerequisite detected as NOT in its required state -> ResolveForward must fire it.
        _mockCatalogDetection
            .Setup(d => d.DetectAsync(It.IsAny<IReadOnlyCollection<Setting>>()))
            .ReturnsAsync(new Dictionary<string, CatalogDetectionResult>
            {
                [req.OtherId] = new CatalogDetectionResult
                {
                    StateLabel = req.RequiredState == "Enabled" ? "Disabled" : "Enabled",
                    Detected = true,
                },
            });

        await _service.ApplySettingAsync(new ApplySettingRequest { SettingId = owner.Id, Enable = true });

        _mockEventBus.Verify(e => e.Publish(It.Is<SettingAppliedEvent>(x => x.SettingId == req.OtherId)), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_ValidSetting_ReturnsSuccess()
    {
        var id = RealPairedToggleId();
        SetupSettingInRegistry(id);

        var result = await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = id,
            Enable = true,
            SkipValuePrerequisites = true,
        });

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ApplySettingAsync_ValidSetting_PublishesEvent()
    {
        SetupSettingInRegistry("test-setting");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "test-setting",
            Enable = true,
        });

        _mockEventBus.Verify(e => e.Publish(It.Is<SettingAppliedEvent>(
            evt => evt.SettingId == "test-setting")), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_SettingNotFound_ThrowsArgumentException()
    {
        _mockSettingsRegistry.Setup(r => r.GetById("missing", It.IsAny<CatalogScope>()))
            .Returns((Setting?)null);

        var action = () => _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "missing",
            Enable = true,
        });

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*missing*not found*");
    }

    [Fact]
    public async Task ApplyRecommendedSettingsForFeatureAsync_DelegatesToApplier()
    {
        await _service.ApplyRecommendedSettingsForFeatureAsync("test-id");

        _mockRecommended.Verify(r => r.ApplyRecommendedSettingsForFeatureAsync(
            "test-id", _service), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_ActionWithApplyRecommended_OneCoalescedRestartForPrimaryPlusRecommended()
    {
        // "One restart per click": the primary Action apply and the recommended batch must
        // run inside a single SuppressRestarts() scope and produce exactly ONE coalesced restart
        // covering the primary action AND every recommended setting.
        // The recommended applier + the coalesced-restart flush are catalog-Setting typed, and SAS builds the
        // restart set from the primary's catalog Setting (renderSetting = Find(settingId)). The test id must be a
        // REAL catalog Action so renderSetting resolves and joins the flush set; GetById returns that same
        // catalog Action Setting, whose Control == Action drives the branch.
        var actionId = SettingCatalog.All.First(s => s.Control == ControlKind.Action).Id;
        SetupSettingInRegistry(actionId);

        var recommended = new Setting { Id = "rec1", Display = new Display { Name = "Rec1", Description = "d" } };
        _mockRecommended
            .Setup(r => r.ApplyRecommendedForFeatureAsync(actionId, It.IsAny<ISettingApplicationService>()))
            .ReturnsAsync(new List<Setting> { recommended });

        // The using-scope needs a real IDisposable back from the mock.
        _mockRestart.Setup(r => r.SuppressRestarts()).Returns(Mock.Of<IDisposable>());

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = actionId,
            Enable = true,
            ApplyRecommended = true,
        });

        _mockRestart.Verify(r => r.SuppressRestarts(), Times.Once);

        // The recommended batch runs through the NON-flushing feature core...
        _mockRecommended.Verify(r => r.ApplyRecommendedForFeatureAsync(
            actionId, _service), Times.Once);
        // ...and the standalone flushing entry is NOT used on this path (would double-restart).
        _mockRecommended.Verify(r => r.ApplyRecommendedSettingsForFeatureAsync(
            It.IsAny<string>(), It.IsAny<ISettingApplicationService>()), Times.Never);

        _mockRestart.Verify(r => r.FlushCoalescedRestartsAsync(
            It.Is<IEnumerable<Setting>>(list =>
                list.Any(s => s.Id == actionId) && list.Any(s => s.Id == "rec1"))),
            Times.Once);
    }

    // The confirmation checkbox: on a setting with NO special handler it means
    // "also apply this feature's recommended settings"

    [Fact]
    public async Task ApplySettingAsync_CheckboxResultWithNoSpecialHandler_AppliesRecommendedForTheFeature()
    {
        // The config-import path (ConfigurationApplicationBridgeService) passes the confirmation result as
        // CheckboxResult ALONE - no ApplyRecommended - so if only a special handler read CheckboxResult, on a
        // setting with none the tick would do nothing at all. A real catalog Action with no handler registered
        // must route it to the recommended-for-feature applier.
        var actionId = SettingCatalog.All.First(s => s.Control == ControlKind.Action).Id;
        SetupSettingInRegistry(actionId);

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = actionId,
            Enable = true,
            CheckboxResult = true,
        });

        _mockRecommended.Verify(r => r.ApplyRecommendedSettingsForFeatureAsync(
            actionId, _service), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_NoCheckboxResult_DoesNotApplyRecommended()
    {
        // The other half of the same rule: an unticked box is the default, so the SAME apply must leave the
        // feature's other settings alone. Without this the rule would fire on every Action apply.
        var actionId = SettingCatalog.All.First(s => s.Control == ControlKind.Action).Id;
        SetupSettingInRegistry(actionId);

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = actionId,
            Enable = true,
            CheckboxResult = false,
        });

        _mockRecommended.Verify(r => r.ApplyRecommendedSettingsForFeatureAsync(
            It.IsAny<string>(), It.IsAny<ISettingApplicationService>()), Times.Never);
    }

    [Fact]
    public async Task ApplySettingAsync_CheckboxResultOnSpecialHandledSetting_DoesNotApplyRecommended()
    {
        // The load-bearing guard. theme-mode-windows' checkbox means "also change the wallpaper" and its
        // handler owns that meaning, so the generic rule must never fire for a setting that HAS a handler -
        // otherwise a wallpaper opt-in would silently apply a whole feature's recommended settings. The
        // handler here DECLINES (returns false) so the funnel falls THROUGH to the generic apply, which is
        // the path where the guard is the only thing standing between the checkbox and the applier; a handler
        // that accepts returns long before this point.
        var actionId = SettingCatalog.All.First(s => s.Control == ControlKind.Action).Id;
        SetupSettingInRegistry(actionId);

        var handler = new Mock<ISpecialSettingHandler>();
        handler
            .Setup(h => h.TryApplySpecialSettingAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>(), It.IsAny<ISettingApplicationService>()))
            .ReturnsAsync(false);
        _mockSpecialHandlerRegistry.Setup(r => r.TryGet(actionId)).Returns(handler.Object);

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = actionId,
            Enable = true,
            CheckboxResult = true,
        });

        _mockRecommended.Verify(r => r.ApplyRecommendedSettingsForFeatureAsync(
            It.IsAny<string>(), It.IsAny<ISettingApplicationService>()), Times.Never);
    }

    [Fact]
    public async Task ApplySettingAsync_ActionWithBothCheckboxFlags_AppliesRecommendedExactlyOnce()
    {
        // The live UI button path sets ApplyRecommended AND CheckboxResult from the SAME checkbox
        // (SettingItemViewModel.RunActionAsync), so the coalesced-restart branch has already applied the
        // recommended settings by the time the checkbox rule is reached. It must not apply them again: two
        // passes would mean two restart flushes and a second run of every recommended setting.
        var actionId = SettingCatalog.All.First(s => s.Control == ControlKind.Action).Id;
        SetupSettingInRegistry(actionId);
        _mockRestart.Setup(r => r.SuppressRestarts()).Returns(Mock.Of<IDisposable>());
        _mockRecommended
            .Setup(r => r.ApplyRecommendedForFeatureAsync(actionId, It.IsAny<ISettingApplicationService>()))
            .ReturnsAsync(new List<Setting>());

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = actionId,
            Enable = true,
            CheckboxResult = true,
            ApplyRecommended = true,
        });

        _mockRecommended.Verify(r => r.ApplyRecommendedForFeatureAsync(
            actionId, _service), Times.Once);
        _mockRecommended.Verify(r => r.ApplyRecommendedSettingsForFeatureAsync(
            It.IsAny<string>(), It.IsAny<ISettingApplicationService>()), Times.Never);
    }

    [Fact]
    public async Task ApplySettingAsync_UnpairedSetting_ResolverReturnsNull_PropagatesFailedResult()
    {
        // An unpaired id (no catalog peer) resolves to a null plan; the funnel returns a failed OperationResult
        // rather than dereferencing it. The failure message names the setting.
        SetupSettingInRegistry("fail-setting");

        var result = await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "fail-setting",
            Enable = true,
        });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("fail-setting");
    }

    [Fact]
    public async Task ApplySettingAsync_UnpairedSetting_Fails_StillPublishesEvent()
    {
        // Even on a failed apply the event is published so listeners re-read actual system state.
        SetupSettingInRegistry("fail-event");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "fail-event",
            Enable = true,
        });

        _mockEventBus.Verify(e => e.Publish(It.Is<SettingAppliedEvent>(
            evt => evt.SettingId == "fail-event")), Times.Once);
    }

    // Change history (#367): record setting changes before → after

    [Fact]
    public async Task ApplySettingAsync_ToggleSuccess_LogsChangeHistoryEntry()
    {
        // Real paired toggle so the apply succeeds and the receipt is reached; GetById returns the real toggle Setting.
        var id = RealPairedToggleId();
        SetupSettingInRegistry(id);

        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [id] = new SettingStateResult { Success = true, IsEnabled = false },
            });

        _mockLocalization.Setup(l => l.GetString("Template_EnabledDisabled_Option_0")).Returns("Disabled");
        _mockLocalization.Setup(l => l.GetString("Template_EnabledDisabled_Option_1")).Returns("Enabled");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = id,
            Enable = true,
            SkipValuePrerequisites = true,
        });

        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(), "Disabled", "Enabled"), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_BeforeEqualsAfter_DoesNotLog()
    {
        // Real paired toggle so the apply succeeds and the receipt is reached; before==after suppresses the entry.
        var id = RealPairedToggleId();
        SetupSettingInRegistry(id);

        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [id] = new SettingStateResult { Success = true, IsEnabled = true },
            });

        _mockLocalization.Setup(l => l.GetString("Template_EnabledDisabled_Option_0")).Returns("Disabled");
        _mockLocalization.Setup(l => l.GetString("Template_EnabledDisabled_Option_1")).Returns("Enabled");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = id,
            Enable = true,
            SkipValuePrerequisites = true,
        });

        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ApplySettingAsync_ChangeHistoryThrows_ApplyStillSucceeds()
    {
        // Real paired toggle so the apply succeeds; a throwing change-history write is swallowed, apply stays Success.
        var id = RealPairedToggleId();
        SetupSettingInRegistry(id);

        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [id] = new SettingStateResult { Success = true, IsEnabled = false },
            });

        _mockLocalization.Setup(l => l.GetString("Template_EnabledDisabled_Option_0")).Returns("Disabled");
        _mockLocalization.Setup(l => l.GetString("Template_EnabledDisabled_Option_1")).Returns("Enabled");

        _mockChangeHistory
            .Setup(h => h.LogSettingChange(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("history write blew up"));

        var result = await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = id,
            Enable = true,
            SkipValuePrerequisites = true,
        });

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ApplySettingAsync_UnpairedSetting_Fails_DoesNotLogChangeHistory()
    {
        // A failed apply (unpaired id -> null plan) returns before the change-history receipt, so nothing is
        // logged even though a before-state is available.
        SetupSettingInRegistry("fail-no-history");

        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["fail-no-history"] = new SettingStateResult { Success = true, IsEnabled = false },
            });

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "fail-no-history",
            Enable = true,
        });

        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockChangeHistory.Verify(h => h.LogSettingAction(
            It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    // Change history (#367): power AC/DC values + option labels render human-readable

    [Fact]
    public async Task ApplySettingAsync_SelectionWithLocalizationKeyDisplayName_RendersLocalizedLabel()
    {
        // Power-setting option state labels ARE localization keys. The receipt must localize the key, not print
        // the raw "Template_..." string. Rendering reads the REAL catalog (power-display-timeout: state 1's label
        // is Template_TimeIntervals_Option_1, an IsLocalizationKey), so mock that real key.
        SetupSettingInRegistry(PowerCfgSelectionId);

        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_1")).Returns("Enabled-ish label");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = PowerCfgSelectionId,
            Enable = true,
            SkipValuePrerequisites = true,
            Value = 1,
        });

        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), "Enabled-ish label"), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_SelectionAcDcTuple_RendersAcDcLabels()
    {
        // Config-import Selection AC/DC values arrive as a (acIndex, dcIndex) ValueTuple. Rendering reads the REAL
        // catalog (power-display-timeout: Template_TimeIntervals_Option_N state labels).
        SetupSettingInRegistry(PowerCfgSelectionId);

        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_0")).Returns("Never");
        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_1")).Returns("4 minutes");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = PowerCfgSelectionId,
            Enable = true,
            SkipValuePrerequisites = true,
            Value = (0, 1),
        });

        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), "AC: Never, DC: 4 minutes"), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_SelectionAcDcDictionary_RendersAcDcLabels()
    {
        // UI / recommended Selection AC/DC values arrive as a dict of option indices. Rendering reads the REAL
        // catalog (power-display-timeout: Template_TimeIntervals_Option_N state labels).
        SetupSettingInRegistry(PowerCfgSelectionId);

        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_0")).Returns("Never");
        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_1")).Returns("4 minutes");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = PowerCfgSelectionId,
            Enable = true,
            SkipValuePrerequisites = true,
            Value = new Dictionary<string, object?> { ["ACValue"] = 0, ["DCValue"] = 1 },
        });

        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), "AC: Never, DC: 4 minutes"), Times.Once);
    }

    // The pair matters: if the "already exists" test stood alone it would pass even when the cascade never
    // fires for an unrelated reason, and prove nothing.
    [Fact]
    public async Task ApplySettingAsync_WinhancePlanDoesNotExistYet_StampsRecommendedSettings()
    {
        SetupSettingInRegistry(SettingIds.PowerPlanSelection);

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = SettingIds.PowerPlanSelection,
            Enable = true,
            SkipValuePrerequisites = true,
            Value = PowerPlanCatalog.WinhancePowerPlanGuid,
        });

        _mockRecommended.Verify(r => r.ApplyRecommendedSettingsForFeatureAsync(
            SettingIds.PowerPlanSelection, It.IsAny<ISettingApplicationService>()), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_WinhancePlanAlreadyExists_LeavesItsSettingsAlone()
    {
        SetupSettingInRegistry(SettingIds.PowerPlanSelection);
        _mockPowerQuery
            .Setup(p => p.GetAvailablePowerPlansAsync())
            .ReturnsAsync(new List<PowerPlan>
            {
                new() { Guid = PowerPlanCatalog.WinhancePowerPlanGuid, Name = "Winhance Power Plan" },
            });

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = SettingIds.PowerPlanSelection,
            Enable = true,
            SkipValuePrerequisites = true,
            Value = PowerPlanCatalog.WinhancePowerPlanGuid,
        });

        // Switching back to a plan that already exists must not re-stamp it: that would silently revert any
        // adjustment the user made while on it.
        _mockRecommended.Verify(r => r.ApplyRecommendedSettingsForFeatureAsync(
            It.IsAny<string>(), It.IsAny<ISettingApplicationService>()), Times.Never);
    }

    [Fact]
    public async Task ApplySettingAsync_PowerPlanShape_RendersJustTheName()
    {
        // The power-plan after-value is a dict with Guid + Name keys; the receipt shows the Name.
        SetupSettingInRegistry(SettingIds.PowerPlanSelection);

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = SettingIds.PowerPlanSelection,
            Enable = true,
            SkipValuePrerequisites = true,
            Value = new Dictionary<string, object?>
            {
                ["Guid"] = "11111111-2222-3333-4444-555555555555",
                ["Name"] = "Winhance Power Plan",
            },
        });

        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), "Winhance Power Plan"), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_NumericRangePowerCfg_ConvertsBeforeStateToDisplayUnitsWithSuffix()
    {
        // Before-state AC/DC are SYSTEM units (seconds). For a Minutes-unit PowerCfg setting,
        // 600s → 10 min. The before must render in display units so it matches the after format.
        // Both before and after must carry the unit suffix (per-value) so no-op detection works.
        SetupSettingInRegistry(PowerCfgNumericId);

        _mockLocalization.Setup(l => l.GetString("Common_Unit_Minutes")).Returns("Minutes");

        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [PowerCfgNumericId] = new SettingStateResult
                {
                    Success = true,
                    IsEnabled = true,
                    AcValue = 0, DcValue = 600,
                },
            });

        // After-value: display-unit AC/DC dict that differs from the before so an entry is logged.
        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = PowerCfgNumericId,
            Enable = true,
            Value = new Dictionary<string, object?> { ["ACValue"] = 5, ["DCValue"] = 15 },
        });

        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(),
            "AC: 0 Minutes, DC: 10 Minutes",
            "AC: 5 Minutes, DC: 15 Minutes"), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_NumericRangePowerCfgNoChange_DoesNotLog()
    {
        // Unchanged NumericRange PowerCfg setting: before and after must produce byte-identical
        // strings (both include the unit suffix) so the no-op suppression fires and no receipt
        // entry is logged.
        SetupSettingInRegistry(PowerCfgNumericId);

        _mockLocalization.Setup(l => l.GetString("Common_Unit_Minutes")).Returns("Minutes");

        // Before-state: 0s AC (= 0 min), 600s DC (= 10 min) — same values as the after.
        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [PowerCfgNumericId] = new SettingStateResult
                {
                    Success = true,
                    IsEnabled = true,
                    AcValue = 0, DcValue = 600,
                },
            });

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = PowerCfgNumericId,
            Enable = true,
            Value = new Dictionary<string, object?> { ["ACValue"] = 0, ["DCValue"] = 10 },
        });

        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ApplySettingAsync_NumericRangePowerCfgPercentUnit_RendersPercentSuffix()
    {
        // Percent-unit NumericRange PowerCfg setting -- the "%" unit has no localization key and is passed through
        // raw. Rendering reads the REAL catalog (processor-min-state: Numeric.Units="%"). Before (80%, 100%)
        // changes to (60%, 80%). GetById returns the real processor-min-state Setting; the receipt reads its live catalog data.
        SetupSettingInRegistry(PowerCfgPercentNumericId);

        // "%" has no Common_Unit_* key — ResolveLocalized returns null for a miss-marker; leave
        // mock at default (key echo). The switch default returns the raw "%" directly.
        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [PowerCfgPercentNumericId] = new SettingStateResult
                {
                    Success = true,
                    IsEnabled = true,
                    // % unit is 1:1 — system value == display value.
                    AcValue = 80, DcValue = 100,
                },
            });

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = PowerCfgPercentNumericId,
            Enable = true,
            Value = new Dictionary<string, object?> { ["ACValue"] = 60, ["DCValue"] = 80 },
        });

        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(),
            "AC: 80 %, DC: 100 %",
            "AC: 60 %, DC: 80 %"), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_SelectionPowerCfgNoChange_DoesNotLog()
    {
        // PowerCfg Separate Selection setting. Rendering reads the REAL catalog (power-display-timeout, whose
        // Set["Power"] value 0 -> option 0). Before-state raw AC/DC = 0 -> option 0. A config import re-applying
        // the SAME state arrives as a (0, 0) ValueTuple. Before "AC: Never, DC: Never" must equal after
        // byte-for-byte so no phantom receipt entry is logged.
        SetupSettingInRegistry(PowerCfgSelectionId);

        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_0")).Returns("Never");

        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [PowerCfgSelectionId] = new SettingStateResult
                {
                    Success = true,
                    IsEnabled = true,
                    AcValue = 0, DcValue = 0,
                },
            });

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = PowerCfgSelectionId,
            Enable = true,
            Value = (0, 0),
        });

        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ApplySettingAsync_SelectionPowerCfgRealChange_LogsBeforeAndAfter()
    {
        // Same setting; DC actually changes (option 0 -> option 1). The before renders from the raw system values
        // (0 -> option 0), the after from the (0, 1) ValueTuple, both in AC/DC label shape. Rendering reads the
        // REAL catalog (power-display-timeout: Template_TimeIntervals_Option_N).
        SetupSettingInRegistry(PowerCfgSelectionId);

        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_0")).Returns("Never");
        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_1")).Returns("4 minutes");

        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [PowerCfgSelectionId] = new SettingStateResult
                {
                    Success = true,
                    IsEnabled = true,
                    AcValue = 0, DcValue = 0,
                },
            });

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = PowerCfgSelectionId,
            Enable = true,
            Value = (0, 1),
        });

        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(), "AC: Never, DC: Never", "AC: Never, DC: 4 minutes"), Times.Once);
    }

    // #367: battery-less machines render AC-only (no phantom DC) +
    //       a no-match raw PowerCfg value renders Custom, never index-by-raw

    [Fact]
    public async Task ApplySettingAsync_NoBatterySelection_RendersAcOnly()
    {
        // Battery-less desktop: only the AC dropdown exists and PowerCfgApplier skips all DC writes.
        // The receipt must show "AC: <label>" only -- no ", DC: ..." phantom. Rendering reads the REAL catalog
        // (power-display-timeout: Set["Power"] raw 0 -> option 0; apply index 2 -> option 2).
        _mockHardware.Setup(h => h.HasBattery()).Returns(false);

        SetupSettingInRegistry(PowerCfgSelectionId);

        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_0")).Returns("Never");
        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_2")).Returns("9 minutes");

        // Before-state raw AC = 0 -> option 0 "Never". DC raw is garbage on a battery-less machine.
        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [PowerCfgSelectionId] = new SettingStateResult
                {
                    Success = true,
                    IsEnabled = true,
                    AcValue = 0, DcValue = 999999,
                },
            });

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = PowerCfgSelectionId,
            Enable = true,
            Value = (2, 0),
        });

        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(), "AC: Never", "AC: 9 minutes"), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_NoBatteryAcUnchanged_DoesNotLog()
    {
        // KEY regression: on a battery-less desktop, the AC value is unchanged but the (irrelevant) DC garbage
        // differs. The receipt must suppress the entry -- DC garbage must NEVER create a phantom change. Before
        // "AC: Never" == after "AC: Never". Rendering reads the REAL catalog (power-display-timeout: raw 0 -> opt 0).
        _mockHardware.Setup(h => h.HasBattery()).Returns(false);

        SetupSettingInRegistry(PowerCfgSelectionId);

        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_0")).Returns("Never");

        // AC raw 0 -> option 0 "Never". DC raw is garbage and DIFFERENT from the applied DC index.
        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [PowerCfgSelectionId] = new SettingStateResult
                {
                    Success = true,
                    IsEnabled = true,
                    AcValue = 0, DcValue = 12345,
                },
            });

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = PowerCfgSelectionId,
            Enable = true,
            Value = (0, 1),
        });

        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ApplySettingAsync_FallbackRawValueWithNoMatchingOption_RendersCustomNotIndexedLabel()
    {
        // Regression for the fallback-as-index bug: a raw PowerCfg DC value that matches NO
        // option must render "Custom" -- NOT States[rawValue]. Rendering reads the REAL catalog
        // (power-display-timeout: Set["Power"] values 0,60,120,180,300,...; raw 1 matches none). Battery present.
        SetupSettingInRegistry(PowerCfgSelectionId);

        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_0")).Returns("Never");
        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_2")).Returns("5 minutes");
        _mockLocalization.Setup(l => l.GetString("Common_CustomState")).Returns("Custom");

        // AC raw 0 -> option 0 "Never". DC raw 1 matches NO option's Set["Power"] value.
        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [PowerCfgSelectionId] = new SettingStateResult
                {
                    Success = true,
                    IsEnabled = true,
                    AcValue = 0, DcValue = 1,
                },
            });

        // After applies (0, 2) so an entry is logged and we can assert the BEFORE rendering.
        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = PowerCfgSelectionId,
            Enable = true,
            Value = (0, 2),
        });

        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(), "AC: Never, DC: Custom", "AC: Never, DC: 5 minutes"), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_BatteryStateUnknown_AppliesAndRendersBothComponents()
    {
        // Unknown (null) is what the service reports when the WMI probe fails. This caller defaults it to
        // true so the receipt renders BOTH AC and DC rather than hiding the DC half.
        _mockHardware.Setup(h => h.HasBattery()).Returns((bool?)null);

        SetupSettingInRegistry(PowerCfgSelectionId);

        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_0")).Returns("Never");
        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_1")).Returns("4 minutes");

        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [PowerCfgSelectionId] = new SettingStateResult
                {
                    Success = true,
                    IsEnabled = true,
                    AcValue = 0, DcValue = 0,
                },
            });

        var result = await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = PowerCfgSelectionId,
            Enable = true,
            Value = (0, 1),
        });

        result.Success.Should().BeTrue();
        _mockChangeHistory.Verify(h => h.LogSettingChange(
            It.IsAny<string>(), It.IsAny<string?>(), "AC: Never, DC: Never", "AC: Never, DC: 4 minutes"), Times.Once);
    }

    // The funnel re-applies Winhance-recommended power settings after a successful switch TO the Winhance
    // power plan. The mocked handler registry returns null, so the funnel exercises the engine path here.

    [Fact]
    public async Task ApplySettingAsync_WinhancePowerPlanApplied_ReappliesRecommendedPowerSettings()
    {
        // power-plan-selection is paired in the live catalog (OptionSource), so the engine builds a
        // PowerPlanActivateOp from the GUID value; make the writer's activate succeed so operationResult.Success.
        SetupSettingInRegistry(SettingIds.PowerPlanSelection);
        _mockStateWriter.Setup(w => w.ActivatePowerPlan(It.IsAny<string>())).Returns(true);
        _mockRecommended
            .Setup(r => r.ApplyRecommendedSettingsForFeatureAsync(It.IsAny<string>(), It.IsAny<ISettingApplicationService>()))
            .Returns(Task.CompletedTask);

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = SettingIds.PowerPlanSelection,
            Enable = true,
            Value = PowerPlanCatalog.WinhancePowerPlanGuid,
        });

        _mockRecommended.Verify(r => r.ApplyRecommendedSettingsForFeatureAsync(
            SettingIds.PowerPlanSelection, _service), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_WinhancePowerPlanDuringConfigImportWithPowerValues_SkipsRecommendedReapply()
    {
        // An active config import that supplies its own individual power values is the source of truth -> skip.
        SetupSettingInRegistry(SettingIds.PowerPlanSelection);
        _mockStateWriter.Setup(w => w.ActivatePowerPlan(It.IsAny<string>())).Returns(true);
        _mockConfigImportState.Setup(c => c.IsActive).Returns(true);
        _mockConfigImportState.Setup(c => c.ImportSuppliesPowerValues).Returns(true);

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = SettingIds.PowerPlanSelection,
            Enable = true,
            Value = PowerPlanCatalog.WinhancePowerPlanGuid,
        });

        _mockRecommended.Verify(r => r.ApplyRecommendedSettingsForFeatureAsync(
            It.IsAny<string>(), It.IsAny<ISettingApplicationService>()), Times.Never);
    }

    [Fact]
    public async Task ApplySettingAsync_NonWinhancePowerPlanApplied_DoesNotReapplyRecommended()
    {
        // Switching to Balanced (not the Winhance plan) must NOT trigger the recommended re-apply.
        SetupSettingInRegistry(SettingIds.PowerPlanSelection);
        _mockStateWriter.Setup(w => w.ActivatePowerPlan(It.IsAny<string>())).Returns(true);

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = SettingIds.PowerPlanSelection,
            Enable = true,
            Value = "381b4222-f694-41f0-9685-ff5bb260df2e",
        });

        _mockRecommended.Verify(r => r.ApplyRecommendedSettingsForFeatureAsync(
            It.IsAny<string>(), It.IsAny<ISettingApplicationService>()), Times.Never);
    }

    // Relationship detection is SCOPED. Detecting all 414 catalog settings after every interactive apply
    // is correct but costs ~1-2s a click, and puts power plans and system restore in the log while the
    // user is on the Taskbar page.

    [Fact]
    public async Task ApplySettingAsync_SettingWithNoRelationships_DetectsNothing()
    {
        // Nothing in the catalog relates to this setting: no state of it declares a Link or Controls, no
        // state of any other setting Controls it, and no state of any other setting Links to it. Every
        // resolver is therefore empty for ANY machine reading, so the detection was pure cost.
        var unrelated = SettingCatalog.All.FirstOrDefault(s =>
            s.Detector is null && s.OptionSource is null && s.Numeric is null
            && s.Targets.OfType<RegTarget>().Any()
            && s.States.Any(st => st.Label == "Enabled") && s.States.Any(st => st.Label == "Disabled")
            && s.States.All(st => st.Links.Count == 0 && (st.Controls is null || st.Controls.Count == 0))
            && !SettingCatalog.All.Any(o => o.States.Any(st =>
                st.Controls != null && st.Controls.ContainsKey(s.Id)))
            && !SettingCatalog.All.Any(o => o.States.Any(st => st.Links.Any(l => l.OtherId == s.Id))));
        Assert.NotNull(unrelated);
        SetupSettingInRegistry(unrelated!.Id);

        await _service.ApplySettingAsync(new ApplySettingRequest { SettingId = unrelated.Id, Enable = true });

        _mockCatalogDetection.Verify(
            d => d.DetectAsync(It.IsAny<IReadOnlyCollection<Setting>>()), Times.Never,
            "a setting nothing relates to needs no machine read to resolve its relationships");
    }

    [Fact]
    public async Task ApplySettingAsync_SettingWithARequiresLink_DetectsOnlyTheScopeAndStillFiresTheFollowOn()
    {
        // The other half of the same rule: narrowing the batch must not narrow BEHAVIOUR. A setting whose
        // target state Requires another must still read that other setting's live state and act on it.
        var owner = SettingCatalog.All.FirstOrDefault(s =>
            s.Detector is null && s.OptionSource is null
            && s.States.Any(st => st.Label == "Disabled")
            && s.States.Any(st => st.Label == "Enabled" && st.Links.Any(l => l.Kind == LinkKind.Requires)));
        Assert.NotNull(owner);
        var req = owner!.States.First(st => st.Label == "Enabled").Links.First(l => l.Kind == LinkKind.Requires);
        Assert.Contains(SettingCatalog.All, s => s.Id == req.OtherId);

        SetupSettingInRegistry(owner.Id);
        SetupSettingInRegistry(req.OtherId);

        IReadOnlyCollection<Setting>? scope = null;
        _mockCatalogDetection
            .Setup(d => d.DetectAsync(It.IsAny<IReadOnlyCollection<Setting>>()))
            .Callback((IReadOnlyCollection<Setting> batch) => scope ??= batch)
            .ReturnsAsync(new Dictionary<string, CatalogDetectionResult>
            {
                [req.OtherId] = new CatalogDetectionResult
                {
                    StateLabel = req.RequiredState == "Enabled" ? "Disabled" : "Enabled",
                    Detected = true,
                },
            });

        await _service.ApplySettingAsync(new ApplySettingRequest { SettingId = owner.Id, Enable = true });

        scope.Should().NotBeNull("a Requires link can only be resolved by reading the prerequisite's state");
        scope!.Should().Contain(s => s.Id == req.OtherId, "the prerequisite is exactly what the resolver reads");
        scope!.Count.Should().BeLessThan(SettingCatalog.All.Count,
            "one relationship must no longer cost a whole-catalog detection");
        _mockEventBus.Verify(e => e.Publish(It.Is<SettingAppliedEvent>(x => x.SettingId == req.OtherId)), Times.Once);
    }
}

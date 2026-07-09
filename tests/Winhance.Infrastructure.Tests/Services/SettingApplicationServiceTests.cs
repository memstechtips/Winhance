using System.Linq;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SettingApplicationServiceTests
{
    private readonly Mock<ICompatibleSettingsRegistry> _mockSettingsRegistry = new();
    private readonly Mock<ISpecialSettingHandlerRegistry> _mockSpecialHandlerRegistry = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly Mock<IRecommendedSettingsApplier> _mockRecommended = new();
    private readonly Mock<IProcessRestartManager> _mockRestart = new();
    private readonly Mock<IChangeHistoryService> _mockChangeHistory = new();
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IHardwareDetectionService> _mockHardware = new();
    private readonly Mock<IStateWriter> _mockStateWriter = new();
    private readonly Mock<IWindowsVersionService> _mockVersion = new();
    private readonly Mock<ICatalogDetectionService> _mockCatalogDetection = new();
    private readonly Mock<ICatalogSettingStateProvider> _mockSettingStateProvider = new();
    private readonly Mock<IConfigImportState> _mockConfigImportState = new();
    private readonly SettingApplicationService _service;

    public SettingApplicationServiceTests()
    {
        // Default: machine HAS a battery, so every existing AC/DC test keeps its current
        // "AC: x, DC: y" expectations. No-battery tests override this per-test.
        _mockHardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(true);

        // Default: every IStateWriter write SUCCEEDS, so a paired setting routed through the new ApplyExecutor
        // reports OperationResult.Succeeded. The old ISettingOperationExecutor "black-box succeeds" fallback for
        // unpaired ids is gone; tests asserting a failure use an unpaired (null-plan) id instead of a mock override.
        _mockStateWriter.SetReturnsDefault(true);

        // Default: GetString echoes the key back. A key-echo is NOT the "[{key}]" miss-marker, so by default
        // ResolveLocalized treats every key as a HIT returning the key text; tests that assert on display strings
        // set explicit returns.
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        // Default: a Windows 11 build. Build-agnostic settings (no Target.AppliesTo) are unaffected by this value,
        // so every existing test is unchanged; build-gated tests override it per-test.
        _mockVersion.Setup(v => v.GetWindowsBuildNumber()).Returns(22631);
        _mockVersion.Setup(v => v.GetWindowsBuildRevision()).Returns(0);

        // Default: the new catalog detection engine resolves nothing. With an empty dictionary, currentStateOf
        // returns null for every id, so the relationship resolvers fire no follow-on applies - paired settings
        // with no live-state-dependent relationships stay a no-op (the existing funnel tests assert nothing extra).
        _mockCatalogDetection
            .Setup(d => d.DetectAsync(It.IsAny<IReadOnlyCollection<Setting>>()))
            .ReturnsAsync(new Dictionary<string, CatalogDetectionResult>());

        // Default: the full-state provider (paired before-state read at the change-history receipt) finds nothing,
        // mirroring the old discovery default above so existing tests keep their "no before-state" expectations.
        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>());

        _service = new SettingApplicationService(
            _mockSettingsRegistry.Object, _mockSpecialHandlerRegistry.Object,
            _mockLog.Object,
            _mockEventBus.Object, _mockRecommended.Object, _mockRestart.Object,
            _mockChangeHistory.Object, _mockLocalization.Object,
            _mockHardware.Object, _mockStateWriter.Object, _mockVersion.Object,
            _mockCatalogDetection.Object, _mockSettingStateProvider.Object, _mockConfigImportState.Object);
    }

    private static SettingDefinition CreateSetting(string id) => new()
    {
        Id = id,
        Name = $"Setting {id}",
        Description = $"Description for {id}",
    };

    private void SetupSettingInRegistry(string settingId, string featureId = "TestDomain")
    {
        var setting = CreateSetting(settingId);
        _mockSettingsRegistry.Setup(r => r.GetById(settingId)).Returns(setting);
        _mockSettingsRegistry.Setup(r => r.GetFeatureIdForSetting(settingId)).Returns(featureId);
        _mockSettingsRegistry.Setup(r => r.GetFilteredSettings(featureId))
            .Returns(new[] { setting });
    }

    // Real catalog settings whose Control routes a given value shape through the new engine to a SUCCEEDING plan.
    // The receipt-rendering tests register a FAKE def under these ids purely to satisfy the funnel wiring (GetById +
    // feature lookup); routing AND the change-history receipt rendering both read the LIVE catalog Setting (the def's
    // options/powercfg are inert). So each test mocks the REAL catalog state labels and picks raw/apply values against
    // the real states. SkipValuePrerequisites isolates the receipt from relationship cascades.
    private const string PowerCfgSelectionId = "power-display-timeout"; // powercfg-Separate selection, 16 Template_TimeIntervals_Option_N states (Set["Power"] = 0,60,120,180,300,600,...)
    private const string PowerCfgNumericId = "power-harddisk-timeout";  // powercfg-Separate numeric slider, Minutes
    private const string PowerCfgPercentNumericId = "processor-min-state"; // powercfg-Separate numeric slider, "%" units

    // A real catalog plain-registry toggle (Enabled/Disabled states, a RegTarget). Applying it routes through the
    // new engine to a plan the (defaulted-to-success) writer completes, so the funnel returns Success. The fake def
    // registered under this id renders as a Toggle (InputType defaults to Toggle) for the change-history receipt.
    private static string RealPairedToggleId() => SettingCatalog.All.First(s =>
        s.Detector is null && s.OptionSource is null && s.Numeric is null
        && s.States.Any(st => st.Label == "Enabled") && s.States.Any(st => st.Label == "Disabled")
        && s.Targets.OfType<RegTarget>().Any()).Id;

    [Fact]
    public async Task ApplySettingAsync_PairedPlainToggle_RoutesThroughNewWriter_BypassingOldExecutor()
    {
        // A real catalog plain registry toggle (no custom detector / dynamic options / numeric, with both an Enabled
        // and a Disabled state) applies through the new ApplyExecutor + IStateWriter. Unpaired/fake ids (the null-plan
        // tests below) now resolve to null -> a logged OperationResult.Failed; there is no old-executor fallback.
        var paired = SettingCatalog.All.First(s =>
            s.Detector is null && s.OptionSource is null && s.Numeric is null
            && s.States.Any(st => st.Label == "Enabled") && s.States.Any(st => st.Label == "Disabled")
            && s.Targets.OfType<RegTarget>().Any());
        SetupSettingInRegistry(paired.Id);

        await _service.ApplySettingAsync(new ApplySettingRequest { SettingId = paired.Id, Enable = true });

        _mockStateWriter.Invocations.Should().NotBeEmpty("the paired toggle must apply through the new writer");
        // The new engine performs no restarts itself, so the funnel must still run process/service restarts
        // (the old executor did this as its final step) - else a setting that restarts Explorer would not take effect.
        _mockRestart.Verify(r => r.HandleProcessAndServiceRestartsAsync(It.IsAny<SettingDefinition>()), Times.Once);
    }

    [Theory]
    [InlineData(22631)] // Windows 11: only the HiddenByDefault write target is live; the Win10 key-delete is gated out.
    [InlineData(19045)] // Windows 10: only the KeyExists key-delete target is live; the Win11 write is gated out.
    public async Task ApplySettingAsync_MergedThisPcToggle_AppliesOnlyTheLiveBuildTarget(int buildNumber)
    {
        // Phase 6.5: a merged This PC folder setting has two build-gated targets on the SAME key - a Windows-11
        // HiddenByDefault DWORD write and a Windows-10 key existence/delete. The funnel must thread the live build
        // so ApplyPlanBuilder emits ONLY this OS's target. (With build=null BOTH would fire - the latent 6.4 bug.)
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
    public async Task ApplySettingAsync_CatalogSettingFilteredOutOnThisOs_ResolvesViaBypassed()
    {
        // Phase 6.5 (Slice 3b): a merged This PC setting imported from a "-win10" config is normalized to its
        // canonical id. On Windows 10 the OLD canonical def is Win11Only -> OS-filtered out, so GetById/
        // GetFeatureIdForSetting MISS. The funnel must fall back to the BYPASSED registry (the catalog has a
        // build-compatible peer) and apply via the new engine's Win10 target instead of dropping/throwing.
        const string id = "explorer-customization-thispc-folder-desktop";
        SettingCatalog.All.Should().Contain(s => s.Id == id);
        var def = CreateSetting(id);
        _mockSettingsRegistry.Setup(r => r.GetById(id)).Returns((SettingDefinition?)null);
        _mockSettingsRegistry.Setup(r => r.GetFeatureIdForSetting(id)).Returns((string?)null);
        _mockSettingsRegistry.Setup(r => r.GetByIdBypassed(id)).Returns(def);
        _mockSettingsRegistry.Setup(r => r.GetFeatureIdForSettingBypassed(id)).Returns("TestDomain");
        _mockSettingsRegistry.Setup(r => r.GetFilteredSettings("TestDomain")).Returns(new[] { def });
        _mockVersion.Setup(v => v.GetWindowsBuildNumber()).Returns(19045); // Windows 10

        var act = async () => await _service.ApplySettingAsync(new ApplySettingRequest { SettingId = id, Enable = false });

        await act.Should().NotThrowAsync();
        // Win10 Disabled -> the KeyExists key-delete fires; the Win11 HiddenByDefault write is gated out.
        _mockStateWriter.Verify(w => w.DeleteRegistry(It.IsAny<RegTarget>(), It.IsAny<string>()), Times.AtLeastOnce());
        _mockStateWriter.Verify(w => w.WriteRegistry(It.IsAny<RegTarget>(), It.IsAny<string>(), It.IsAny<object>()), Times.Never());
    }

    [Fact]
    public async Task ApplySettingAsync_NonCatalogIdMissingFromRegistry_StillThrows()
    {
        // The bypassed fallback is gated on catalog membership: a non-catalog id that misses the filtered registry
        // must NOT be resolved via bypassed (even if bypassed happens to hold it) - it still throws as before.
        const string id = "definitely-not-a-catalog-setting";
        _mockSettingsRegistry.Setup(r => r.GetById(id)).Returns((SettingDefinition?)null);
        _mockSettingsRegistry.Setup(r => r.GetByIdBypassed(id)).Returns(CreateSetting(id));

        var act = async () => await _service.ApplySettingAsync(new ApplySettingRequest { SettingId = id, Enable = true });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ApplySettingAsync_CatalogSettingNotBuildCompatible_DoesNotResolveViaBypassed()
    {
        // The fallback is also build-gated: a catalog setting whose Availability excludes this build must NOT be
        // resolved via bypassed. taskbar-copilot's authored window is [22621, 26099]; build 30000 is outside it.
        const string id = "taskbar-copilot";
        SettingCatalog.All.Should().Contain(s => s.Id == id);
        _mockSettingsRegistry.Setup(r => r.GetById(id)).Returns((SettingDefinition?)null);
        _mockSettingsRegistry.Setup(r => r.GetByIdBypassed(id)).Returns(CreateSetting(id));
        _mockVersion.Setup(v => v.GetWindowsBuildNumber()).Returns(30000); // above copilot's max -> incompatible

        var act = async () => await _service.ApplySettingAsync(new ApplySettingRequest { SettingId = id, Enable = true });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ApplySettingAsync_PairedSetting_FiresForwardRequiresFollowOn()
    {
        // Phase 6.6 Slice 2 (relationship go-live): applying a paired setting whose target state declares a Requires
        // Link must, via RelationshipResolver.ResolveForward, recursively apply the prerequisite when it is not already
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
        // A paired plain toggle routes through the new engine; with the writer succeeding (ctor default), the apply
        // succeeds. (Previously a fake id "succeeded" via the old ISettingOperationExecutor; that fallback is gone.)
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
        _mockSettingsRegistry.Setup(r => r.GetById("missing"))
            .Returns((SettingDefinition?)null);

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
        // Bug A "one restart per click": the primary Action apply and the recommended batch must
        // run inside a single SuppressRestarts() scope and produce exactly ONE coalesced restart
        // covering the primary action AND every recommended setting.
        // Slice 3b: the recommended applier + the coalesced-restart flush are now catalog-Setting typed, and
        // SAS builds the restart set from the primary's catalog Setting (renderSetting = Find(settingId)). Repoint
        // the synthetic Action id onto a REAL catalog Action so renderSetting resolves and joins the flush set;
        // the def (still fed via the old registry) only supplies the InputType.Action gate.
        var actionId = SettingCatalog.All.First(s => s.Control == ControlKind.Action).Id;
        var actionSetting = new SettingDefinition
        {
            Id = actionId,
            Name = "Action Clean",
            Description = "desc",
            InputType = InputType.Action,
        };
        _mockSettingsRegistry.Setup(r => r.GetById(actionId)).Returns(actionSetting);
        _mockSettingsRegistry.Setup(r => r.GetFeatureIdForSetting(actionId)).Returns("TestDomain");
        _mockSettingsRegistry.Setup(r => r.GetFilteredSettings("TestDomain")).Returns(new[] { actionSetting });

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

        // One suppress scope wraps both the primary action and the recommended batch.
        _mockRestart.Verify(r => r.SuppressRestarts(), Times.Once);

        // The recommended batch runs through the NON-flushing feature core...
        _mockRecommended.Verify(r => r.ApplyRecommendedForFeatureAsync(
            actionId, _service), Times.Once);
        // ...and the standalone flushing entry is NOT used on this path (would double-restart).
        _mockRecommended.Verify(r => r.ApplyRecommendedSettingsForFeatureAsync(
            It.IsAny<string>(), It.IsAny<ISettingApplicationService>()), Times.Never);

        // Exactly one coalesced flush, containing the primary action AND the recommended setting.
        _mockRestart.Verify(r => r.FlushCoalescedRestartsAsync(
            It.Is<IEnumerable<Setting>>(list =>
                list.Any(s => s.Id == actionId) && list.Any(s => s.Id == "rec1"))),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Unpaired setting -> resolver returns null -> logged OperationResult.Failed
    // (the old ISettingOperationExecutor fallback that used to make a fake id "succeed" is gone; the success
    //  path is now the new engine, covered by ApplySettingAsync_ValidSetting_ReturnsSuccess)
    // ---------------------------------------------------------------

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

    // ---------------------------------------------------------------
    // Change history (#367): record setting changes before → after
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingAsync_ToggleSuccess_LogsChangeHistoryEntry()
    {
        // Real paired toggle so the apply succeeds and the receipt is reached; the fake def renders as a Toggle.
        var id = RealPairedToggleId();
        SetupSettingInRegistry(id);

        // Before-state: discovery reports the toggle currently disabled.
        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
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

        // Before-state already matches the requested state (enabled -> enable=true).
        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
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
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
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
        // A failed apply (unpaired id -> null plan, no old-executor fallback) returns before the change-history
        // receipt, so nothing is logged even though a before-state is available.
        SetupSettingInRegistry("fail-no-history");

        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
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

    // ---------------------------------------------------------------
    // Change history (#367): power AC/DC values + option labels render human-readable
    // ---------------------------------------------------------------

    private SettingDefinition RegisterSelectionSetting(
        string settingId, IReadOnlyList<ComboBoxOption> options,
        IReadOnlyList<PowerCfgSetting>? powerCfg = null, string featureId = "TestDomain")
    {
        var setting = new SettingDefinition
        {
            Id = settingId,
            Name = $"Setting {settingId}",
            Description = $"Description for {settingId}",
            InputType = InputType.Selection,
            ComboBox = new ComboBoxMetadata { Options = options },
            PowerCfgSettings = powerCfg,
        };
        _mockSettingsRegistry.Setup(r => r.GetById(settingId)).Returns(setting);
        _mockSettingsRegistry.Setup(r => r.GetFeatureIdForSetting(settingId)).Returns(featureId);
        _mockSettingsRegistry.Setup(r => r.GetFilteredSettings(featureId)).Returns(new[] { setting });
        return setting;
    }

    private static ComboBoxOption Opt(string displayName) => new() { DisplayName = displayName };

    [Fact]
    public async Task ApplySettingAsync_SelectionWithLocalizationKeyDisplayName_RendersLocalizedLabel()
    {
        // Power-setting option state labels ARE localization keys. The receipt must localize the key, not print
        // the raw "Template_..." string. Rendering reads the REAL catalog (power-display-timeout: state 1's label
        // is Template_TimeIntervals_Option_1, an IsLocalizationKey), so mock that real key.
        RegisterSelectionSetting(PowerCfgSelectionId, System.Array.Empty<ComboBoxOption>());

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
        RegisterSelectionSetting(PowerCfgSelectionId, System.Array.Empty<ComboBoxOption>());

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
        RegisterSelectionSetting(PowerCfgSelectionId, System.Array.Empty<ComboBoxOption>());

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

    [Fact]
    public async Task ApplySettingAsync_PowerPlanShape_RendersJustTheName()
    {
        // The power-plan after-value is a dict with Guid + Name keys; the receipt shows the Name.
        var options = new[] { Opt("PowerPlan_Custom") };
        RegisterSelectionSetting(SettingIds.PowerPlanSelection, options);

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
        var powerCfg = new[]
        {
            new PowerCfgSetting
            {
                SettingGUIDAlias = "VIDEOIDLE",
                PowerModeSupport = PowerModeSupport.Separate,
                Units = "Minutes",
                RecommendedValueAC = null,
                RecommendedValueDC = null,
                DefaultValueAC = null,
                DefaultValueDC = null,
            }
        };
        var setting = new SettingDefinition
        {
            Id = PowerCfgNumericId,
            Name = "Setting num-power",
            Description = "desc",
            InputType = InputType.NumericRange,
            NumericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 60, Units = "Minutes" },
            PowerCfgSettings = powerCfg,
        };
        _mockSettingsRegistry.Setup(r => r.GetById(PowerCfgNumericId)).Returns(setting);
        _mockSettingsRegistry.Setup(r => r.GetFeatureIdForSetting(PowerCfgNumericId)).Returns("TestDomain");
        _mockSettingsRegistry.Setup(r => r.GetFilteredSettings("TestDomain")).Returns(new[] { setting });

        _mockLocalization.Setup(l => l.GetString("Common_Unit_Minutes")).Returns("Minutes");

        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
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
        var powerCfg = new[]
        {
            new PowerCfgSetting
            {
                SettingGUIDAlias = "VIDEOIDLE",
                PowerModeSupport = PowerModeSupport.Separate,
                Units = "Minutes",
                RecommendedValueAC = null,
                RecommendedValueDC = null,
                DefaultValueAC = null,
                DefaultValueDC = null,
            }
        };
        var setting = new SettingDefinition
        {
            Id = PowerCfgNumericId,
            Name = "Setting num-power-noop",
            Description = "desc",
            InputType = InputType.NumericRange,
            NumericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 60, Units = "Minutes" },
            PowerCfgSettings = powerCfg,
        };
        _mockSettingsRegistry.Setup(r => r.GetById(PowerCfgNumericId)).Returns(setting);
        _mockSettingsRegistry.Setup(r => r.GetFeatureIdForSetting(PowerCfgNumericId)).Returns("TestDomain");
        _mockSettingsRegistry.Setup(r => r.GetFilteredSettings("TestDomain")).Returns(new[] { setting });

        _mockLocalization.Setup(l => l.GetString("Common_Unit_Minutes")).Returns("Minutes");

        // Before-state: 0s AC (= 0 min), 600s DC (= 10 min) — same values as the after.
        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [PowerCfgNumericId] = new SettingStateResult
                {
                    Success = true,
                    IsEnabled = true,
                    AcValue = 0, DcValue = 600,
                },
            });

        // Re-applying the same display values: AC=0, DC=10 (minutes) — no change.
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
        // changes to (60%, 80%). The fake def only routes the funnel wiring; its metadata is inert.
        var setting = new SettingDefinition
        {
            Id = PowerCfgPercentNumericId,
            Name = "Setting num-power-pct",
            Description = "desc",
            InputType = InputType.NumericRange,
        };
        _mockSettingsRegistry.Setup(r => r.GetById(PowerCfgPercentNumericId)).Returns(setting);
        _mockSettingsRegistry.Setup(r => r.GetFeatureIdForSetting(PowerCfgPercentNumericId)).Returns("TestDomain");
        _mockSettingsRegistry.Setup(r => r.GetFilteredSettings("TestDomain")).Returns(new[] { setting });

        // "%" has no Common_Unit_* key — ResolveLocalized returns null for a miss-marker; leave
        // mock at default (key echo). The switch default returns the raw "%" directly.
        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
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
        RegisterSelectionSetting(PowerCfgSelectionId, System.Array.Empty<ComboBoxOption>());

        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_0")).Returns("Never");

        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
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
        RegisterSelectionSetting(PowerCfgSelectionId, System.Array.Empty<ComboBoxOption>());

        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_0")).Returns("Never");
        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_1")).Returns("4 minutes");

        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
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

    // ---------------------------------------------------------------
    // #367: battery-less machines render AC-only (no phantom DC) +
    //       a no-match raw PowerCfg value renders Custom, never index-by-raw
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingAsync_NoBatterySelection_RendersAcOnly()
    {
        // Battery-less desktop: only the AC dropdown exists and PowerCfgApplier skips all DC writes.
        // The receipt must show "AC: <label>" only -- no ", DC: ..." phantom. Rendering reads the REAL catalog
        // (power-display-timeout: Set["Power"] raw 0 -> option 0; apply index 2 -> option 2).
        _mockHardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(false);

        RegisterSelectionSetting(PowerCfgSelectionId, System.Array.Empty<ComboBoxOption>());

        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_0")).Returns("Never");
        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_2")).Returns("9 minutes");

        // Before-state raw AC = 0 -> option 0 "Never". DC raw is garbage on a battery-less machine.
        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [PowerCfgSelectionId] = new SettingStateResult
                {
                    Success = true,
                    IsEnabled = true,
                    AcValue = 0, DcValue = 999999,
                },
            });

        // After: apply tuple (2, 0). AC changes Never → 9 minutes; entry renders AC-only on both sides.
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
        _mockHardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(false);

        RegisterSelectionSetting(PowerCfgSelectionId, System.Array.Empty<ComboBoxOption>());

        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_0")).Returns("Never");

        // AC raw 0 -> option 0 "Never". DC raw is garbage and DIFFERENT from the applied DC index.
        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                [PowerCfgSelectionId] = new SettingStateResult
                {
                    Success = true,
                    IsEnabled = true,
                    AcValue = 0, DcValue = 12345,
                },
            });

        // Apply (0, 1): AC stays option 0 (Never); DC index differs but is suppressed on no-battery.
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
        // Regression for the fallback-as-index bug (commit f9528147): a raw PowerCfg DC value that matches NO
        // option must render "Custom" -- NOT States[rawValue]. Rendering reads the REAL catalog
        // (power-display-timeout: Set["Power"] values 0,60,120,180,300,...; raw 1 matches none). Battery present.
        RegisterSelectionSetting(PowerCfgSelectionId, System.Array.Empty<ComboBoxOption>());

        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_0")).Returns("Never");
        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_2")).Returns("5 minutes");
        _mockLocalization.Setup(l => l.GetString("Common_CustomState")).Returns("Custom");

        // AC raw 0 -> option 0 "Never". DC raw 1 matches NO option's Set["Power"] value.
        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
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
    public async Task ApplySettingAsync_BatteryDetectionThrows_AppliesAndRendersBothComponents()
    {
        // Fail-open: a hardware detection failure defaults to battery=true, so the apply still succeeds and the
        // entry renders BOTH AC and DC (more information, never a phantom suppression). Rendering reads the REAL
        // catalog (power-display-timeout: raw 0 -> option 0).
        _mockHardware.Setup(h => h.HasBatteryAsync()).ThrowsAsync(new InvalidOperationException("WMI exploded"));

        RegisterSelectionSetting(PowerCfgSelectionId, System.Array.Empty<ComboBoxOption>());

        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_0")).Returns("Never");
        _mockLocalization.Setup(l => l.GetString("Template_TimeIntervals_Option_1")).Returns("4 minutes");

        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
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

    // --- Phase 6.7 Slice 8b-2b (D1): the funnel re-applies Winhance-recommended power settings after a successful
    //     switch TO the Winhance power plan (re-homed from the retired PowerService special-handler tail). The mocked
    //     handler registry returns null, so the funnel always exercises the NEW engine path here, as it will in
    //     production once the special-handler registration is removed. ---

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
            Value = PowerPlanDefinitions.WinhancePowerPlanGuid,
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
            Value = PowerPlanDefinitions.WinhancePowerPlanGuid,
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
}

using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class BuilderModeEntryTests
{
    private readonly Mock<IApplicationModeService> _mode = new();
    private readonly Mock<IConfigurationService> _configuration = new();
    private readonly Mock<IDialogService> _dialogs = new();
    private readonly Mock<ILocalizationService> _loc = new();
    private readonly Mock<IUserPreferencesService> _preferences = new();
    private readonly Mock<IBuilderSeedSource> _seeds = new();
    private readonly Mock<ISelectionSetBuilder> _selections = new();
    private readonly Mock<ICatalogSettingsRegistry> _registry = new();
    private readonly Mock<IConfigAppSelectionService> _appSelection = new();
    private readonly Mock<IEventBus> _eventBus = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IFileStore> _fileStore = new();
    private readonly Mock<IConfigLoadService> _configLoad = new();
    private readonly Mock<ITargetWindowsService> _targetWindows = new();
    private readonly Mock<IFilePickerService> _picker = new();
    private readonly Mock<IAutounattendReader> _reader = new();
    private readonly Mock<IInstallOptionsService> _installOptions = new();
    private readonly Mock<IProfileService> _profiles = new();

    public BuilderModeEntryTests()
    {
        _loc.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
        ArrangeSeed(BuilderSeed.CurrentMachine);
        _fileStore.Setup(f => f.MaterializeAsync(It.IsAny<SelectionSet>())).ReturnsAsync((SelectionSet set) => set);
    }

    private BuilderModeEntry Sut() => new(
        _mode.Object,
        _configuration.Object,
        _dialogs.Object,
        _loc.Object,
        _preferences.Object,
        _seeds.Object,
        _selections.Object,
        _registry.Object,
        _appSelection.Object,
        _eventBus.Object,
        _log.Object,
        _fileStore.Object,
        _configLoad.Object,
        _targetWindows.Object,
        _picker.Object,
        _reader.Object,
        _installOptions.Object,
        _profiles.Object);

    private void ArrangePicked(string? path)
    {
        ArrangeIntroAlreadyDismissed();
        ArrangeSeed(BuilderSeed.ConfigFile);
        _picker.Setup(p => p.PickFile(It.IsAny<string[]>(), null)).Returns(path);
    }

    private void ArrangeIntroAlreadyDismissed() =>
        _preferences.Setup(p => p.GetPreference(It.IsAny<string>(), false)).Returns(true);

    private ModeEntryNotes? _shownNotes;

    private void ArrangeSeed(BuilderSeed? seed, bool hideExplanation = false) =>
        _dialogs.Setup(d => d.ShowBuilderSeedDialogAsync(It.IsAny<ModeEntryNotes>()))
            .Callback<ModeEntryNotes>(notes => _shownNotes = notes)
            .ReturnsAsync((seed, hideExplanation));

    [Fact]
    public async Task EnterFromProfile_UnreadableFile_SaysSo_AndDoesNotEnter()
    {
        var profile = new SettingsProfile("Gaming", "Gaming", ProfileKind.User, @"C:\p\Gaming.winhance");
        _profiles.Setup(p => p.LoadAsync(profile)).ReturnsAsync((WinhanceConfigFile?)null);
        var entry = Sut();

        (await entry.EnterFromProfileAsync(profile)).Should().BeFalse();

        _dialogs.Verify(d => d.ShowErrorAsync("Profiles_Error_Load", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mode.Verify(m => m.EnterBuilderMode(It.IsAny<BuilderTarget>()), Times.Never);
        entry.EditingProfile.Should().BeNull();
    }

    [Fact]
    public async Task EnteringIsOneDialog_ThatExplainsBuilder_WithNoReasonWhenChosenFromTheBar()
    {
        (await Sut().EnterAsync(BuilderTarget.Config, null)).Should().BeTrue();

        _dialogs.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
        _dialogs.Verify(d => d.ShowBuilderSeedDialogAsync(It.IsAny<ModeEntryNotes>()), Times.Once);
        _shownNotes.Should().Be(new ModeEntryNotes(null, "Dialog_BuilderIntro_Message", null));
    }

    [Fact]
    public async Task AnotherPagesReason_IsShownAtTheTop()
    {
        await Sut().EnterAsync(BuilderTarget.Autounattend, "Unattend needs Builder mode.");

        _shownNotes!.Reason.Should().Be("Unattend needs Builder mode.");
    }

    [Fact]
    public async Task ExplanationAlreadyDismissed_IsLeftOut_ButTheSeedIsStillAsked()
    {
        ArrangeIntroAlreadyDismissed();

        await Sut().EnterAsync(BuilderTarget.Config, null);

        _shownNotes!.Explanation.Should().BeNull();
        _mode.Verify(m => m.EnterBuilderMode(BuilderTarget.Config), Times.Once);
    }

    [Fact]
    public async Task TickedDontShowAgain_IsWrittenToThePreferences()
    {
        ArrangeSeed(BuilderSeed.CurrentMachine, hideExplanation: true);

        await Sut().EnterAsync(BuilderTarget.Config, null);

        _preferences.Verify(p => p.SetPreferenceAsync("BuilderModeIntroDontShow", true), Times.Once);
    }

    [Theory]
    [InlineData(WinhanceMode.Normal, false, null)]
    [InlineData(WinhanceMode.ConfigReview, false, "Dialog_ModeEntry_Discards")]
    public async Task LeavingAReview_WarnsInsideTheSameDialog(WinhanceMode current, bool builderChanges, string? warning)
    {
        _mode.Setup(m => m.CurrentMode).Returns(current);
        _mode.Setup(m => m.HasBuilderChanges).Returns(builderChanges);

        await Sut().EnterAsync(BuilderTarget.Config, null);

        _shownNotes!.Warning.Should().Be(warning);
    }

    [Fact]
    public async Task SeedDialogCancelled_DoesNotEnterBuilder()
    {
        ArrangeIntroAlreadyDismissed();
        ArrangeSeed(null, hideExplanation: true);

        (await Sut().EnterAsync(BuilderTarget.Config, null)).Should().BeFalse();

        _mode.Verify(m => m.EnterBuilderMode(It.IsAny<BuilderTarget>()), Times.Never);
        _preferences.Verify(p => p.SetPreferenceAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task SeedCurrentMachine_EntersBuilder_RecordsNothing()
    {
        ArrangeIntroAlreadyDismissed();

        (await Sut().EnterAsync(BuilderTarget.Config, null)).Should().BeTrue();

        _mode.Verify(m => m.EnterBuilderMode(BuilderTarget.Config), Times.Once);
        _mode.Verify(m => m.RecordBuilderEdit(It.IsAny<SettingChoice>()), Times.Never);
        _eventBus.Verify(b => b.Publish(It.IsAny<BuilderSeededEvent>()), Times.Never);
    }

    [Fact]
    public async Task SeedRecommended_RecordsEveryChoice_AndPublishes()
    {
        ArrangeIntroAlreadyDismissed();
        ArrangeSeed(BuilderSeed.Recommended);

        var scope = new CatalogScope(IncludeOtherOsVersions: true, IncludeOtherHardware: false);
        _selections.Setup(b => b.CurrentScope).Returns(scope);
        _seeds
            .Setup(s => s.ChoicesForAsync(BuilderSeed.Recommended, scope))
            .ReturnsAsync(new List<SettingChoice>
            {
                new("setting-a", new ChoiceValue.Toggle(true)),
                new("setting-b", new ChoiceValue.Toggle(false)),
            });

        // The edits are recorded into a Builder session, so entering the mode has to come first.
        var calls = new List<string>();
        _mode.Setup(m => m.EnterBuilderMode(BuilderTarget.Config)).Callback(() => calls.Add("enter"));
        _mode.Setup(m => m.RecordBuilderEdit(It.IsAny<SettingChoice>()))
            .Callback<SettingChoice>(choice => calls.Add($"record:{choice.SettingId}"));
        _eventBus.Setup(b => b.Publish(It.IsAny<BuilderSeededEvent>())).Callback(() => calls.Add("publish"));

        (await Sut().EnterAsync(BuilderTarget.Config, null)).Should().BeTrue();

        calls.Should().Equal("enter", "record:setting-a", "record:setting-b", "publish");
    }

    [Fact]
    public async Task AThrowWhileEntering_IsLoggedAndReportsFalse()
    {
        // Both callers run this outside a try: the top bar fires and forgets, WIMUtil's button is a command.
        ArrangeIntroAlreadyDismissed();
        _mode.Setup(m => m.EnterBuilderMode(It.IsAny<BuilderTarget>())).Throws(new InvalidOperationException("boom"));

        (await Sut().EnterAsync(BuilderTarget.Config, null)).Should().BeFalse();

        _log.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("boom")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task LeavingConfigReview_CancelsItBeforeEnteringBuilder()
    {
        ArrangeIntroAlreadyDismissed();
        _mode.Setup(m => m.CurrentMode).Returns(WinhanceMode.ConfigReview);

        var calls = new List<string>();
        _configuration.Setup(c => c.CancelReviewModeAsync())
            .Callback(() => calls.Add("cancel-review"))
            .Returns(Task.CompletedTask);
        _mode.Setup(m => m.EnterBuilderMode(It.IsAny<BuilderTarget>())).Callback(() => calls.Add("enter"));

        await Sut().EnterAsync(BuilderTarget.Config, null);

        calls.Should().Equal("cancel-review", "enter");
    }

    // Cancelling a review keeps its app ticks for Normal mode (issue 656), so Builder has to drop them itself.
    [Fact]
    public async Task SeedCurrentMachine_TicksWhatThisPcMissesAndHas_AfterTheReviewEnds_AndBeforeEntering()
    {
        ArrangeIntroAlreadyDismissed();
        _mode.Setup(m => m.CurrentMode).Returns(WinhanceMode.ConfigReview);
        var calls = new List<string>();
        _configuration.Setup(c => c.CancelReviewModeAsync()).Callback(() => calls.Add("cancel-review")).Returns(Task.CompletedTask);
        _appSelection.Setup(a => a.SelectInstalledStateAsync()).Callback(() => calls.Add("installed-state")).Returns(Task.CompletedTask);
        _mode.Setup(m => m.EnterBuilderMode(It.IsAny<BuilderTarget>())).Callback(() => calls.Add("enter"));

        (await Sut().EnterAsync(BuilderTarget.Config, null)).Should().BeTrue();

        calls.Should().Equal("cancel-review", "installed-state", "enter");
        _appSelection.Verify(a => a.ClearWindowsAppsSelectionAsync(), Times.Never);
        _appSelection.Verify(a => a.ClearExternalAppsSelectionAsync(), Times.Never);
    }

    [Theory]
    [InlineData(BuilderSeed.WindowsDefaults)]
    public async Task AFreshSeed_ClearsLeftoverAppTicks_AfterTheReviewEnds_AndBeforeEntering(BuilderSeed seed)
    {
        ArrangeIntroAlreadyDismissed();
        ArrangeSeed(seed);
        _seeds.Setup(s => s.ChoicesForAsync(It.IsAny<BuilderSeed>(), It.IsAny<CatalogScope>())).ReturnsAsync([]);
        _mode.Setup(m => m.CurrentMode).Returns(WinhanceMode.ConfigReview);
        var calls = new List<string>();
        _configuration.Setup(c => c.CancelReviewModeAsync()).Callback(() => calls.Add("cancel-review")).Returns(Task.CompletedTask);
        _appSelection.Setup(a => a.ClearWindowsAppsSelectionAsync()).Callback(() => calls.Add("clear-windows")).Returns(Task.CompletedTask);
        _appSelection.Setup(a => a.ClearExternalAppsSelectionAsync()).Callback(() => calls.Add("clear-external")).Returns(Task.CompletedTask);
        _mode.Setup(m => m.EnterBuilderMode(It.IsAny<BuilderTarget>())).Callback(() => calls.Add("enter"));

        (await Sut().EnterAsync(BuilderTarget.Config, null)).Should().BeTrue();

        calls.Should().Equal("cancel-review", "clear-windows", "clear-external", "enter");
        _appSelection.Verify(a => a.SelectWindowsAppsFromConfigAsync(It.IsAny<ConfigSection>()), Times.Never);
        _appSelection.Verify(a => a.SelectExternalAppsFromConfigAsync(It.IsAny<ConfigSection>()), Times.Never);
    }

    [Theory]
    [InlineData(BuilderSeed.CurrentMachine)]
    [InlineData(BuilderSeed.Recommended)]
    [InlineData(BuilderSeed.WindowsDefaults)]
    public async Task TheChosenSeed_IsStored_AndForgottenWhenBuilderIsLeft(BuilderSeed seed)
    {
        ArrangeIntroAlreadyDismissed();
        ArrangeSeed(seed);
        _seeds.Setup(s => s.ChoicesForAsync(It.IsAny<BuilderSeed>(), It.IsAny<CatalogScope>())).ReturnsAsync([]);
        var entry = Sut();

        (await entry.EnterAsync(BuilderTarget.Config, null)).Should().BeTrue();
        entry.StartedFrom.Should().Be(seed);

        _mode.Setup(m => m.CurrentMode).Returns(WinhanceMode.Normal);
        _mode.Raise(m => m.ModeChanged += null, EventArgs.Empty);
        entry.StartedFrom.Should().BeNull();
    }

    [Fact]
    public async Task Reseed_AsksOnce_ThenStartsTheOpenSessionOverFromTheNewSeed()
    {
        ArrangeIntroAlreadyDismissed();
        var entry = Sut();
        await entry.EnterAsync(BuilderTarget.Autounattend, null);
        _mode.Setup(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        _mode.Setup(m => m.CurrentBuilderTarget).Returns(BuilderTarget.Autounattend);
        _dialogs.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>())).ReturnsAsync(new ConfirmationResponse { Confirmed = true });
        _seeds.Setup(s => s.ChoicesForAsync(BuilderSeed.WindowsDefaults, It.IsAny<CatalogScope>())).ReturnsAsync([]);
        _mode.Invocations.Clear();

        (await entry.ReseedAsync(BuilderSeed.WindowsDefaults)).Should().BeTrue();

        _dialogs.Verify(d => d.ShowConfirmationAsync(It.Is<ConfirmationRequest>(r => r.Message == "Builder_Reseed_Message")), Times.Once);
        _dialogs.Verify(d => d.ShowBuilderSeedDialogAsync(It.IsAny<ModeEntryNotes>()), Times.Once);
        _mode.Verify(m => m.EnterBuilderMode(BuilderTarget.Autounattend), Times.Once);
        _appSelection.Verify(a => a.ClearWindowsAppsSelectionAsync(), Times.Once);
        entry.StartedFrom.Should().Be(BuilderSeed.WindowsDefaults);
    }

    [Fact]
    public async Task Reseed_Declined_ChangesNothing()
    {
        ArrangeIntroAlreadyDismissed();
        var entry = Sut();
        await entry.EnterAsync(BuilderTarget.Config, null);
        _mode.Setup(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        _dialogs.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>())).ReturnsAsync(new ConfirmationResponse { Confirmed = false });
        _mode.Invocations.Clear();
        _appSelection.Invocations.Clear();

        (await entry.ReseedAsync(BuilderSeed.Recommended)).Should().BeFalse();

        entry.StartedFrom.Should().Be(BuilderSeed.CurrentMachine);
        _mode.Verify(m => m.EnterBuilderMode(It.IsAny<BuilderTarget>()), Times.Never);
        _mode.Verify(m => m.RecordBuilderEdit(It.IsAny<SettingChoice>()), Times.Never);
        _appSelection.Invocations.Should().BeEmpty();
        _configLoad.Verify(c => c.LoadRecommendedConfigurationAsync(), Times.Never);
    }

    [Fact]
    public async Task Reseed_ToTheSeedItStartedFrom_OrOutsideBuilder_AsksNothing()
    {
        ArrangeIntroAlreadyDismissed();
        var entry = Sut();
        await entry.EnterAsync(BuilderTarget.Config, null);

        (await entry.ReseedAsync(BuilderSeed.CurrentMachine)).Should().BeFalse();
        _mode.Setup(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        (await entry.ReseedAsync(BuilderSeed.CurrentMachine)).Should().BeFalse();

        _dialogs.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
    }

    // The Recommended seed ticks the apps of the very file the import dialog loads.
    [Fact]
    public async Task TheRecommendedSeed_TicksTheAppsOfItsImportFile_BeforeEntering()
    {
        ArrangeIntroAlreadyDismissed();
        ArrangeSeed(BuilderSeed.Recommended);
        _seeds.Setup(s => s.ChoicesForAsync(It.IsAny<BuilderSeed>(), It.IsAny<CatalogScope>())).ReturnsAsync([]);
        var file = new WinhanceConfigFile();
        _configLoad.Setup(c => c.LoadRecommendedConfigurationAsync()).ReturnsAsync(file);
        var calls = new List<string>();
        _appSelection.Setup(a => a.SelectWindowsAppsFromConfigAsync(file.WindowsApps)).Callback(() => calls.Add("windows")).Returns(Task.CompletedTask);
        _appSelection.Setup(a => a.SelectExternalAppsFromConfigAsync(file.ExternalApps)).Callback(() => calls.Add("external")).Returns(Task.CompletedTask);
        _mode.Setup(m => m.EnterBuilderMode(It.IsAny<BuilderTarget>())).Callback(() => calls.Add("enter"));

        (await Sut().EnterAsync(BuilderTarget.Config, null)).Should().BeTrue();

        calls.Should().Equal("windows", "external", "enter");
        _appSelection.Verify(a => a.ClearWindowsAppsSelectionAsync(), Times.Never);
    }

    [Fact]
    public async Task TheTargetReachesEnterBuilderMode()
    {
        ArrangeIntroAlreadyDismissed();

        await Sut().EnterAsync(BuilderTarget.Autounattend, null);

        _mode.Verify(m => m.EnterBuilderMode(BuilderTarget.Autounattend), Times.Once);
    }

    private static readonly IReadOnlyList<Setting> PrivacyToggles =
        SettingCatalog.ByFeature[FeatureIds.Privacy].Where(s => s.Control == ControlKind.Toggle).Take(2).ToList();

    // Built by the real writer, so the reader under test cannot drift from what a saved file holds.
    private WinhanceConfigFile ArrangeFileHoldingTheFirstToggle()
    {
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [FeatureIds.Privacy] = PrivacyToggles };
        _registry.Setup(r => r.GetAll(It.IsAny<CatalogScope>())).Returns(byFeature);
        var set = new SelectionSet(
            [new SettingChoice(PrivacyToggles[0].Id, new ChoiceValue.Toggle(true))],
            [new AppChoice("app", "App", null, null, null, null)],
            []);
        return ConfigFileMapper.ToFile(set, byFeature);
    }

    [Fact]
    public async Task EnterFrom_RecordsTheFilesChoices_AndLeavesOutWhatItLeftOut()
    {
        var config = ArrangeFileHoldingTheFirstToggle();

        (await Sut().EnterFromAsync(BuilderTarget.Autounattend, config)).Should().BeTrue();

        _mode.Verify(m => m.EnterBuilderMode(BuilderTarget.Autounattend), Times.Once);
        _mode.Verify(m => m.RecordBuilderEdit(new SettingChoice(PrivacyToggles[0].Id, new ChoiceValue.Toggle(true))), Times.Once);
        _mode.Verify(m => m.SetIncluded(PrivacyToggles[1].Id, false), Times.Once);
        _mode.Verify(m => m.SetIncluded(PrivacyToggles[0].Id, It.IsAny<bool>()), Times.Never);
        _eventBus.Verify(b => b.Publish(It.IsAny<BuilderSeededEvent>()), Times.Once);
    }

    [Fact]
    public async Task EnterFrom_EditsTheFilesInstallOptions_AndLeavingBuilderDropsThem()
    {
        var config = ArrangeFileHoldingTheFirstToggle();
        var carried = new InstallOptionsStore { Defaults = InstallOptions.Default with { Interactive = true } };
        config.AppInstallOptions = carried;
        var sut = Sut();
        _mode.Setup(m => m.CurrentMode).Returns(WinhanceMode.Builder);

        await sut.EnterFromAsync(BuilderTarget.Config, config);

        _installOptions.Verify(o => o.BeginSession(carried), Times.Once);
        _installOptions.Invocations.Clear();
        _mode.Setup(m => m.CurrentMode).Returns(WinhanceMode.Normal);
        _mode.Raise(m => m.ModeChanged += null, EventArgs.Empty);
        _installOptions.Verify(o => o.EndSession(), Times.Once);
    }

    [Fact]
    public async Task FreshBuilder_StartsFromThisPcsSavedInstallOptions()
    {
        ArrangeIntroAlreadyDismissed();
        var saved = new InstallOptionsStore { Defaults = InstallOptions.Default with { Scope = InstallScope.User } };
        _installOptions.Setup(o => o.Current).Returns(saved);

        await Sut().EnterAsync(BuilderTarget.Config, null);

        _installOptions.Verify(o => o.BeginSession(saved), Times.Once);
    }

    [Fact]
    public async Task EnterFrom_SetsTheFilesTargetBeforeReadingItsChoices()
    {
        var config = ArrangeFileHoldingTheFirstToggle();
        config.TargetBuild = 26300;
        var calls = new List<string>();
        _targetWindows.Setup(t => t.Set(26300)).Callback(() => calls.Add("target"));
        _registry.Setup(r => r.GetAll(It.IsAny<CatalogScope>())).Callback(() => calls.Add("read"))
            .Returns(new Dictionary<string, IReadOnlyList<Setting>> { [FeatureIds.Privacy] = PrivacyToggles });

        await Sut().EnterFromAsync(BuilderTarget.Autounattend, config);

        calls.Should().Equal("target", "read");
    }

    [Fact]
    public async Task EnterFrom_TicksTheFilesAppsBeforeEntering_WithNoIntroOrSeed()
    {
        var config = ArrangeFileHoldingTheFirstToggle();
        var calls = new List<string>();
        _appSelection.Setup(a => a.SelectWindowsAppsFromConfigAsync(config.WindowsApps))
            .Callback(() => calls.Add("apps")).Returns(Task.CompletedTask);
        _mode.Setup(m => m.EnterBuilderMode(BuilderTarget.Autounattend)).Callback(() => calls.Add("enter"));

        await Sut().EnterFromAsync(BuilderTarget.Autounattend, config);

        calls.Should().Equal("apps", "enter");
        _appSelection.Verify(a => a.SelectExternalAppsFromConfigAsync(config.ExternalApps), Times.Once);
        _dialogs.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
        _dialogs.Verify(d => d.ShowBuilderSeedDialogAsync(It.IsAny<ModeEntryNotes>()), Times.Never);
    }

    [Fact]
    public async Task ConfigFileSeed_OpensThePickedFile()
    {
        ArrangePicked(@"C:\Configs\mine.winhance");
        _configLoad.Setup(c => c.LoadAndValidateConfigurationAsync(@"C:\Configs\mine.winhance")).ReturnsAsync(ArrangeFileHoldingTheFirstToggle());

        (await Sut().EnterAsync(BuilderTarget.Config, null)).Should().BeTrue();

        _mode.Verify(m => m.RecordBuilderEdit(new SettingChoice(PrivacyToggles[0].Id, new ChoiceValue.Toggle(true))), Times.Once);
        _seeds.Verify(s => s.ChoicesForAsync(It.IsAny<BuilderSeed>(), It.IsAny<CatalogScope>()), Times.Never);
        // The file's own apps replace the leftovers; the separate clears are for the seeds that carry no apps.
        _appSelection.Verify(a => a.SelectWindowsAppsFromConfigAsync(It.IsAny<ConfigSection>()), Times.Once);
        _appSelection.Verify(a => a.ClearExternalAppsSelectionAsync(), Times.Never);
    }

    [Fact]
    public async Task ConfigFileSeed_WithNoFilePicked_DoesNotEnter()
    {
        ArrangePicked(null);

        (await Sut().EnterAsync(BuilderTarget.Config, null)).Should().BeFalse();

        _mode.Verify(m => m.EnterBuilderMode(It.IsAny<BuilderTarget>()), Times.Never);
    }

    [Fact]
    public async Task ConfigFileSeed_AWinhanceAnswerFile_OpensItsChoicesForTheAnswerFile()
    {
        ArrangePicked(@"C:\Media\autounattend.xml");
        _reader.Setup(r => r.ReadAsync(@"C:\Media\autounattend.xml"))
            .ReturnsAsync(new WinhanceAnswerFile("26.10.01", ArrangeFileHoldingTheFirstToggle()));

        (await Sut().EnterAsync(BuilderTarget.Config, null)).Should().BeTrue();

        _mode.Verify(m => m.EnterBuilderMode(BuilderTarget.Autounattend), Times.Once);
        _mode.Verify(m => m.RecordBuilderEdit(new SettingChoice(PrivacyToggles[0].Id, new ChoiceValue.Toggle(true))), Times.Once);
    }

    [Theory]
    [InlineData(false, "Dialog_BuilderSeed_NotWinhanceFile")]
    [InlineData(true, "Dialog_BuilderSeed_NoChoices")]
    public async Task ConfigFileSeed_AnAnswerFileWithoutChoices_IsRefused(bool madeByWinhance, string messageKey)
    {
        ArrangePicked(@"C:\Media\autounattend.xml");
        _loc.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>())).Returns((string key, object[] _) => key);
        _reader.Setup(r => r.ReadAsync(It.IsAny<string>()))
            .ReturnsAsync(madeByWinhance ? new WinhanceAnswerFile("26.05.01", null) : null);

        (await Sut().EnterAsync(BuilderTarget.Config, null)).Should().BeFalse();

        _dialogs.Verify(d => d.ShowInformationAsync(messageKey, "Dialog_BuilderSeed_Title", It.IsAny<string>()), Times.Once);
        _mode.Verify(m => m.EnterBuilderMode(It.IsAny<BuilderTarget>()), Times.Never);
    }

    // The staging tick and the user's own installers are two settings of the file, opened like any other card.
    [Fact]
    public async Task EnterFrom_RecordsTheFilesStagingTickAndOwnInstallers()
    {
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            [FeatureIds.Autounattend] = [SettingCatalog.Find(OwnInstaller.StageAppsId)!, SettingCatalog.Find(OwnInstaller.SettingId)!],
        };
        _registry.Setup(r => r.GetAll(It.IsAny<CatalogScope>())).Returns(byFeature);
        var stage = new SettingChoice(OwnInstaller.StageAppsId, new ChoiceValue.CheckBox(false));
        var installers = new SettingChoice(OwnInstaller.SettingId, new ChoiceValue.List(
        [
            new ChoiceValue.ListRow(new Dictionary<string, string> { ["file"] = @"C:\Setup\Reader.msi", ["switches"] = "/qn" }),
        ]));
        var config = ConfigFileMapper.ToFile(new SelectionSet([stage, installers], [], []), byFeature);

        await Sut().EnterFromAsync(BuilderTarget.Autounattend, config);

        _mode.Verify(m => m.RecordBuilderEdit(stage), Times.Once);
        _mode.Verify(m => m.RecordBuilderEdit(installers), Times.Once);
    }

    [Fact]
    public async Task EnterFrom_LandsTheFilesCarriedPictures_BeforeRecordingTheChoices()
    {
        var config = ArrangeFileHoldingTheFirstToggle();
        var landed = new SettingChoice(PrivacyToggles[0].Id, new ChoiceValue.Toggle(false));
        _fileStore.Setup(f => f.MaterializeAsync(It.IsAny<SelectionSet>()))
            .ReturnsAsync((SelectionSet set) => set with { Settings = [landed] });

        await Sut().EnterFromAsync(BuilderTarget.Autounattend, config);

        _mode.Verify(m => m.RecordBuilderEdit(landed), Times.Once);
    }
}

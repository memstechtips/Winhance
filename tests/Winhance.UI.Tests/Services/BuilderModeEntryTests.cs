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

    public BuilderModeEntryTests()
    {
        _loc.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
        _dialogs.Setup(d => d.ShowBuilderSeedDialogAsync()).ReturnsAsync(BuilderSeed.CurrentMachine);
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
        _installOptions.Object);

    private void ArrangePicked(string? path)
    {
        ArrangeIntroAlreadyDismissed();
        _dialogs.Setup(d => d.ShowBuilderSeedDialogAsync()).ReturnsAsync(BuilderSeed.ConfigFile);
        _picker.Setup(p => p.PickFile(It.IsAny<string[]>(), null)).Returns(path);
    }

    private void ArrangeIntroAlreadyDismissed() =>
        _preferences.Setup(p => p.GetPreference(It.IsAny<string>(), false)).Returns(true);

    private void ArrangeIntro(bool confirmed, bool dontShowAgain) =>
        _dialogs.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = confirmed, CheckboxChecked = dontShowAgain });

    [Fact]
    public async Task IntroDeclined_DoesNotAskForASeed_AndDoesNotEnter()
    {
        ArrangeIntro(confirmed: false, dontShowAgain: false);

        (await Sut().EnterAsync(BuilderTarget.Config)).Should().BeFalse();

        _dialogs.Verify(d => d.ShowBuilderSeedDialogAsync(), Times.Never);
        _mode.Verify(m => m.EnterBuilderMode(It.IsAny<BuilderTarget>()), Times.Never);
    }

    [Fact]
    public async Task IntroTickedDontShowAgain_IsWrittenToThePreferences()
    {
        ArrangeIntro(confirmed: true, dontShowAgain: true);

        await Sut().EnterAsync(BuilderTarget.Config);

        _preferences.Verify(p => p.SetPreferenceAsync("BuilderModeIntroDontShow", true), Times.Once);
    }

    [Fact]
    public async Task IntroAlreadyDismissed_IsNotShownAgain()
    {
        ArrangeIntroAlreadyDismissed();

        await Sut().EnterAsync(BuilderTarget.Config);

        _dialogs.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
        _mode.Verify(m => m.EnterBuilderMode(BuilderTarget.Config), Times.Once);
    }

    [Fact]
    public async Task SeedDialogCancelled_DoesNotEnterBuilder()
    {
        ArrangeIntroAlreadyDismissed();
        _dialogs.Setup(d => d.ShowBuilderSeedDialogAsync()).ReturnsAsync((BuilderSeed?)null);

        (await Sut().EnterAsync(BuilderTarget.Config)).Should().BeFalse();

        _mode.Verify(m => m.EnterBuilderMode(It.IsAny<BuilderTarget>()), Times.Never);
    }

    [Fact]
    public async Task SeedCurrentMachine_EntersBuilder_RecordsNothing()
    {
        ArrangeIntroAlreadyDismissed();

        (await Sut().EnterAsync(BuilderTarget.Config)).Should().BeTrue();

        _mode.Verify(m => m.EnterBuilderMode(BuilderTarget.Config), Times.Once);
        _mode.Verify(m => m.RecordBuilderEdit(It.IsAny<SettingChoice>()), Times.Never);
        _eventBus.Verify(b => b.Publish(It.IsAny<BuilderSeededEvent>()), Times.Never);
    }

    [Fact]
    public async Task SeedRecommended_RecordsEveryChoice_AndPublishes()
    {
        ArrangeIntroAlreadyDismissed();
        _dialogs.Setup(d => d.ShowBuilderSeedDialogAsync()).ReturnsAsync(BuilderSeed.Recommended);

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

        (await Sut().EnterAsync(BuilderTarget.Config)).Should().BeTrue();

        calls.Should().Equal("enter", "record:setting-a", "record:setting-b", "publish");
    }

    [Fact]
    public async Task AThrowWhileEntering_IsLoggedAndReportsFalse()
    {
        // Both callers run this outside a try: the top bar fires and forgets, WIMUtil's button is a command.
        ArrangeIntroAlreadyDismissed();
        _mode.Setup(m => m.EnterBuilderMode(It.IsAny<BuilderTarget>())).Throws(new InvalidOperationException("boom"));

        (await Sut().EnterAsync(BuilderTarget.Config)).Should().BeFalse();

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

        await Sut().EnterAsync(BuilderTarget.Config);

        calls.Should().Equal("cancel-review", "enter");
    }

    [Fact]
    public async Task TheTargetReachesEnterBuilderMode()
    {
        ArrangeIntroAlreadyDismissed();

        await Sut().EnterAsync(BuilderTarget.Autounattend);

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

        await Sut().EnterAsync(BuilderTarget.Config);

        _installOptions.Verify(o => o.BeginSession(saved), Times.Once);
    }

    [Fact]
    public async Task EnterFrom_SetsTheFilesTargetBeforeReadingItsChoices()
    {
        var config = ArrangeFileHoldingTheFirstToggle();
        config.TargetBuild = 26300;
        var calls = new List<string>();
        _targetWindows.Setup(t => t.SetAsync(26300)).Callback(() => calls.Add("target")).Returns(Task.CompletedTask);
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
        _dialogs.Verify(d => d.ShowBuilderSeedDialogAsync(), Times.Never);
    }

    [Fact]
    public async Task ConfigFileSeed_OpensThePickedFile()
    {
        ArrangePicked(@"C:\Configs\mine.winhance");
        _configLoad.Setup(c => c.LoadAndValidateConfigurationAsync(@"C:\Configs\mine.winhance")).ReturnsAsync(ArrangeFileHoldingTheFirstToggle());

        (await Sut().EnterAsync(BuilderTarget.Config)).Should().BeTrue();

        _mode.Verify(m => m.RecordBuilderEdit(new SettingChoice(PrivacyToggles[0].Id, new ChoiceValue.Toggle(true))), Times.Once);
        _seeds.Verify(s => s.ChoicesForAsync(It.IsAny<BuilderSeed>(), It.IsAny<CatalogScope>()), Times.Never);
    }

    [Fact]
    public async Task ConfigFileSeed_WithNoFilePicked_DoesNotEnter()
    {
        ArrangePicked(null);

        (await Sut().EnterAsync(BuilderTarget.Config)).Should().BeFalse();

        _mode.Verify(m => m.EnterBuilderMode(It.IsAny<BuilderTarget>()), Times.Never);
    }

    [Fact]
    public async Task ConfigFileSeed_AWinhanceAnswerFile_OpensItsChoicesForTheAnswerFile()
    {
        ArrangePicked(@"C:\Media\autounattend.xml");
        _reader.Setup(r => r.ReadAsync(@"C:\Media\autounattend.xml"))
            .ReturnsAsync(new WinhanceAnswerFile("26.10.01", ArrangeFileHoldingTheFirstToggle()));

        (await Sut().EnterAsync(BuilderTarget.Config)).Should().BeTrue();

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

        (await Sut().EnterAsync(BuilderTarget.Config)).Should().BeFalse();

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

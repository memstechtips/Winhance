using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
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
    private readonly Mock<IEventBus> _eventBus = new();
    private readonly Mock<ILogService> _log = new();

    public BuilderModeEntryTests()
    {
        _loc.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
        _dialogs.Setup(d => d.ShowBuilderSeedDialogAsync()).ReturnsAsync(BuilderSeed.CurrentMachine);
    }

    private BuilderModeEntry Sut() => new(
        _mode.Object,
        _configuration.Object,
        _dialogs.Object,
        _loc.Object,
        _preferences.Object,
        _seeds.Object,
        _selections.Object,
        _eventBus.Object,
        _log.Object);

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
}

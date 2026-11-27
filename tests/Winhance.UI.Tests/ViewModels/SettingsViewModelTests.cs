using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Settings.ViewModels;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IThemeService> _mockThemeService = new();
    private readonly Mock<IUserPreferencesService> _mockPreferencesService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<IConfigurationService> _mockConfigurationService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<ISystemBackupService> _mockBackupService = new();
    private readonly Mock<ITaskProgressService> _mockTaskProgressService = new();
    private readonly Mock<IScheduledTaskService> _mockScheduledTaskService = new();
    private readonly Mock<IInteractiveUserService> _mockInteractiveUserService = new();
    private readonly Mock<IProfileService> _mockProfiles = new();
    private readonly Mock<IBackgroundTaskService> _mockBackgroundTasks = new();
    private readonly Mock<IExplorerWindowManager> _mockExplorer = new();
    private readonly Mock<IDispatcherService> _mockDispatcher = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly Mock<IAppIconResolver> _mockAppIcons = new();

    // Stands in for the UI thread: the view model's awaits resume on the scheduler that started them, so a poll run
    // here never reads a list while a refresh is rebuilding it on a pool thread.
    private readonly TaskScheduler _ui = new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler;

    public SettingsViewModelTests()
    {
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _mockLocalization.MirrorTryGetString();
        _mockLocalization
            .Setup(l => l.CurrentLanguage)
            .Returns("en");
        _mockLocalization
            .Setup(l => l.GetAvailableLanguages())
            .Returns(new List<LanguageOption>
            {
                new("en", "English"),
                new("af", "Afrikaans"),
                new("fr", "Français (French)"),
            });
        _mockThemeService
            .Setup(t => t.CurrentTheme)
            .Returns(WinhanceTheme.System);
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] _) => key);
        _mockProfiles.Setup(p => p.GetProfiles()).Returns([]);
        _mockProfiles.Setup(p => p.PendingReboot).Returns([]);
        _mockProfiles.Setup(p => p.DriftAsync()).ReturnsAsync([]);
        _mockDispatcher.Setup(d => d.RunOnUIThread(It.IsAny<Action>())).Callback((Action a) => a());
        _mockBackgroundTasks.Setup(b => b.GetMode()).Returns(KeepRemovedMode.Winhance);
        _mockBackgroundTasks.Setup(b => b.IsKeepRemovedOnAsync()).ReturnsAsync(false);
        _mockBackgroundTasks.Setup(b => b.IsTaskSafetyOkAsync()).ReturnsAsync(true);
        _mockBackgroundTasks.Setup(b => b.GetRemovalTasksAsync()).ReturnsAsync([]);
        _mockBackgroundTasks.Setup(b => b.GetSignInOptionsAsync()).ReturnsAsync(SignInOptions.None);
        _mockBackgroundTasks.Setup(b => b.SetSignInOptionAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(OperationResult.Succeeded());
        _mockBackgroundTasks.Setup(b => b.SetKeepRemovedOnAsync(It.IsAny<bool>())).ReturnsAsync(OperationResult.Succeeded());
        _mockBackgroundTasks.Setup(b => b.SetModeAsync(It.IsAny<KeepRemovedMode>())).ReturnsAsync(OperationResult.Succeeded());
        _mockBackgroundTasks.Setup(b => b.GetKeptApps()).Returns([]);
        _mockBackgroundTasks.Setup(b => b.GetStatus()).Returns(new KeepRemovedStatus(null, "", [], null));
        _mockBackgroundTasks.Setup(b => b.GetOtherTasksAsync()).ReturnsAsync([]);
        _mockBackgroundTasks.Setup(b => b.GetEdgeRedirectAsync()).ReturnsAsync((RemovalTaskState?)null);
    }

    private void GivenConfirmation(bool confirmed) =>
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = confirmed });

    private void GivenSignInOptions(bool startInTray = false, bool reapply = false, bool maintain = false) =>
        _mockBackgroundTasks.Setup(b => b.GetSignInOptionsAsync()).ReturnsAsync(new SignInOptions(startInTray, reapply, maintain));

    private SettingsViewModel CreateViewModel()
    {
        return new SettingsViewModel(
            _mockLocalization.Object,
            _mockThemeService.Object,
            _mockPreferencesService.Object,
            _mockDialogService.Object,
            _mockConfigurationService.Object,
            _mockLogService.Object,
            _mockBackupService.Object,
            _mockTaskProgressService.Object,
            _mockScheduledTaskService.Object,
            _mockInteractiveUserService.Object,
            _mockProfiles.Object,
            Mock.Of<IProcessExecutor>(),
            Mock.Of<IFilePickerService>(),
            Mock.Of<IBuilderModeEntry>(),
            Mock.Of<ICoverageSummaryService>(),
            _mockBackgroundTasks.Object,
            _mockExplorer.Object,
            _mockDispatcher.Object,
            _mockEventBus.Object,
            _mockAppIcons.Object);
    }

    private async Task<SettingsViewModel> CreateReadyViewModelAsync()
    {
        var vm = CreateViewModel();
        await vm.BackgroundTasksReady;
        return vm;
    }

    [Fact]
    public async Task OpenLogCommand_WhenExplorerThrows_ShowsTheError()
    {
        var vm = await CreateReadyViewModelAsync();
        _mockExplorer.Setup(e => e.OpenFolderAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("boom"));

        await vm.OpenLogCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowErrorAsync("Common_ActionFailed", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task StartInTrayAtSignIn_WhenTurnedOn_SetsTheTrayFlagOnTheSignInTask()
    {
        var vm = await CreateReadyViewModelAsync();

        vm.StartInTrayAtSignIn = true;

        _mockBackgroundTasks.Verify(b => b.SetSignInOptionAsync(SignInTask.TrayFlag, true), Times.Once);
        vm.StartInTrayAtSignIn.Should().BeTrue();
    }

    [Fact]
    public async Task ReapplyAtSignIn_WhenTurnedOn_SetsTheReapplyFlagOnTheSignInTask()
    {
        var vm = await CreateReadyViewModelAsync();

        vm.ReapplyAtSignIn = true;

        _mockBackgroundTasks.Verify(b => b.SetSignInOptionAsync(SignInTask.ReapplyFlag, true), Times.Once);
        _mockBackgroundTasks.Verify(b => b.SetSignInOptionAsync(SignInTask.TrayFlag, It.IsAny<bool>()), Times.Never);
        vm.ReapplyAtSignIn.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, false, "Profiles_Status_Active", false, "")]
    [InlineData(1, false, "Profiles_Status_ChangedOne", true, "")]
    [InlineData(3, false, "Profiles_Status_Changed", true, "")]
    [InlineData(3, true, "", false, "Profiles_Status_Failed")]
    public void Profiles_Always_KeepEveryDescriptionAndBadgeOnlyTheActiveProfile(int drifted, bool failed, string badge, bool attention, string failedText)
    {
        _mockProfiles.Setup(p => p.GetProfiles()).Returns(
        [
            new SettingsProfile("builtin:focus", "Winhance Focus", ProfileKind.Focus, null, "Quiet while you play"),
            new SettingsProfile("work", "Work", ProfileKind.User, @"C:\p\work.winhance"),
            new SettingsProfile("gaming", "Gaming", ProfileKind.User, @"C:\p\gaming.winhance", "Max FPS"),
        ]);
        _mockProfiles.Setup(p => p.ActiveProfileId).Returns("gaming");
        _mockProfiles.Setup(p => p.LastSwitchFailed).Returns(failed);
        _mockProfiles.Setup(p => p.DriftAsync()).ReturnsAsync([.. Enumerable.Repeat("Game Mode", drifted)]);

        var vm = CreateViewModel();

        vm.Profiles.Select(r => (r.Name, r.Description, r.Badge, r.FailedText, r.CanDelete)).Should().Equal(
            ("Winhance Focus", "Quiet while you play", "", "", false),
            ("Work", "", "", "", true),
            ("Gaming", "Max FPS", badge, failedText, true));
        vm.Profiles[2].ShowAttentionBadge.Should().Be(attention);
        vm.Profiles[2].ShowCleanBadge.Should().Be(badge.Length > 0 && !attention);
    }

    [Fact]
    public async Task SelectedWinhanceUpdateActionOption_SavesItsOwnPreferenceAndLeavesTheSoftwareOneAlone()
    {
        var vm = await CreateReadyViewModelAsync();

        vm.SelectedWinhanceUpdateActionOption.Should().BeSameAs(vm.WinhanceUpdateActionOptions.Single(o => Equals(o.Value, AppUpdateAction.Notify)));
        vm.SelectedWinhanceUpdateActionOption = vm.WinhanceUpdateActionOptions.Single(o => Equals(o.Value, AppUpdateAction.Install));

        _mockPreferencesService.Verify(p => p.SetPreferenceAsync(UserPreferenceKeys.WinhanceUpdateAction, AppUpdateAction.Install), Times.Once);
        _mockPreferencesService.Verify(p => p.SetPreferenceAsync(UserPreferenceKeys.AppUpdateAction, It.IsAny<AppUpdateAction>()), Times.Never);
    }

    [Fact]
    public void Profiles_WhenARestartIsPending_BadgeTheActiveProfileForAttention()
    {
        _mockProfiles.Setup(p => p.GetProfiles()).Returns([new SettingsProfile("work", "Work", ProfileKind.User, @"C:\p\work.winhance")]);
        _mockProfiles.Setup(p => p.ActiveProfileId).Returns("work");
        _mockProfiles.Setup(p => p.PendingReboot).Returns(["Core isolation"]);

        var vm = CreateViewModel();

        vm.Profiles.Single().Badge.Should().Be("Profiles_Status_RestartNeeded");
        vm.Profiles.Single().ShowAttentionBadge.Should().BeTrue();
    }

    [Fact]
    public async Task StartInTrayAtSignIn_WhenTheSignInTaskFails_PutsTheSwitchBack()
    {
        _mockBackgroundTasks.Setup(b => b.SetSignInOptionAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(OperationResult.Failed("Access denied"));
        var vm = await CreateReadyViewModelAsync();

        vm.StartInTrayAtSignIn = true;

        vm.StartInTrayAtSignIn.Should().BeFalse();
    }

    [Fact]
    public async Task StartInTrayAtSignIn_WhenTheFolderIsUnsafe_PutsTheSwitchBackAndWarns()
    {
        _mockBackgroundTasks.Setup(b => b.SetSignInOptionAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(OperationResult.Failed(SignInTask.UnsafeFolderError));
        var vm = await CreateReadyViewModelAsync();

        vm.StartInTrayAtSignIn = true;

        vm.StartInTrayAtSignIn.Should().BeFalse();
        _mockDialogService.Verify(d => d.ShowWarningAsync("Settings_SignIn_UnsafeFolder_Message", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task StartInTrayAtSignIn_WhenTheSignInTaskFails_ShowsNoWarning()
    {
        _mockBackgroundTasks.Setup(b => b.SetSignInOptionAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(OperationResult.Failed("Access denied"));
        var vm = await CreateReadyViewModelAsync();

        vm.StartInTrayAtSignIn = true;

        _mockDialogService.Verify(d => d.ShowWarningAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task BackgroundTasksReady_WhenSignInTaskHasOptions_ShowsThemWithoutChangingTheTask()
    {
        GivenSignInOptions(startInTray: true, reapply: true);

        var vm = await CreateReadyViewModelAsync();

        vm.StartInTrayAtSignIn.Should().BeTrue();
        vm.ReapplyAtSignIn.Should().BeTrue();
        _mockBackgroundTasks.Verify(b => b.SetSignInOptionAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task StartInTrayAtSignIn_WhenSignInAnswerArrivesLate_KeepsTheUsersChoice()
    {
        var options = new TaskCompletionSource<SignInOptions>();
        _mockBackgroundTasks.Setup(b => b.GetSignInOptionsAsync()).Returns(options.Task);
        var vm = CreateViewModel();

        vm.StartInTrayAtSignIn = true;
        options.SetResult(SignInOptions.None);
        await vm.BackgroundTasksReady;

        vm.StartInTrayAtSignIn.Should().BeTrue();
    }

    [Fact]
    public void CanStartInTrayAtSignIn_WhenRunningAsTheSignedInUser_IsTrue()
    {
        var vm = CreateViewModel();

        vm.CanStartInTrayAtSignIn.Should().BeTrue();
        vm.StartInTrayDescription.Should().Be("Settings_StartInTray_Description");
    }

    [Fact]
    public void CanStartInTrayAtSignIn_UnderAnotherAccountsAdminRights_IsFalseAndSaysWhy()
    {
        _mockInteractiveUserService.Setup(i => i.IsOtsElevation).Returns(true);

        var vm = CreateViewModel();

        vm.CanStartInTrayAtSignIn.Should().BeFalse();
        vm.StartInTrayDescription.Should().Be("Settings_StartInTray_OtsUnavailable");
        vm.StartInTrayAtSignIn.Should().BeFalse();
    }

    [Fact]
    public async Task StartInTrayAtSignIn_UnderAnotherAccountsAdminRights_CanOnlyBeTurnedOff()
    {
        _mockInteractiveUserService.Setup(i => i.IsOtsElevation).Returns(true);
        GivenSignInOptions(startInTray: true);
        var vm = await CreateReadyViewModelAsync();
        vm.StartInTrayAtSignIn.Should().BeTrue();
        vm.CanStartInTrayAtSignIn.Should().BeTrue();
        vm.StartInTrayDescription.Should().Be("Settings_StartInTray_OtsUnavailable");
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.StartInTrayAtSignIn = false;

        _mockBackgroundTasks.Verify(b => b.SetSignInOptionAsync(SignInTask.TrayFlag, false), Times.Once);
        vm.CanStartInTrayAtSignIn.Should().BeFalse();
        changed.Should().Contain(nameof(SettingsViewModel.CanStartInTrayAtSignIn));
    }

    [Fact]
    public async Task StartInTrayAtSignIn_WithNoSignInTaskForTheUser_BothSwitchesAreOff()
    {
        var vm = await CreateReadyViewModelAsync();

        vm.StartInTrayAtSignIn.Should().BeFalse();
        vm.ReapplyAtSignIn.Should().BeFalse();
    }

    [Fact]
    public async Task StartInTrayAtSignIn_WhenTurnedOff_ClearsTheTrayFlag()
    {
        GivenSignInOptions(startInTray: true);
        var vm = await CreateReadyViewModelAsync();

        vm.StartInTrayAtSignIn = false;

        _mockBackgroundTasks.Verify(b => b.SetSignInOptionAsync(SignInTask.TrayFlag, false), Times.Once);
    }


    [Fact]
    public async Task SelectedUpdateCheckOption_WhenPicked_OffersEveryChoiceAndSavesThePick()
    {
        var vm = await CreateReadyViewModelAsync();

        vm.UpdateCheckOptions.Select(o => o.Value).Should().Equal(AppUpdateCheckFrequency.Off, AppUpdateCheckFrequency.Daily, AppUpdateCheckFrequency.Weekly);
        vm.UpdateActionOptions.Should().HaveCount(2);

        vm.SelectedUpdateCheckOption = vm.UpdateCheckOptions.Single(o => Equals(o.Value, AppUpdateCheckFrequency.Weekly));

        _mockPreferencesService.Verify(p => p.SetPreferenceAsync(UserPreferenceKeys.AppUpdateCheckFrequency, AppUpdateCheckFrequency.Weekly), Times.Once);
    }

    [Fact]
    public void Constructor_LoadsCurrentLanguage()
    {
        _mockLocalization
            .Setup(l => l.CurrentLanguage)
            .Returns("fr");

        var vm = CreateViewModel();

        vm.SelectedLanguage.Should().Be("fr");
    }

    [Fact]
    public void Constructor_LoadsCurrentTheme()
    {
        _mockThemeService
            .Setup(t => t.CurrentTheme)
            .Returns(WinhanceTheme.DarkNative);

        var vm = CreateViewModel();

        vm.SelectedTheme.Should().Be(WinhanceTheme.DarkNative);
    }

    [Fact]
    public void Constructor_InitializesLanguagesCollection()
    {
        var vm = CreateViewModel();

        vm.Languages.Should().NotBeEmpty();
        vm.Languages.Count.Should().Be(3);
        vm.Languages[0].DisplayText.Should().Be("English");
        vm.Languages[0].Value.Should().Be("en");
    }

    [Fact]
    public void Constructor_InitializesThemesCollection_WithThreeOptions()
    {
        var vm = CreateViewModel();

        vm.Themes.Should().HaveCount(3);
        vm.Themes.Select(t => t.Theme).Should().Contain(WinhanceTheme.System);
        vm.Themes.Select(t => t.Theme).Should().Contain(WinhanceTheme.LightNative);
        vm.Themes.Select(t => t.Theme).Should().Contain(WinhanceTheme.DarkNative);
    }

    [Fact]
    public void Constructor_SetsSelectedThemeOption_MatchingCurrentTheme()
    {
        _mockThemeService
            .Setup(t => t.CurrentTheme)
            .Returns(WinhanceTheme.LightNative);

        var vm = CreateViewModel();

        vm.SelectedThemeOption.Should().NotBeNull();
        vm.SelectedThemeOption!.Theme.Should().Be(WinhanceTheme.LightNative);
    }

    [Fact]
    public void SelectedLanguage_SetToNewValue_CallsLocalizationSetLanguage()
    {
        _mockLocalization
            .Setup(l => l.CurrentLanguage)
            .Returns("en");
        _mockLocalization
            .Setup(l => l.SetLanguage("fr"))
            .Returns(true);

        var vm = CreateViewModel();

        vm.SelectedLanguage = "fr";

        _mockLocalization.Verify(l => l.SetLanguage("fr"), Times.Once);
    }

    [Fact]
    public void SelectedLanguage_SetToSameAsCurrentLanguage_DoesNotCallSetLanguage()
    {
        _mockLocalization
            .Setup(l => l.CurrentLanguage)
            .Returns("en");

        var vm = CreateViewModel();

        vm.SelectedLanguage = "en";

        _mockLocalization.Verify(l => l.SetLanguage(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void SelectedLanguage_SetToEmpty_DoesNotCallSetLanguage()
    {
        var vm = CreateViewModel();

        vm.SelectedLanguage = "";

        _mockLocalization.Verify(l => l.SetLanguage(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void SelectedLanguage_WhenSetLanguageSucceeds_SavesPreference()
    {
        _mockLocalization
            .Setup(l => l.CurrentLanguage)
            .Returns("en");
        _mockLocalization
            .Setup(l => l.SetLanguage("de"))
            .Returns(true);
        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync("Language", "de"))
            .ReturnsAsync(OperationResult.Succeeded());

        var vm = CreateViewModel();

        vm.SelectedLanguage = "de";

        _mockPreferencesService.Verify(
            p => p.SetPreferenceAsync("Language", "de"),
            Times.Once);
    }

    [Fact]
    public void SelectedLanguage_WhenSetLanguageFails_DoesNotSavePreference()
    {
        _mockLocalization
            .Setup(l => l.CurrentLanguage)
            .Returns("en");
        _mockLocalization
            .Setup(l => l.SetLanguage("zz"))
            .Returns(false);

        var vm = CreateViewModel();

        vm.SelectedLanguage = "zz";

        _mockPreferencesService.Verify(
            p => p.SetPreferenceAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void SelectedLanguage_RaisesPropertyChanged()
    {
        var vm = CreateViewModel();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.SelectedLanguage))
                raised = true;
        };

        vm.SelectedLanguage = "es";

        raised.Should().BeTrue();
    }

    [Fact]
    public void SelectedTheme_SetToNewValue_CallsThemeServiceSetTheme()
    {
        _mockThemeService
            .Setup(t => t.CurrentTheme)
            .Returns(WinhanceTheme.System);

        var vm = CreateViewModel();

        vm.SelectedTheme = WinhanceTheme.DarkNative;

        _mockThemeService.Verify(t => t.SetTheme(WinhanceTheme.DarkNative), Times.Once);
    }

    [Fact]
    public void SelectedTheme_SetToSameAsCurrentTheme_DoesNotCallSetTheme()
    {
        _mockThemeService
            .Setup(t => t.CurrentTheme)
            .Returns(WinhanceTheme.System);

        var vm = CreateViewModel();

        vm.SelectedTheme = WinhanceTheme.System;

        _mockThemeService.Verify(t => t.SetTheme(It.IsAny<WinhanceTheme>()), Times.Never);
    }

    [Fact]
    public void SelectedThemeOption_SetToNonNull_UpdatesSelectedTheme()
    {
        var vm = CreateViewModel();
        var darkOption = vm.Themes.First(t => t.Theme == WinhanceTheme.DarkNative);

        vm.SelectedThemeOption = darkOption;

        vm.SelectedTheme.Should().Be(WinhanceTheme.DarkNative);
    }

    [Fact]
    public void SelectedThemeOption_SetToNull_DoesNotChangeSelectedTheme()
    {
        var vm = CreateViewModel();
        var originalTheme = vm.SelectedTheme;

        vm.SelectedThemeOption = null;

        vm.SelectedTheme.Should().Be(originalTheme);
    }

    [Fact]
    public void PageTitle_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString("Settings_Title"))
            .Returns("Settings Title");

        var vm = CreateViewModel();

        vm.PageTitle.Should().Be("Settings Title");
    }

    [Fact]
    public void PageTitle_WhenNull_ReturnsFallback()
    {
        _mockLocalization
            .Setup(l => l.GetString("Settings_Title"))
            .Returns((string?)null!);

        var vm = CreateViewModel();

        vm.PageTitle.Should().Be("Winhance Settings");
    }

    [Fact]
    public void PageDescription_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString("Settings_Description"))
            .Returns("My Desc");

        var vm = CreateViewModel();

        vm.PageDescription.Should().Be("My Desc");
    }

    [Fact]
    public void ImportButtonText_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString("Button_Import"))
            .Returns("Import Config");

        var vm = CreateViewModel();

        vm.ImportButtonText.Should().Be("Import Config");
    }

    [Fact]
    public void ExportButtonText_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString("Button_Export"))
            .Returns("Export Config");

        var vm = CreateViewModel();

        vm.ExportButtonText.Should().Be("Export Config");
    }

    [Fact]
    public void OnLanguageChanged_UpdatesThemeDisplayNames()
    {
        _mockLocalization
            .Setup(l => l.GetString("Theme_System"))
            .Returns("System");

        var vm = CreateViewModel();

        _mockLocalization
            .Setup(l => l.GetString("Theme_System"))
            .Returns("Systeme");
        _mockLocalization
            .Setup(l => l.GetString("Theme_LightNative"))
            .Returns("Clair");
        _mockLocalization
            .Setup(l => l.GetString("Theme_DarkNative"))
            .Returns("Sombre");

        _mockLocalization.Raise(l => l.LanguageChanged += null, EventArgs.Empty);

        var systemTheme = vm.Themes.First(t => t.Theme == WinhanceTheme.System);
        systemTheme.DisplayText.Should().Be("Systeme");

        var lightTheme = vm.Themes.First(t => t.Theme == WinhanceTheme.LightNative);
        lightTheme.DisplayText.Should().Be("Clair");

        var darkTheme = vm.Themes.First(t => t.Theme == WinhanceTheme.DarkNative);
        darkTheme.DisplayText.Should().Be("Sombre");
    }

    [Fact]
    public async Task OnLanguageChanged_RaisesPropertyChangedForAllLocalizedProperties()
    {
        var vm = CreateViewModel();
        // Without the UI thread's context the background load finishes on a pool thread, filling ModeOptions
        // while the language handler enumerates it.
        await vm.BackgroundTasksReady;
        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _mockLocalization.Raise(l => l.LanguageChanged += null, EventArgs.Empty);

        changedProperties.Should().Contain(nameof(vm.PageTitle));
        changedProperties.Should().Contain(nameof(vm.PageDescription));
        changedProperties.Should().Contain(nameof(vm.GeneralLabel));
        changedProperties.Should().Contain(nameof(vm.LanguageHeader));
        changedProperties.Should().Contain(nameof(vm.LanguageDescription));
        changedProperties.Should().Contain(nameof(vm.ThemeHeader));
        changedProperties.Should().Contain(nameof(vm.ThemeDescription));
        changedProperties.Should().Contain(nameof(vm.ConfigsProfilesLabel));
        changedProperties.Should().Contain(nameof(vm.ConfigFileHeader));
        changedProperties.Should().Contain(nameof(vm.ConfigFileDescription));
        changedProperties.Should().Contain(nameof(vm.ImportButtonText));
        changedProperties.Should().Contain(nameof(vm.ExportButtonText));
    }

    [Fact]
    public async Task ImportConfigCommand_CallsConfigurationService()
    {
        _mockConfigurationService
            .Setup(c => c.ImportConfigurationAsync())
            .Returns(Task.CompletedTask);

        var vm = CreateViewModel();

        await vm.ImportConfigCommand.ExecuteAsync(null);

        _mockConfigurationService.Verify(c => c.ImportConfigurationAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateRestorePointCommand_WhenCancelled_ShowsNoDialogAndEndsTheTaskAsCancelled()
    {
        var cts = new CancellationTokenSource();
        _mockTaskProgressService.Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>())).Returns(cts);
        _mockBackupService
            .Setup(b => b.CreateRestorePointAsync(It.IsAny<string?>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .Returns(async (string? _, IProgress<TaskProgressDetail>? _, CancellationToken token) =>
            {
                await cts.CancelAsync();
                token.ThrowIfCancellationRequested();
                return BackupResult.CreateFailure("not reached");
            });
        var vm = CreateViewModel();

        await vm.CreateRestorePointCommand.ExecuteAsync(null);

        _mockTaskProgressService.Verify(t => t.CancelTask(), Times.Once);
        _mockTaskProgressService.Verify(t => t.FailTask(), Times.Never);
        _mockDialogService.Verify(d => d.ShowWarningAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockDialogService.Verify(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        vm.IsCreatingRestorePoint.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateRestorePointCommand_FailsAfterTurningProtectionOn_OneDialogAsksWhetherToKeepIt(bool turnOff)
    {
        _mockTaskProgressService.Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>())).Returns(new CancellationTokenSource());
        _mockBackupService
            .Setup(b => b.CreateRestorePointAsync(It.IsAny<string?>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BackupResult.CreateFailure("disk full", turnedProtectionOn: true));
        ConfirmationRequest? asked = null;
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .Callback((ConfirmationRequest r) => asked = r)
            .ReturnsAsync(new ConfirmationResponse { Confirmed = turnOff });
        var vm = CreateViewModel();

        await vm.CreateRestorePointCommand.ExecuteAsync(null);

        asked.Should().NotBeNull();
        asked!.Message.Should().Contain("Settings_RestorePoint_Fail").And.Contain("disk full").And.Contain("RestorePoint_Fail_ProtectionTurnedOn");
        asked.ConfirmButtonText.Should().Be("RestorePoint_Fail_Button_TurnOff");
        asked.CancelButtonText.Should().Be("RestorePoint_Fail_Button_KeepOn");
        _mockBackupService.Verify(b => b.TurnOffSystemRestoreAsync(), turnOff ? Times.Once() : Times.Never());
        _mockDialogService.Verify(d => d.ShowWarningAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockTaskProgressService.Verify(t => t.FailTask(), Times.Once);
    }

    [Fact]
    public async Task CreateRestorePointCommand_FailsWithProtectionAlreadyOn_ShowsThePlainWarning()
    {
        _mockTaskProgressService.Setup(t => t.StartTask(It.IsAny<string>(), It.IsAny<bool>())).Returns(new CancellationTokenSource());
        _mockBackupService
            .Setup(b => b.CreateRestorePointAsync(It.IsAny<string?>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BackupResult.CreateFailure("disk full"));
        var vm = CreateViewModel();

        await vm.CreateRestorePointCommand.ExecuteAsync(null);

        _mockDialogService.Verify(d => d.ShowWarningAsync(It.Is<string>(m => m.Contains("disk full")), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockDialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
        _mockBackupService.Verify(b => b.TurnOffSystemRestoreAsync(), Times.Never);
    }

    [Fact]
    public async Task ExportConfigCommand_CallsConfigurationService()
    {
        _mockConfigurationService
            .Setup(c => c.ExportConfigurationAsync())
            .Returns(Task.CompletedTask);

        var vm = CreateViewModel();

        await vm.ExportConfigCommand.ExecuteAsync(null);

        _mockConfigurationService.Verify(c => c.ExportConfigurationAsync(), Times.Once);
    }

    [Fact]
    public async Task Dispose_UnsubscribesFromLanguageChanged()
    {
        var vm = await CreateReadyViewModelAsync();
        vm.Dispose();

        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _mockLocalization.Raise(l => l.LanguageChanged += null, EventArgs.Empty);

        changedProperties.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var vm = CreateViewModel();

        var act = () =>
        {
            vm.Dispose();
            vm.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task KeepRemovedEnabled_WhenServiceReportsOn_IsOn()
    {
        _mockBackgroundTasks.Setup(b => b.IsKeepRemovedOnAsync()).ReturnsAsync(true);

        var vm = await CreateReadyViewModelAsync();

        vm.KeepRemovedEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task KeepRemovedEnabled_WhenTurnedOn_AsksTheServiceToKeep()
    {
        var vm = await CreateReadyViewModelAsync();

        vm.KeepRemovedEnabled = true;

        _mockBackgroundTasks.Verify(b => b.SetKeepRemovedOnAsync(true), Times.Once);
    }

    [Fact]
    public async Task KeepRemovedEnabled_WhenTurnedOffInWinhanceMode_TurnsOffWithoutConfirming()
    {
        _mockBackgroundTasks.Setup(b => b.IsKeepRemovedOnAsync()).ReturnsAsync(true);
        var vm = await CreateReadyViewModelAsync();

        vm.KeepRemovedEnabled = false;

        _mockBackgroundTasks.Verify(b => b.SetKeepRemovedOnAsync(false), Times.Once);
        _mockDialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
    }

    [Fact]
    public async Task KeepRemovedEnabled_WhenTurnedOffInScriptsModeAndDeclined_StaysOn()
    {
        GivenScriptsWithAnEdgeTask();
        GivenConfirmation(false);
        var vm = await CreateReadyViewModelAsync();

        vm.KeepRemovedEnabled = false;
        await Eventually(() => vm.KeepRemovedEnabled);

        vm.KeepRemovedEnabled.Should().BeTrue();
        _mockBackgroundTasks.Verify(b => b.SetKeepRemovedOnAsync(false), Times.Never);
    }

    [Fact]
    public async Task KeepRemovedEnabled_WhenTurnedOffInScriptsModeAndConfirmed_TurnsOff()
    {
        GivenScriptsWithAnEdgeTask();
        GivenConfirmation(true);
        var vm = await CreateReadyViewModelAsync();

        vm.KeepRemovedEnabled = false;

        await Eventually(() => _mockBackgroundTasks.Invocations.Any(i => i.Method.Name == nameof(IBackgroundTaskService.SetKeepRemovedOnAsync)));
        _mockBackgroundTasks.Verify(b => b.SetKeepRemovedOnAsync(false), Times.Once);
    }

    [Fact]
    public async Task KeepRemovedEnabled_WhenTheChangeFails_RevertsTheSwitch()
    {
        _mockBackgroundTasks.Setup(b => b.SetKeepRemovedOnAsync(true)).ReturnsAsync(OperationResult.Failed("no"));
        var vm = await CreateReadyViewModelAsync();

        vm.KeepRemovedEnabled = true;
        await Eventually(() => !vm.KeepRemovedEnabled);

        vm.KeepRemovedEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, "BackgroundTasks_ChecksOnOpen")]
    [InlineData(true, "BackgroundTasks_ChecksAtSignIn")]
    public async Task KeepRemovedDescription_Always_SaysWhenWinhanceChecks(bool signInTask, string key)
    {
        GivenSignInOptions(startInTray: signInTask, maintain: signInTask);

        var vm = await CreateReadyViewModelAsync();

        vm.KeepRemovedDescription.Should().EndWith(key);
    }

    [Fact]
    public async Task ModeOptions_Always_SelectTheCurrentMode()
    {
        _mockBackgroundTasks.Setup(b => b.GetMode()).Returns(KeepRemovedMode.Scripts);

        var vm = await CreateReadyViewModelAsync();

        vm.ModeOptions.Should().HaveCount(2);
        vm.SelectedModeOption!.Value.Should().Be(KeepRemovedMode.Scripts);
    }

    [Fact]
    public async Task SelectedModeOption_WhenAnotherModeIsPickedAndConfirmed_SwitchesMode()
    {
        GivenConfirmation(true);
        var vm = await CreateReadyViewModelAsync();

        vm.SelectedModeOption = vm.ModeOptions.First(o => Equals(o.Value, KeepRemovedMode.Scripts));

        await Eventually(() => _mockBackgroundTasks.Invocations.Any(i => i.Method.Name == nameof(IBackgroundTaskService.SetModeAsync)));
        _mockBackgroundTasks.Verify(b => b.SetModeAsync(KeepRemovedMode.Scripts), Times.Once);
    }

    [Fact]
    public async Task SelectedModeOption_WhenConfirmationIsCancelled_KeepsTheMode()
    {
        GivenConfirmation(false);
        var vm = await CreateReadyViewModelAsync();
        var winhance = vm.SelectedModeOption;

        vm.SelectedModeOption = vm.ModeOptions.First(o => Equals(o.Value, KeepRemovedMode.Scripts));
        await Eventually(() => ReferenceEquals(vm.SelectedModeOption, winhance));

        vm.SelectedModeOption.Should().BeSameAs(winhance);
        _mockBackgroundTasks.Verify(b => b.SetModeAsync(It.IsAny<KeepRemovedMode>()), Times.Never);
    }

    private void GivenScriptsWithAnEdgeTask(RemovalTaskHealth health = RemovalTaskHealth.Active)
    {
        _mockBackgroundTasks.Setup(b => b.GetMode()).Returns(KeepRemovedMode.Scripts);
        _mockBackgroundTasks.Setup(b => b.IsKeepRemovedOnAsync()).ReturnsAsync(true);
        _mockBackgroundTasks.Setup(b => b.GetRemovalTasksAsync()).ReturnsAsync(
            [new RemovalTaskState("EdgeRemoval", health == RemovalTaskHealth.NoTask ? null : health != RemovalTaskHealth.TaskDisabled,
                health != RemovalTaskHealth.ScriptMissing, "e.ps1", null, null, ["Microsoft Edge", "Edge Update"])]);
    }

    private void GivenReadableDetails()
    {
        _mockLocalization
            .Setup(l => l.GetString("BackgroundTasks_Removes", It.IsAny<object[]>()))
            .Returns((string _, object[] a) => $"Removes: {a[0]}");
        _mockLocalization
            .Setup(l => l.GetString("BackgroundTasks_TaskAndScript", It.IsAny<object[]>()))
            .Returns((string _, object[] a) => $"Task: {a[0]} | Script: {a[1]}");
    }

    private static List<CategoryRow> Categories(SettingsViewModel vm) => vm.KeepRemovedChildren.OfType<CategoryRow>().ToList();

    private static TaskRow LastCheckRow(SettingsViewModel vm) => vm.KeepRemovedChildren.OfType<TaskRow>().First();

    [Fact]
    public async Task KeepRemovedEnabled_WhenTurnedOnWithAnEmptyList_StaysOnAndShowsTheEmptyNote()
    {
        var on = false;
        _mockBackgroundTasks.Setup(b => b.IsKeepRemovedOnAsync()).ReturnsAsync(() => on);
        _mockBackgroundTasks.Setup(b => b.SetKeepRemovedOnAsync(true)).Callback(() => on = true).ReturnsAsync(OperationResult.Succeeded());
        var vm = await CreateReadyViewModelAsync();

        vm.KeepRemovedEnabled = true;
        await Eventually(() => _mockBackgroundTasks.Invocations.Any(i => i.Method.Name == nameof(IBackgroundTaskService.SetKeepRemovedOnAsync)));
        await Task.Delay(150);

        vm.KeepRemovedEnabled.Should().BeTrue();
        vm.KeepRemovedChildren.OfType<EmptyListRow>().Should().ContainSingle();
    }

    [Fact]
    public async Task KeepRemovedChildren_WhenEmptyNoteButtonPressed_OpensSoftwareAndApps()
    {
        var vm = await CreateReadyViewModelAsync();

        vm.KeepRemovedChildren.OfType<EmptyListRow>().Single().Action.Execute(null);

        _mockEventBus.Verify(e => e.Publish(It.Is<PageRequestedEvent>(ev => ev.PageTag == "SoftwareApps")), Times.Once);
    }

    [Fact]
    public async Task KeepRemovedChildren_WithKeptApps_ShowNoEmptyNote()
    {
        _mockBackgroundTasks.Setup(b => b.GetKeptApps()).Returns([new KeptApp("a", "App A", null, 0)]);

        var vm = await CreateReadyViewModelAsync();

        vm.KeepRemovedChildren.OfType<EmptyListRow>().Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public async Task KeepRemovedChildren_ModeRow_IsEnabledOnlyWithSwitchOnAndAKeptList(bool on, bool hasApps, bool enabled)
    {
        _mockBackgroundTasks.Setup(b => b.IsKeepRemovedOnAsync()).ReturnsAsync(on);
        _mockBackgroundTasks.Setup(b => b.GetKeptApps()).Returns(hasApps ? [new KeptApp("a", "App A", null, 0)] : []);

        var vm = await CreateReadyViewModelAsync();

        vm.KeepRemovedChildren[0].Should().BeOfType<ModeRow>().Which.Enabled.Should().Be(enabled);
    }

    [Fact]
    public async Task KeepRemovedChildren_InWinhanceMode_BuildOneRowPerCategoryFromTheKeptList()
    {
        _mockBackgroundTasks.Setup(b => b.IsKeepRemovedOnAsync()).ReturnsAsync(true);
        GivenReadableDetails();
        _mockBackgroundTasks.Setup(b => b.GetKeptApps()).Returns(
        [
            new KeptApp("a", "Copilot", null, 1, "BloatRemoval"),
            new KeptApp("b", "Xbox", null, 2, "BloatRemoval"),
            new KeptApp("c", "OneDrive", null, 0, "OneDriveRemoval"),
        ]);

        var vm = await CreateReadyViewModelAsync();

        var rows = Categories(vm);
        rows.Select(r => (r.Icon, r.IconPack)).Should().Equal(("Apps", "FluentColor"), ("Cloud", "Fluent"));
        rows[0].Detail.Should().StartWith("Removes: Copilot, Xbox");
        rows[0].CanStop.Should().BeTrue();
        rows[0].CanRemove.Should().BeFalse();
        rows[0].Active.Should().BeTrue();
    }

    [Fact]
    public async Task KeepRemovedChildren_InScriptsMode_HistoryShowsMissingArtifactsWithNoTaskActions()
    {
        var removedOn = new DateTime(2026, 10, 1);
        GivenReadableRunDetails();
        _mockBackgroundTasks.Setup(b => b.GetMode()).Returns(KeepRemovedMode.Scripts);
        _mockBackgroundTasks.Setup(b => b.GetKeptApps()).Returns([new KeptApp("a", "App A", removedOn, 0)]);

        var vm = await CreateReadyViewModelAsync();

        var row = Categories(vm).Should().ContainSingle().Which;
        row.TaskStatus.Should().Be("BackgroundTasks_TaskMissing");
        row.ScriptStatus.Should().Be("BackgroundTasks_ScriptMissing");
        row.Detail.Should().StartWith("Removed App A");
        row.Active.Should().BeFalse();
        row.HasTaskActions.Should().BeFalse();
        row.CanStop.Should().BeFalse();
    }

    [Fact]
    public async Task Stop_OnCategoryRow_ConfirmsThenDropsItsApps()
    {
        GivenConfirmation(true);
        var kept = new List<KeptApp>
        {
            new("a", "Copilot", null, 0, "BloatRemoval"),
            new("b", "Edge", null, 0, "EdgeRemoval"),
        };
        _mockBackgroundTasks.Setup(b => b.GetKeptApps()).Returns(() => kept.ToList());
        _mockBackgroundTasks.Setup(b => b.RemoveKeptApp("a")).Callback(() => kept.RemoveAll(k => k.Id == "a"));
        var vm = await CreateReadyViewModelAsync();

        var bloat = Categories(vm).Single(r => r.Icon == "Apps");
        await OnUi(() => bloat.Stop!.Execute(bloat));
        await Eventually(() => Categories(vm).Count == 1);

        _mockBackgroundTasks.Verify(b => b.RemoveKeptApp("a"), Times.Once);
        _mockBackgroundTasks.Verify(b => b.RemoveKeptApp("b"), Times.Never);
        Categories(vm).Single().Icon.Should().Be("Globe");
    }

    [Fact]
    public async Task Stop_WhenConfirmationIsCancelled_LeavesTheListAlone()
    {
        GivenConfirmation(false);
        _mockBackgroundTasks.Setup(b => b.GetKeptApps()).Returns([new KeptApp("a", "Copilot", null, 0, "BloatRemoval")]);
        var vm = await CreateReadyViewModelAsync();

        var row = Categories(vm).Single();
        row.Stop!.Execute(row);
        await Task.Delay(100);

        _mockBackgroundTasks.Verify(b => b.RemoveKeptApp(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task KeepRemovedChildren_InScriptsMode_ShowARowPerTaskWithPillAndPaths()
    {
        GivenReadableDetails();
        _mockBackgroundTasks.Setup(b => b.GetRemovalTasksAsync()).ReturnsAsync(
        [
            new RemovalTaskState("EdgeRemoval", true, true, "e.ps1", null, null, ["Microsoft Edge"]),
            new RemovalTaskState("BloatRemoval", true, false, "b.ps1", null, null, []),
        ]);
        _mockBackgroundTasks.Setup(b => b.GetMode()).Returns(KeepRemovedMode.Scripts);

        var vm = await CreateReadyViewModelAsync();

        var rows = Categories(vm);
        rows.Select(r => (r.Icon, r.TaskStatus, r.ScriptStatus, r.Active, r.CanRepair, r.CanRemove)).Should().Equal(
            ("Apps", "BackgroundTasks_TaskEnabled", "BackgroundTasks_ScriptMissing", false, true, true),
            ("Globe", "BackgroundTasks_TaskEnabled", "BackgroundTasks_ScriptPresent", true, false, true));
        rows[1].Detail.Should().Be("Removes: Microsoft Edge\nTask: \\Winhance\\EdgeRemoval | Script: e.ps1\nBackgroundTasks_Task_NeverRan");
    }

    [Fact]
    public async Task Repair_OnCategoryRow_AsksTheServiceToRepairThatTask()
    {
        GivenScriptsWithAnEdgeTask(RemovalTaskHealth.NoTask);
        _mockBackgroundTasks.Setup(b => b.RepairRemovalTaskAsync("EdgeRemoval")).ReturnsAsync(OperationResult.Succeeded());
        var vm = await CreateReadyViewModelAsync();

        var row = Categories(vm).Single();
        row.Repair!.Execute(row);

        _mockBackgroundTasks.Verify(b => b.RepairRemovalTaskAsync("EdgeRemoval"), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Remove_OnCategoryRow_OnlyRemovesAfterConfirmation(bool confirmed)
    {
        GivenConfirmation(confirmed);
        GivenScriptsWithAnEdgeTask();
        _mockBackgroundTasks.Setup(b => b.RemoveRemovalTaskAsync("EdgeRemoval"))
            .Callback(() => _mockBackgroundTasks.Setup(b => b.GetRemovalTasksAsync()).ReturnsAsync([]))
            .ReturnsAsync(OperationResult.Succeeded());
        var vm = await CreateReadyViewModelAsync();

        var row = Categories(vm).Single();
        await OnUi(() => row.Remove!.Execute(row));
        if (confirmed)
            await Eventually(() => Categories(vm).Count == 0);

        _mockBackgroundTasks.Verify(b => b.RemoveRemovalTaskAsync("EdgeRemoval"), confirmed ? Times.Once() : Times.Never());
        Categories(vm).Should().HaveCount(confirmed ? 0 : 1);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(null, true)]
    [InlineData(null, false)]
    public async Task EdgeRedirectRow_ShowsScriptStatusWithoutTaskControls(bool? taskEnabled, bool scriptExists)
    {
        _mockBackgroundTasks.Setup(b => b.GetEdgeRedirectAsync()).ReturnsAsync(
            new RemovalTaskState("OpenWebSearchRepair", taskEnabled, scriptExists, "repair.ps1", null, null, []));
        var vm = await CreateReadyViewModelAsync();

        var row = Categories(vm).Single();
        row.Title.Should().Be("BackgroundTasks_Redirect_Title");
        row.ShowScriptPresent.Should().Be(scriptExists);
        row.ShowScriptMissing.Should().Be(!scriptExists);
        row.HasTaskActions.Should().BeFalse();
        row.CanStop.Should().BeFalse();
        row.Toggle.Should().BeNull();
        row.Repair.Should().BeNull();
        row.Remove.Should().BeNull();
    }

    [Fact]
    public async Task KeepRemovedChildren_WithEdgeRedirect_PlacesItUnderEdgeRemovalEvenWithSwitchOff()
    {
        _mockAppIcons.Setup(r => r.CachedIconPath("windows-app-edge", It.IsAny<bool>())).Returns("edge.png");
        _mockAppIcons.Setup(r => r.CachedIconPath("windows-app-onedrive", It.IsAny<bool>())).Returns("onedrive.png");
        _mockBackgroundTasks.Setup(b => b.GetKeptApps()).Returns(
        [
            new KeptApp("a", "Copilot", null, 0, "BloatRemoval"),
            new KeptApp("b", "Edge", null, 0, "EdgeRemoval"),
            new KeptApp("c", "OneDrive", null, 0, "OneDriveRemoval"),
        ]);
        _mockBackgroundTasks.Setup(b => b.GetEdgeRedirectAsync()).ReturnsAsync(
            new RemovalTaskState("OpenWebSearchRepair", true, true, "C:\\f", null, null, []));

        var vm = await CreateReadyViewModelAsync();

        var rows = Categories(vm);
        rows.Select(r => (r.Icon, r.IconPack)).Should().Equal(
            ("Apps", "FluentColor"), ("edge.png", "AppFile"), ("Globe", "FluentColor"), ("onedrive.png", "AppFile"));
        rows[2].Title.Should().Be("BackgroundTasks_Redirect_Title");
        rows[2].Active.Should().BeTrue();
        rows[2].HasTaskActions.Should().BeFalse();
    }

    [Fact]
    public async Task KeepRemovedChildren_WhenEdgeRepairScriptIsGone_ShowsTheScriptMissingText()
    {
        _mockBackgroundTasks.Setup(b => b.GetEdgeRedirectAsync()).ReturnsAsync(
            new RemovalTaskState("OpenWebSearchRepair", true, false, "C:\\f", null, null, []));

        var vm = await CreateReadyViewModelAsync();

        var row = Categories(vm).Single();
        row.TaskStatus.Should().Be("BackgroundTasks_TaskEnabled");
        row.ScriptStatus.Should().Be("BackgroundTasks_ScriptMissing");
        row.Active.Should().BeFalse();
        row.EnableTask.Should().BeNull();
    }

    [Fact]
    public async Task KeepRemovedChildren_WithoutRedirectTaskOrFolder_HasNoRedirectRow()
    {
        _mockBackgroundTasks.Setup(b => b.GetKeptApps()).Returns([new KeptApp("b", "Edge", null, 0, "EdgeRemoval")]);

        var vm = await CreateReadyViewModelAsync();

        Categories(vm).Select(r => r.Title).Should().NotContain("BackgroundTasks_Redirect_Title");
    }

    [Fact]
    public async Task Maintained_WhenACheckFinishes_RefreshesTheRows()
    {
        var vm = await CreateReadyViewModelAsync();
        _mockBackgroundTasks.Setup(b => b.GetKeptApps()).Returns([new KeptApp("a", "App A", null, 0)]);

        await OnUi(() => _mockBackgroundTasks.Raise(b => b.Maintained += null, EventArgs.Empty));
        await Eventually(() => Categories(vm).Count == 1);

        Categories(vm).Should().ContainSingle();
    }

    [Fact]
    public async Task KeepRemovedChildren_WithLockedFolderInScriptsMode_ShowsTheProtectedNote()
    {
        GivenScriptsWithAnEdgeTask();

        var vm = await CreateReadyViewModelAsync();

        vm.KeepRemovedChildren.OfType<FolderSafeRow>().Should().ContainSingle();
        vm.KeepRemovedChildren.OfType<FolderUnsafeRow>().Should().BeEmpty();
    }

    [Fact]
    public async Task KeepRemovedChildren_WhenSafetyCheckFails_ShowsTheWarningWithARepairButton()
    {
        GivenScriptsWithAnEdgeTask();
        _mockBackgroundTasks.Setup(b => b.IsTaskSafetyOkAsync()).ReturnsAsync(false);
        _mockBackgroundTasks.Setup(b => b.RepairTaskSafetyAsync()).ReturnsAsync(OperationResult.Failed("Unsafe folder"));
        var vm = await CreateReadyViewModelAsync();

        vm.KeepRemovedChildren.OfType<FolderUnsafeRow>().Single().Action.Execute(null);

        vm.KeepRemovedChildren.OfType<FolderSafeRow>().Should().BeEmpty();
        _mockBackgroundTasks.Verify(b => b.RepairTaskSafetyAsync(), Times.Once);
    }

    [Fact]
    public async Task RecheckTaskSafetyAsync_WhenLockBreaksThenReturns_FlipsTheSafetyNoteAndBack()
    {
        GivenScriptsWithAnEdgeTask();
        var vm = await CreateReadyViewModelAsync();
        vm.KeepRemovedChildren.OfType<FolderSafeRow>().Should().ContainSingle();

        _mockBackgroundTasks.Setup(b => b.IsTaskSafetyOkAsync()).ReturnsAsync(false);
        await vm.RecheckTaskSafetyAsync();

        vm.KeepRemovedChildren.OfType<FolderUnsafeRow>().Should().ContainSingle();
        vm.KeepRemovedChildren.OfType<FolderSafeRow>().Should().BeEmpty();

        _mockBackgroundTasks.Setup(b => b.IsTaskSafetyOkAsync()).ReturnsAsync(true);
        await vm.RecheckTaskSafetyAsync();

        vm.KeepRemovedChildren.OfType<FolderSafeRow>().Should().ContainSingle();
    }

    [Fact]
    public async Task RecheckTaskSafetyAsync_WhenLockIsUnchanged_LeavesTheRowsAlone()
    {
        GivenScriptsWithAnEdgeTask();
        var vm = await CreateReadyViewModelAsync();
        var before = vm.KeepRemovedChildren.ToList();

        await vm.RecheckTaskSafetyAsync();

        vm.KeepRemovedChildren.Should().Equal(before);
    }

    [Theory]
    [InlineData("EdgeRemoval", true, false)]
    [InlineData("EdgeRemoval", false, true)]
    [InlineData("BloatRemoval", true, false)]
    [InlineData("BloatRemoval", false, true)]
    public async Task TaskActions_UseTheIndividualTaskStateRatherThanTheParentSwitch(string name, bool taskEnabled, bool enable)
    {
        _mockBackgroundTasks.Setup(b => b.GetRemovalTasksAsync()).ReturnsAsync(
            [new RemovalTaskState(name, taskEnabled, true, "removal.ps1", null, null, [])]);
        _mockBackgroundTasks.Setup(b => b.SetBackgroundTaskEnabledAsync(name, enable)).ReturnsAsync(OperationResult.Succeeded());
        var vm = await CreateReadyViewModelAsync();

        var row = Categories(vm).Single();
        vm.KeepRemovedEnabled.Should().BeFalse();
        row.Active.Should().Be(taskEnabled);
        row.EnableTask.Should().Be(enable);
        row.CanToggle.Should().BeTrue();
        row.CanRemove.Should().BeTrue();
        row.HasTaskActions.Should().BeTrue();
        await OnUi(() => row.Toggle!.Execute(row));

        _mockBackgroundTasks.Verify(b => b.SetBackgroundTaskEnabledAsync(name, enable), Times.Once);
    }

    [Fact]
    public async Task KeepRemovedChildren_InWinhanceModeWithNoRedirect_ShowNoSafetyNote()
    {
        _mockBackgroundTasks.Setup(b => b.GetKeptApps()).Returns([new KeptApp("a", "App A", null, 0)]);

        var vm = await CreateReadyViewModelAsync();

        vm.KeepRemovedChildren.OfType<FolderSafeRow>().Should().BeEmpty();
        vm.KeepRemovedChildren.OfType<FolderUnsafeRow>().Should().BeEmpty();
    }

    [Fact]
    public async Task KeepRemovedChildren_InWinhanceModeWithEdgeRedirect_ShowTheSafetyNote()
    {
        _mockBackgroundTasks.Setup(b => b.GetEdgeRedirectAsync()).ReturnsAsync(
            new RemovalTaskState("OpenWebSearchRepair", true, true, "C:\\f", null, null, []));

        var vm = await CreateReadyViewModelAsync();

        vm.KeepRemovedChildren.OfType<FolderSafeRow>().Should().ContainSingle();
    }

    [Theory]
    [InlineData(KeepRemovedResult.NoApps, "BackgroundTasks_Result_NoApps")]
    [InlineData(KeepRemovedResult.AllStillRemoved, "BackgroundTasks_Result_AllStillRemoved")]
    [InlineData(KeepRemovedResult.RemovedAgain, "BackgroundTasks_Result_RemovedAgain")]
    [InlineData(KeepRemovedResult.RemovalFailed, "BackgroundTasks_Result_RemovalFailed")]
    public async Task KeepRemovedChildren_LastCheckRow_ShowsResultCodeAsLocalizedText(string code, string key)
    {
        _mockBackgroundTasks.Setup(b => b.GetStatus()).Returns(new KeepRemovedStatus(DateTime.Now, code, ["x"], null));
        _mockLocalization
            .Setup(l => l.GetString("BackgroundTasks_LastCheck", It.IsAny<object[]>()))
            .Returns((string _, object[] args) => (string)args[1]);
        _mockLocalization
            .Setup(l => l.GetString("BackgroundTasks_Result_RemovedAgain", It.IsAny<object[]>()))
            .Returns("BackgroundTasks_Result_RemovedAgain");

        var vm = await CreateReadyViewModelAsync();

        LastCheckRow(vm).Title.Should().Be(key);
    }

    [Fact]
    public async Task KeepRemovedChildren_LastCheckRowWithUnknownResult_ShowsItAsIs()
    {
        _mockBackgroundTasks.Setup(b => b.GetStatus()).Returns(new KeepRemovedStatus(DateTime.Now, "Something new", [], null));
        _mockLocalization
            .Setup(l => l.GetString("BackgroundTasks_LastCheck", It.IsAny<object[]>()))
            .Returns((string _, object[] args) => (string)args[1]);

        var vm = await CreateReadyViewModelAsync();

        LastCheckRow(vm).Title.Should().Be("Something new");
    }

    [Fact]
    public async Task KeepRemovedChildren_WithRecentPutBack_ShowsItUnderTheCheckRowWithCatalogNames()
    {
        var at = DateTime.Now.AddDays(-1);
        _mockBackgroundTasks.Setup(b => b.GetKeptApps()).Returns([new KeptApp("a", "App A", null, 1)]);
        _mockBackgroundTasks.Setup(b => b.GetStatus()).Returns(new KeepRemovedStatus(DateTime.Now, KeepRemovedResult.AllStillRemoved, ["a", "gone"], at));
        _mockLocalization
            .Setup(l => l.GetString("BackgroundTasks_LastPutBack", It.IsAny<object[]>()))
            .Returns((string _, object[] args) => $"{args[0]}|{args[1]}");

        var vm = await CreateReadyViewModelAsync();

        LastCheckRow(vm).Detail.Should().Be($"App A, gone|{at.ToString("g", System.Globalization.CultureInfo.CurrentCulture)}");
    }

    [Fact]
    public async Task KeepRemovedChildren_WithOldOrMissingPutBack_LeavesTheLineEmpty()
    {
        _mockBackgroundTasks.Setup(b => b.GetStatus()).Returns(new KeepRemovedStatus(DateTime.Now, KeepRemovedResult.AllStillRemoved, ["a"], DateTime.Now.AddDays(-90)));

        var vm = await CreateReadyViewModelAsync();

        LastCheckRow(vm).Detail.Should().BeEmpty();
    }

    [Fact]
    public async Task KeepRemovedChildren_WithUnknownWinhanceTask_ShowsAPlainRowAtTheBottomWithWhatItRuns()
    {
        _mockBackgroundTasks.Setup(b => b.GetKeptApps()).Returns([new KeptApp("a", "App A", null, 0)]);
        _mockBackgroundTasks.Setup(b => b.GetOtherTasksAsync()).ReturnsAsync(
        [
            new WinhanceTaskInfo("Custom", "C:\\tool.exe", "--go", "SYSTEM", true),
        ]);

        var vm = await CreateReadyViewModelAsync();

        var last = vm.KeepRemovedChildren[^1].Should().BeOfType<TaskRow>().Subject;
        last.Title.Should().Be("Custom");
        last.Detail.Should().Be("C:\\tool.exe --go\nBackgroundTasks_RunsAs\nBackgroundTasks_Task_NeverRan");
    }

    private void GivenReadableRunDetails()
    {
        _mockLocalization
            .Setup(l => l.GetString("BackgroundTasks_RunsAs", It.IsAny<object[]>()))
            .Returns((string _, object[] a) => $"As {a[0]}");
        _mockLocalization
            .Setup(l => l.GetString("BackgroundTasks_RemovedOn", It.IsAny<object[]>()))
            .Returns((string _, object[] a) => $"Removed {a[0]}");
        _mockLocalization
            .Setup(l => l.GetString("BackgroundTasks_Task_LastRun", It.IsAny<object[]>()))
            .Returns((string _, object[] a) => $"Ran {a[0]} {a[1]}");
        _mockLocalization
            .Setup(l => l.GetString("BackgroundTasks_Task_Failed", It.IsAny<object[]>()))
            .Returns((string _, object[] a) => $"Failed {a[0]}");
    }

    private static string ShortDateTime(DateTime at) => at.ToString("g", System.Globalization.CultureInfo.CurrentCulture);

    [Theory]
    [InlineData(0, "BackgroundTasks_Task_Succeeded")]
    [InlineData(0x41301, "BackgroundTasks_Task_Running")]
    [InlineData(0x1, "Failed 0x00000001")]
    public async Task KeepRemovedChildren_OtherTaskRows_ShowWhoRunsThemAndHowTheLastRunEnded(int code, string outcome)
    {
        GivenReadableRunDetails();
        var at = new DateTime(2026, 11, 3, 14, 30, 0);
        _mockBackgroundTasks.Setup(b => b.GetOtherTasksAsync()).ReturnsAsync(
            [new WinhanceTaskInfo("Custom", "C:\\tool.exe", "--go", "SYSTEM", true, at, code)]);

        var vm = await CreateReadyViewModelAsync();

        vm.KeepRemovedChildren.OfType<TaskRow>().Last().Detail
            .Should().Be($"C:\\tool.exe --go\nAs SYSTEM\nRan {ShortDateTime(at)} {outcome}");
    }

    [Fact]
    public async Task KeepRemovedChildren_OtherTaskRowWithoutRunAccount_SkipsThatLine()
    {
        _mockBackgroundTasks.Setup(b => b.GetOtherTasksAsync()).ReturnsAsync(
            [new WinhanceTaskInfo("Custom", "C:\\tool.exe", "", "", true)]);

        var vm = await CreateReadyViewModelAsync();

        vm.KeepRemovedChildren.OfType<TaskRow>().Last().Detail
            .Should().Be("C:\\tool.exe\nBackgroundTasks_Task_NeverRan");
    }

    [Fact]
    public async Task KeepRemovedChildren_RemovalTaskRows_ShowTheLastRunUnlessTheTaskIsMissing()
    {
        GivenReadableRunDetails();
        var at = new DateTime(2026, 11, 3, 14, 30, 0);
        _mockBackgroundTasks.Setup(b => b.GetMode()).Returns(KeepRemovedMode.Scripts);
        _mockBackgroundTasks.Setup(b => b.GetRemovalTasksAsync()).ReturnsAsync(
        [
            new RemovalTaskState("EdgeRemoval", true, true, "e.ps1", at, 0, []),
            new RemovalTaskState("BloatRemoval", null, true, "b.ps1", null, null, []),
        ]);

        var vm = await CreateReadyViewModelAsync();

        var rows = Categories(vm);
        rows[0].Detail.Should().NotContain("Ran ").And.NotContain("NeverRan");
        rows[1].Detail.Should().EndWith($"Ran {ShortDateTime(at)} BackgroundTasks_Task_Succeeded");
    }

    [Fact]
    public async Task KeepRemovedChildren_EdgeRedirectRow_ShowsWhenItsTaskLastRan()
    {
        GivenReadableRunDetails();
        var at = new DateTime(2026, 11, 3, 14, 30, 0);
        _mockBackgroundTasks.Setup(b => b.GetEdgeRedirectAsync()).ReturnsAsync(
            new RemovalTaskState("OpenWebSearch", true, true, "folder", at, 0, []));

        var vm = await CreateReadyViewModelAsync();

        Categories(vm).Single().Detail.Should().EndWith($"\nRan {ShortDateTime(at)} BackgroundTasks_Task_Succeeded");
    }

    [Fact]
    public async Task KeepRemovedChildren_KeptAppRows_ShowWhenEachAppWasRemoved()
    {
        GivenReadableDetails();
        GivenReadableRunDetails();
        var removed = new DateTime(2026, 10, 1);
        _mockBackgroundTasks.Setup(b => b.GetKeptApps()).Returns(
        [
            new KeptApp("a", "Copilot", removed, 0, "BloatRemoval"),
            new KeptApp("b", "Xbox", null, 0, "BloatRemoval"),
        ]);

        var vm = await CreateReadyViewModelAsync();

        Categories(vm).Single().Detail.Should().Contain(
            $"Removed Copilot ({removed.ToString("d", System.Globalization.CultureInfo.CurrentCulture)})");
    }

    [Fact]
    public async Task Primary_OnUnknownTaskRow_UnregistersTheTaskAndReloadsTheList()
    {
        var tasks = new List<WinhanceTaskInfo> { new("Custom", "c", "", "SYSTEM", true) };
        _mockBackgroundTasks.Setup(b => b.GetOtherTasksAsync()).ReturnsAsync(() => tasks.ToList());
        _mockScheduledTaskService
            .Setup(s => s.UnregisterScheduledTaskAsync("Custom"))
            .Callback(() => tasks.Clear())
            .ReturnsAsync(OperationResult.Succeeded());
        var vm = await CreateReadyViewModelAsync();

        var custom = (TaskRow)vm.KeepRemovedChildren[^1];
        await OnUi(() => custom.Primary!.Execute(custom));
        await Eventually(() => vm.KeepRemovedChildren.OfType<TaskRow>().All(r => r.Title != "Custom"));

        vm.KeepRemovedChildren.OfType<TaskRow>().Select(r => r.Title).Should().NotContain("Custom");
    }

    private Task OnUi(Action action) => Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, _ui);

    private async Task Eventually(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !await Task.Factory.StartNew(condition, CancellationToken.None, TaskCreationOptions.None, _ui); i++)
            await Task.Delay(20);
    }
}

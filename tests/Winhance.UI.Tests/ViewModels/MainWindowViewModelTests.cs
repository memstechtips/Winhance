using FluentAssertions;
using Microsoft.UI.Xaml;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class MainWindowViewModelTests : IDisposable
{
    private readonly Mock<IThemeService> _mockThemeService = new();
    private readonly Mock<IConfigurationService> _mockConfigurationService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IVersionService> _mockVersionService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IInteractiveUserService> _mockInteractiveUserService = new();
    private readonly Mock<IWinGetStartupService> _mockWinGetStartupService = new();
    private readonly Mock<IWindowsVersionFilterService> _mockWindowsVersionFilterService = new();

    // Child ViewModel dependencies
    private readonly Mock<ITaskProgressService> _mockTaskProgressService = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<IConfigReviewModeService> _mockConfigReviewModeService = new();
    private readonly Mock<IConfigReviewDiffService> _mockConfigReviewDiffService = new();
    private readonly Mock<IConfigReviewBadgeService> _mockConfigReviewBadgeService = new();
    private readonly Mock<IInternetConnectivityService> _mockInternetConnectivityService = new();

    private readonly TaskProgressViewModel _taskProgressViewModel;
    private readonly UpdateCheckViewModel _updateCheckViewModel;
    private readonly ReviewModeBarViewModel _reviewModeBarViewModel;

    public MainWindowViewModelTests()
    {
        // Set up dispatcher to execute actions synchronously
        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());
        _mockDispatcherService
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(f => f());

        // Default localization returns null so fallbacks are used
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => null!);

        // Default theme
        _mockThemeService
            .Setup(t => t.GetEffectiveTheme())
            .Returns(ElementTheme.Dark);

        // Default version info
        _mockVersionService
            .Setup(v => v.GetCurrentVersion())
            .Returns(new VersionInfo { Version = "v25.05.01" });

        // Default interactive user service (not OTS)
        _mockInteractiveUserService
            .Setup(i => i.IsOtsElevation)
            .Returns(false);

        // Create child ViewModels
        _taskProgressViewModel = new TaskProgressViewModel(
            _mockTaskProgressService.Object,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockLogService.Object);

        _updateCheckViewModel = new UpdateCheckViewModel(
            _mockVersionService.Object,
            _mockInternetConnectivityService.Object,
            _mockLocalizationService.Object,
            _mockLogService.Object);

        _reviewModeBarViewModel = new ReviewModeBarViewModel(
            _mockConfigReviewModeService.Object,
            _mockConfigReviewDiffService.Object,
            _mockConfigReviewBadgeService.Object,
            _mockConfigurationService.Object,
            _mockDispatcherService.Object,
            _mockLocalizationService.Object,
            _mockDialogService.Object,
            _mockLogService.Object);
    }

    private MainWindowViewModel CreateSut()
    {
        return new MainWindowViewModel(
            _mockThemeService.Object,
            _mockConfigurationService.Object,
            _mockLocalizationService.Object,
            _mockVersionService.Object,
            _mockLogService.Object,
            _mockInteractiveUserService.Object,
            _mockWinGetStartupService.Object,
            _mockWindowsVersionFilterService.Object,
            _taskProgressViewModel,
            _updateCheckViewModel,
            _reviewModeBarViewModel);
    }

    public void Dispose()
    {
        _taskProgressViewModel.Dispose();
        _updateCheckViewModel.Dispose();
        _reviewModeBarViewModel.Dispose();
    }

    // ── Constructor ──

    [Fact]
    public void Constructor_InitializesDefaultProperties()
    {
        var sut = CreateSut();

        sut.AppIconSource.Should().Be("ms-appx:///Assets/AppIcons/winhance-rocket-white-transparent-bg.png");
        sut.VersionInfo.Should().Be("Winhance");
        sut.IsWindowsVersionFilterEnabled.Should().BeTrue();
        sut.OtsInfoBarTitle.Should().BeEmpty();
        sut.OtsInfoBarMessage.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_AssignsChildViewModels()
    {
        var sut = CreateSut();

        sut.TaskProgress.Should().BeSameAs(_taskProgressViewModel);
        sut.UpdateCheck.Should().BeSameAs(_updateCheckViewModel);
        sut.ReviewModeBar.Should().BeSameAs(_reviewModeBarViewModel);
    }

    // ── Initialize ──

    [Fact]
    public void Initialize_SubscribesToThemeChangedEvent()
    {
        var sut = CreateSut();

        sut.Initialize();

        // Raise the theme changed event and verify the icon updates
        _mockThemeService.Setup(t => t.GetEffectiveTheme()).Returns(ElementTheme.Light);
        _mockThemeService.Raise(t => t.ThemeChanged += null, this, WinhanceTheme.LightNative);

        sut.AppIconSource.Should().Be("ms-appx:///Assets/AppIcons/winhance-rocket-black-transparent-bg.png");
    }

    [Fact]
    public void Initialize_SetsVersionInfo()
    {
        _mockVersionService
            .Setup(v => v.GetCurrentVersion())
            .Returns(new VersionInfo { Version = "v25.05.01" });

        var sut = CreateSut();
        sut.Initialize();

        sut.VersionInfo.Should().Be("Winhance v25.05.01");
    }

    [Fact]
    public void Initialize_VersionServiceThrows_FallsBackToDefault()
    {
        _mockVersionService
            .Setup(v => v.GetCurrentVersion())
            .Throws(new Exception("Version unavailable"));

        var sut = CreateSut();
        sut.Initialize();

        sut.VersionInfo.Should().Be("Winhance");
    }

    [Fact]
    public void Initialize_WhenOtsElevation_ShowsInfoBar()
    {
        _mockInteractiveUserService
            .Setup(i => i.IsOtsElevation)
            .Returns(true);
        _mockInteractiveUserService
            .Setup(i => i.InteractiveUserName)
            .Returns("Standard");

        var sut = CreateSut();
        sut.Initialize();

        sut.IsOtsInfoBarOpen.Should().BeTrue();
        sut.OtsInfoBarTitle.Should().NotBeNullOrEmpty();
        sut.OtsInfoBarMessage.Should().Contain("Standard");
    }

    [Fact]
    public void Initialize_WhenNotOtsElevation_DoesNotShowInfoBar()
    {
        var sut = CreateSut();
        sut.Initialize();

        sut.IsOtsInfoBarOpen.Should().BeFalse();
    }

    // ── Theme Handling ──

    [Fact]
    public void UpdateAppIconForTheme_DarkTheme_SetsWhiteIcon()
    {
        _mockThemeService.Setup(t => t.GetEffectiveTheme()).Returns(ElementTheme.Dark);
        var sut = CreateSut();

        sut.UpdateAppIconForTheme();

        sut.AppIconSource.Should().Contain("white");
    }

    [Fact]
    public void UpdateAppIconForTheme_LightTheme_SetsBlackIcon()
    {
        _mockThemeService.Setup(t => t.GetEffectiveTheme()).Returns(ElementTheme.Light);
        var sut = CreateSut();

        sut.UpdateAppIconForTheme();

        sut.AppIconSource.Should().Contain("black");
    }

    // ── OTS InfoBar ──

    [Fact]
    public void DismissOtsInfoBar_SetsIsOtsInfoBarOpenToFalse()
    {
        _mockInteractiveUserService
            .Setup(i => i.IsOtsElevation)
            .Returns(true);
        _mockInteractiveUserService
            .Setup(i => i.InteractiveUserName)
            .Returns("User");

        var sut = CreateSut();
        sut.Initialize();
        sut.IsOtsInfoBarOpen.Should().BeTrue();

        sut.DismissOtsInfoBar();

        sut.IsOtsInfoBarOpen.Should().BeFalse();
    }

    // ── Localized Strings with Fallbacks ──

    [Fact]
    public void AppTitle_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        var sut = CreateSut();
        sut.AppTitle.Should().Be("Winhance");
    }

    [Fact]
    public void AppSubtitle_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        var sut = CreateSut();
        sut.AppSubtitle.Should().Be("by Memory");
    }

    [Fact]
    public void SaveConfigTooltip_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        var sut = CreateSut();
        sut.SaveConfigTooltip.Should().Be("Save Configuration");
    }

    [Fact]
    public void ImportConfigTooltip_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        var sut = CreateSut();
        sut.ImportConfigTooltip.Should().Be("Import Configuration");
    }

    [Fact]
    public void ToggleNavigationTooltip_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        var sut = CreateSut();
        sut.ToggleNavigationTooltip.Should().Be("Toggle Navigation");
    }

    [Fact]
    public void NavSoftwareAppsText_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        var sut = CreateSut();
        sut.NavSoftwareAppsText.Should().Be("Software & Apps");
    }

    [Fact]
    public void NavOptimizeText_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        var sut = CreateSut();
        sut.NavOptimizeText.Should().Be("Optimize");
    }

    [Fact]
    public void NavCustomizeText_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        var sut = CreateSut();
        sut.NavCustomizeText.Should().Be("Customize");
    }

    [Fact]
    public void NavAdvancedToolsText_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        var sut = CreateSut();
        sut.NavAdvancedToolsText.Should().Be("Advanced Tools");
    }

    [Fact]
    public void NavSettingsText_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        var sut = CreateSut();
        sut.NavSettingsText.Should().Be("Settings");
    }

    [Fact]
    public void NavMoreText_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        var sut = CreateSut();
        sut.NavMoreText.Should().Be("More");
    }

    // ── Windows Filter Tooltip ──

    [Fact]
    public void WindowsFilterTooltip_WhenFilterEnabled_ContainsON()
    {
        var sut = CreateSut();
        sut.IsWindowsVersionFilterEnabled = true;

        // The fallback strings contain "ON" or specific text
        sut.WindowsFilterTooltip.Should().Contain("ON");
    }

    [Fact]
    public void WindowsFilterTooltip_WhenFilterDisabled_ContainsOFF()
    {
        var sut = CreateSut();
        sut.IsWindowsVersionFilterEnabled = false;

        sut.WindowsFilterTooltip.Should().Contain("OFF");
    }

    // ── Review Mode / Filter Cross-Cutting ──

    [Fact]
    public void IsWindowsFilterButtonEnabled_ReturnsTrueWhenNotInReviewMode()
    {
        var sut = CreateSut();
        _reviewModeBarViewModel.IsInReviewMode = false;

        sut.IsWindowsFilterButtonEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsWindowsFilterButtonEnabled_ReturnsFalseWhenInReviewMode()
    {
        var sut = CreateSut();
        _reviewModeBarViewModel.IsInReviewMode = true;

        sut.IsWindowsFilterButtonEnabled.Should().BeFalse();
    }

    [Fact]
    public void ReviewModePropertyChange_NotifiesIsWindowsFilterButtonEnabled()
    {
        var sut = CreateSut();
        sut.Initialize();

        var changedProperties = new List<string>();
        sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _reviewModeBarViewModel.IsInReviewMode = true;

        changedProperties.Should().Contain(nameof(sut.IsWindowsFilterButtonEnabled));
    }

    [Fact]
    public void ReviewModeEntered_ForcesFilterOn()
    {
        var sut = CreateSut();
        sut.Initialize();

        _reviewModeBarViewModel.IsInReviewMode = true;

        _mockWindowsVersionFilterService.Verify(f => f.ForceFilterOn(), Times.Once);
    }

    [Fact]
    public void ReviewModeExited_RestoresFilterPreference()
    {
        _mockWindowsVersionFilterService
            .Setup(f => f.RestoreFilterPreferenceAsync())
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        sut.Initialize();

        // Enter review mode first
        _reviewModeBarViewModel.IsInReviewMode = true;
        // Exit review mode
        _reviewModeBarViewModel.IsInReviewMode = false;

        _mockWindowsVersionFilterService.Verify(f => f.RestoreFilterPreferenceAsync(), Times.Once);
    }

    // ── Language Change ──

    [Fact]
    public void LanguageChanged_NotifiesLocalizedStringProperties()
    {
        var sut = CreateSut();
        sut.Initialize();

        var changedProperties = new List<string>();
        sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _mockLocalizationService.Raise(l => l.LanguageChanged += null, this, EventArgs.Empty);

        changedProperties.Should().Contain(nameof(sut.AppTitle));
        changedProperties.Should().Contain(nameof(sut.AppSubtitle));
        changedProperties.Should().Contain(nameof(sut.SaveConfigTooltip));
        changedProperties.Should().Contain(nameof(sut.ImportConfigTooltip));
        changedProperties.Should().Contain(nameof(sut.WindowsFilterTooltip));
        changedProperties.Should().Contain(nameof(sut.ToggleNavigationTooltip));
        changedProperties.Should().Contain(nameof(sut.DonateTooltip));
        changedProperties.Should().Contain(nameof(sut.BugReportTooltip));
        changedProperties.Should().Contain(nameof(sut.DocsTooltip));
    }

    // ── Filter State Changed ──

    [Fact]
    public void FilterStateChanged_UpdatesIsWindowsVersionFilterEnabled()
    {
        var sut = CreateSut();
        sut.Initialize();
        sut.IsWindowsVersionFilterEnabled.Should().BeTrue();

        _mockWindowsVersionFilterService.Raise(f => f.FilterStateChanged += null, this, false);

        sut.IsWindowsVersionFilterEnabled.Should().BeFalse();
    }

    // ── EnsureWinGetReadyOnStartupAsync ──

    [Fact]
    public async Task EnsureWinGetReadyOnStartupAsync_DelegatesToService()
    {
        _mockWinGetStartupService
            .Setup(w => w.EnsureWinGetReadyOnStartupAsync())
            .Returns(Task.CompletedTask);

        var sut = CreateSut();

        await sut.EnsureWinGetReadyOnStartupAsync();

        _mockWinGetStartupService.Verify(w => w.EnsureWinGetReadyOnStartupAsync(), Times.Once);
    }

    // ── LoadFilterPreferenceAsync ──

    [Fact]
    public async Task LoadFilterPreferenceAsync_DelegatesToService()
    {
        _mockWindowsVersionFilterService
            .Setup(f => f.LoadFilterPreferenceAsync())
            .Returns(Task.CompletedTask);

        var sut = CreateSut();

        await sut.LoadFilterPreferenceAsync();

        _mockWindowsVersionFilterService.Verify(f => f.LoadFilterPreferenceAsync(), Times.Once);
    }

    // ── IDisposable ──

    [Fact]
    public void Dispose_UnsubscribesFromEvents()
    {
        var sut = CreateSut();
        sut.Initialize();

        sut.Dispose();

        // After dispose, raising theme changed should not update the icon
        // (we can verify this indirectly by ensuring no exception is thrown
        //  and the icon stays as it was before the event)
        var iconBeforeEvent = sut.AppIconSource;
        _mockThemeService.Setup(t => t.GetEffectiveTheme()).Returns(ElementTheme.Light);
        _mockThemeService.Raise(t => t.ThemeChanged += null, this, WinhanceTheme.LightNative);

        sut.AppIconSource.Should().Be(iconBeforeEvent);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var sut = CreateSut();
        sut.Initialize();

        var act = () =>
        {
            sut.Dispose();
            sut.Dispose();
        };

        act.Should().NotThrow();
    }
}

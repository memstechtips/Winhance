using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Constants;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Settings.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IThemeService> _mockThemeService = new();
    private readonly Mock<IUserPreferencesService> _mockPreferencesService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<IConfigurationService> _mockConfigurationService = new();
    private readonly Mock<ILogService> _mockLogService = new();

    public SettingsViewModelTests()
    {
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        _mockLocalization
            .Setup(l => l.CurrentLanguage)
            .Returns("en");
        _mockThemeService
            .Setup(t => t.CurrentTheme)
            .Returns(WinhanceTheme.System);
    }

    private SettingsViewModel CreateViewModel()
    {
        return new SettingsViewModel(
            _mockLocalization.Object,
            _mockThemeService.Object,
            _mockPreferencesService.Object,
            _mockDialogService.Object,
            _mockConfigurationService.Object,
            _mockLogService.Object);
    }

    // -------------------------------------------------------
    // Constructor / Initialization
    // -------------------------------------------------------

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
        vm.Languages.Count.Should().Be(StringKeys.Languages.SupportedLanguages.Count);
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

    // -------------------------------------------------------
    // SelectedLanguage
    // -------------------------------------------------------

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

    // -------------------------------------------------------
    // SelectedTheme
    // -------------------------------------------------------

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

        // Already System, set to System again -- the setter checks CurrentTheme
        vm.SelectedTheme = WinhanceTheme.System;

        _mockThemeService.Verify(t => t.SetTheme(It.IsAny<WinhanceTheme>()), Times.Never);
    }

    // -------------------------------------------------------
    // SelectedThemeOption
    // -------------------------------------------------------

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

    // -------------------------------------------------------
    // Localized string properties
    // -------------------------------------------------------

    [Fact]
    public void PageTitle_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString(StringKeys.Settings.Title))
            .Returns("Settings Title");

        var vm = CreateViewModel();

        vm.PageTitle.Should().Be("Settings Title");
    }

    [Fact]
    public void PageTitle_WhenNull_ReturnsFallback()
    {
        _mockLocalization
            .Setup(l => l.GetString(StringKeys.Settings.Title))
            .Returns((string?)null!);

        var vm = CreateViewModel();

        vm.PageTitle.Should().Be("Settings");
    }

    [Fact]
    public void PageDescription_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString(StringKeys.Settings.Description))
            .Returns("My Desc");

        var vm = CreateViewModel();

        vm.PageDescription.Should().Be("My Desc");
    }

    [Fact]
    public void ImportButtonText_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString(StringKeys.Buttons.Import))
            .Returns("Import Config");

        var vm = CreateViewModel();

        vm.ImportButtonText.Should().Be("Import Config");
    }

    [Fact]
    public void ExportButtonText_ReturnsLocalizedString()
    {
        _mockLocalization
            .Setup(l => l.GetString(StringKeys.Buttons.Export))
            .Returns("Export Config");

        var vm = CreateViewModel();

        vm.ExportButtonText.Should().Be("Export Config");
    }

    // -------------------------------------------------------
    // Language changed event updates theme display names
    // -------------------------------------------------------

    [Fact]
    public void OnLanguageChanged_UpdatesThemeDisplayNames()
    {
        _mockLocalization
            .Setup(l => l.GetString(StringKeys.Themes.System))
            .Returns("System");

        var vm = CreateViewModel();

        // Now change the localization return for theme names
        _mockLocalization
            .Setup(l => l.GetString(StringKeys.Themes.System))
            .Returns("Systeme");
        _mockLocalization
            .Setup(l => l.GetString(StringKeys.Themes.LightNative))
            .Returns("Clair");
        _mockLocalization
            .Setup(l => l.GetString(StringKeys.Themes.DarkNative))
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
    public void OnLanguageChanged_RaisesPropertyChangedForAllLocalizedProperties()
    {
        var vm = CreateViewModel();
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
        changedProperties.Should().Contain(nameof(vm.ConfigurationLabel));
        changedProperties.Should().Contain(nameof(vm.BackupRestoreHeader));
        changedProperties.Should().Contain(nameof(vm.BackupRestoreDescription));
        changedProperties.Should().Contain(nameof(vm.ImportButtonText));
        changedProperties.Should().Contain(nameof(vm.ExportButtonText));
    }

    // -------------------------------------------------------
    // Import / Export commands
    // -------------------------------------------------------

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
    public async Task ExportConfigCommand_CallsConfigurationService()
    {
        _mockConfigurationService
            .Setup(c => c.ExportConfigurationAsync())
            .Returns(Task.CompletedTask);

        var vm = CreateViewModel();

        await vm.ExportConfigCommand.ExecuteAsync(null);

        _mockConfigurationService.Verify(c => c.ExportConfigurationAsync(), Times.Once);
    }

    // -------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------

    [Fact]
    public void Dispose_UnsubscribesFromLanguageChanged()
    {
        var vm = CreateViewModel();
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
}

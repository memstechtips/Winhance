using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Constants;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;

namespace Winhance.UI.Features.Settings.ViewModels;

/// <summary>
/// ViewModel for the Settings page.
/// </summary>
public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly ILocalizationService _localizationService;
    private readonly IThemeService _themeService;
    private readonly IUserPreferencesService _preferencesService;
    private readonly IDialogService _dialogService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogService _logService;

    private ObservableCollection<ComboBoxOption> _languages = new();
    public ObservableCollection<ComboBoxOption> Languages
    {
        get => _languages;
        set => SetProperty(ref _languages, value);
    }

    private string _selectedLanguage = "en";
    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                OnSelectedLanguageChanged(value);
            }
        }
    }

    private ObservableCollection<ThemeOption> _themes = new();
    public ObservableCollection<ThemeOption> Themes
    {
        get => _themes;
        set => SetProperty(ref _themes, value);
    }

    private WinhanceTheme _selectedTheme;
    public WinhanceTheme SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                OnSelectedThemeChanged(value);
            }
        }
    }

    private ThemeOption? _selectedThemeOption;
    public ThemeOption? SelectedThemeOption
    {
        get => _selectedThemeOption;
        set
        {
            if (SetProperty(ref _selectedThemeOption, value) && value != null)
            {
                SelectedTheme = value.Theme;
            }
        }
    }

    /// <summary>
    /// Creates a new instance of the SettingsViewModel.
    /// </summary>
    public SettingsViewModel(
        ILocalizationService localizationService,
        IThemeService themeService,
        IUserPreferencesService preferencesService,
        IDialogService dialogService,
        IConfigurationService configurationService,
        ILogService logService)
    {
        _localizationService = localizationService;
        _themeService = themeService;
        _preferencesService = preferencesService;
        _dialogService = dialogService;
        _configurationService = configurationService;
        _logService = logService;

        // Initialize languages from StringKeys
        InitializeLanguages();

        // Initialize themes
        InitializeThemes();

        // Load current selections
        _selectedLanguage = _localizationService.CurrentLanguage ?? "en";
        _selectedTheme = _themeService.CurrentTheme;
        _selectedThemeOption = Themes.FirstOrDefault(t => t.Theme == _selectedTheme);

        // Subscribe to language changes to update theme display names
        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _localizationService.LanguageChanged -= OnLanguageChanged;
    }

    /// <summary>
    /// Initializes the language options from StringKeys.
    /// </summary>
    private void InitializeLanguages()
    {
        Languages.Clear();
        foreach (var lang in StringKeys.Languages.SupportedLanguages)
        {
            Languages.Add(new ComboBoxOption(lang.Value, lang.Key));
        }
    }

    /// <summary>
    /// Initializes the theme options.
    /// </summary>
    private void InitializeThemes()
    {
        Themes.Clear();
        Themes.Add(new ThemeOption(WinhanceTheme.System, GetThemeDisplayName(WinhanceTheme.System)));
        Themes.Add(new ThemeOption(WinhanceTheme.LightNative, GetThemeDisplayName(WinhanceTheme.LightNative)));
        Themes.Add(new ThemeOption(WinhanceTheme.DarkNative, GetThemeDisplayName(WinhanceTheme.DarkNative)));
    }

    /// <summary>
    /// Gets the localized display name for a theme.
    /// </summary>
    private string GetThemeDisplayName(WinhanceTheme theme) => theme switch
    {
        WinhanceTheme.System => _localizationService.GetString(StringKeys.Themes.System) ?? "System",
        WinhanceTheme.LightNative => _localizationService.GetString(StringKeys.Themes.LightNative) ?? "Light",
        WinhanceTheme.DarkNative => _localizationService.GetString(StringKeys.Themes.DarkNative) ?? "Dark",
        _ => theme.ToString()
    };

    /// <summary>
    /// Called when the language changes to update all localized strings.
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Update theme display names
        foreach (var theme in Themes)
        {
            theme.DisplayText = GetThemeDisplayName(theme.Theme);
        }

        // Notify UI to refresh all localized strings
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageDescription));
        OnPropertyChanged(nameof(GeneralLabel));
        OnPropertyChanged(nameof(LanguageHeader));
        OnPropertyChanged(nameof(LanguageDescription));
        OnPropertyChanged(nameof(ThemeHeader));
        OnPropertyChanged(nameof(ThemeDescription));
        OnPropertyChanged(nameof(ConfigurationLabel));
        OnPropertyChanged(nameof(BackupRestoreHeader));
        OnPropertyChanged(nameof(BackupRestoreDescription));
        OnPropertyChanged(nameof(ImportButtonText));
        OnPropertyChanged(nameof(ExportButtonText));
    }

    // Localized string properties for x:Bind
    public string PageTitle => _localizationService.GetString(StringKeys.Settings.Title) ?? "Settings";
    public string PageDescription => _localizationService.GetString(StringKeys.Settings.Description) ?? "Configure Winhance Application Preferences";
    public string GeneralLabel => _localizationService.GetString(StringKeys.Categories.General) ?? "General";
    public string LanguageHeader => _localizationService.GetString(StringKeys.Settings.Language) ?? "Language";
    public string LanguageDescription => _localizationService.GetString(StringKeys.Settings.LanguageDescription) ?? "Select your preferred language";
    public string ThemeHeader => _localizationService.GetString(StringKeys.Settings.ThemeTitle) ?? "Theme";
    public string ThemeDescription => _localizationService.GetString(StringKeys.Settings.ThemeDescription) ?? "Choose your preferred theme";
    public string ConfigurationLabel => _localizationService.GetString(StringKeys.Categories.Configuration) ?? "Configuration";
    public string BackupRestoreHeader => _localizationService.GetString(StringKeys.Settings.BackupRestoreTitle) ?? "Backup & Restore";
    public string BackupRestoreDescription => _localizationService.GetString(StringKeys.Settings.BackupRestoreDescription) ?? "Import or export your settings configuration";
    public string ImportButtonText => _localizationService.GetString(StringKeys.Buttons.Import) ?? "Import";
    public string ExportButtonText => _localizationService.GetString(StringKeys.Buttons.Export) ?? "Export";

    /// <summary>
    /// Called when the selected theme changes.
    /// </summary>
    private void OnSelectedThemeChanged(WinhanceTheme value)
    {
        if (_themeService.CurrentTheme != value)
        {
            _themeService.SetTheme(value);
        }
    }

    /// <summary>
    /// Called when the selected language changes.
    /// </summary>
    private void OnSelectedLanguageChanged(string value)
    {
        if (string.IsNullOrEmpty(value) || value == _localizationService.CurrentLanguage)
            return;

        if (_localizationService.SetLanguage(value))
        {
            _preferencesService.SetPreferenceAsync("Language", value).FireAndForget(_logService);
        }
    }

    /// <summary>
    /// Command to import configuration.
    /// </summary>
    [RelayCommand]
    private async Task ImportConfigAsync()
    {
        await _configurationService.ImportConfigurationAsync();
    }

    /// <summary>
    /// Command to export configuration.
    /// </summary>
    [RelayCommand]
    private async Task ExportConfigAsync()
    {
        await _configurationService.ExportConfigurationAsync();
    }
}

/// <summary>
/// Represents a theme option for the ComboBox.
/// </summary>
public partial class ThemeOption : ObservableObject
{
    private string _displayText = string.Empty;
    public string DisplayText
    {
        get => _displayText;
        set => SetProperty(ref _displayText, value);
    }

    public WinhanceTheme Theme { get; }

    public ThemeOption(WinhanceTheme theme, string displayText)
    {
        Theme = theme;
        _displayText = displayText;
    }
}

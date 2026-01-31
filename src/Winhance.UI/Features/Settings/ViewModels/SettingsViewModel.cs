using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Constants;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Settings.ViewModels;

/// <summary>
/// ViewModel for the Settings page.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;
    private readonly IThemeService _themeService;
    private readonly IUserPreferencesService _preferencesService;
    private readonly IDialogService _dialogService;

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
        IDialogService dialogService)
    {
        _localizationService = localizationService;
        _themeService = themeService;
        _preferencesService = preferencesService;
        _dialogService = dialogService;

        // Initialize languages from StringKeys
        InitializeLanguages();

        // Initialize themes (5-theme system)
        InitializeThemes();

        // Load current selections
        _selectedLanguage = _localizationService.CurrentLanguage ?? "en";
        _selectedTheme = _themeService.CurrentTheme;
        _selectedThemeOption = Themes.FirstOrDefault(t => t.Theme == _selectedTheme);

        // Subscribe to language changes to update theme display names
        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>
    /// Initializes the language options from StringKeys.
    /// </summary>
    private void InitializeLanguages()
    {
        Languages.Clear();
        foreach (var lang in StringKeys.Languages.SupportedLanguages)
        {
            Languages.Add(new ComboBoxOption { DisplayText = lang.Value, Value = lang.Key });
        }
    }

    /// <summary>
    /// Initializes the 5-theme options.
    /// </summary>
    private void InitializeThemes()
    {
        Themes.Clear();
        Themes.Add(new ThemeOption(WinhanceTheme.System, GetThemeDisplayName(WinhanceTheme.System)));
        Themes.Add(new ThemeOption(WinhanceTheme.LightNative, GetThemeDisplayName(WinhanceTheme.LightNative)));
        Themes.Add(new ThemeOption(WinhanceTheme.DarkNative, GetThemeDisplayName(WinhanceTheme.DarkNative)));
        Themes.Add(new ThemeOption(WinhanceTheme.LegacyWhite, GetThemeDisplayName(WinhanceTheme.LegacyWhite)));
        Themes.Add(new ThemeOption(WinhanceTheme.LegacyDark, GetThemeDisplayName(WinhanceTheme.LegacyDark)));
    }

    /// <summary>
    /// Gets the localized display name for a theme.
    /// </summary>
    private string GetThemeDisplayName(WinhanceTheme theme) => theme switch
    {
        WinhanceTheme.System => _localizationService.GetString(StringKeys.Themes.System) ?? "System",
        WinhanceTheme.LightNative => _localizationService.GetString(StringKeys.Themes.LightNative) ?? "Light",
        WinhanceTheme.DarkNative => _localizationService.GetString(StringKeys.Themes.DarkNative) ?? "Dark",
        WinhanceTheme.LegacyWhite => _localizationService.GetString(StringKeys.Themes.LegacyWhite) ?? "Legacy Light",
        WinhanceTheme.LegacyDark => _localizationService.GetString(StringKeys.Themes.LegacyDark) ?? "Legacy Dark",
        _ => theme.ToString()
    };

    /// <summary>
    /// Called when the language changes to update theme display names.
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Update theme display names
        foreach (var theme in Themes)
        {
            theme.DisplayText = GetThemeDisplayName(theme.Theme);
        }
    }

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
            _ = _preferencesService.SetPreferenceAsync("Language", value);
        }
    }

    /// <summary>
    /// Command to import configuration.
    /// </summary>
    [RelayCommand]
    private async Task ImportConfigAsync()
    {
        // TODO: Implement configuration import for WinUI 3
        await _dialogService.ShowInformationAsync("Configuration import not yet implemented.", "Import");
    }

    /// <summary>
    /// Command to export configuration.
    /// </summary>
    [RelayCommand]
    private async Task ExportConfigAsync()
    {
        // TODO: Implement configuration export for WinUI 3
        await _dialogService.ShowInformationAsync("Configuration export not yet implemented.", "Export");
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

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Settings.ViewModels;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly ILocalizationService _localizationService;
    private readonly IThemeService _themeService;
    private readonly IUserPreferencesService _preferencesService;
    private readonly IDialogService _dialogService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogService _logService;
    private readonly ISystemBackupService _backupService;
    private readonly ITaskProgressService _taskProgressService;

    [ObservableProperty]
    public partial bool IsCreatingRestorePoint { get; set; }

    private ObservableCollection<ComboBoxDisplayOption> _languages = new();
    public ObservableCollection<ComboBoxDisplayOption> Languages
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

    public SettingsViewModel(
        ILocalizationService localizationService,
        IThemeService themeService,
        IUserPreferencesService preferencesService,
        IDialogService dialogService,
        IConfigurationService configurationService,
        ILogService logService,
        ISystemBackupService backupService,
        ITaskProgressService taskProgressService)
    {
        _localizationService = localizationService;
        _themeService = themeService;
        _preferencesService = preferencesService;
        _dialogService = dialogService;
        _configurationService = configurationService;
        _logService = logService;
        _backupService = backupService;
        _taskProgressService = taskProgressService;

        // Initialize languages from the localization service
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
        GC.SuppressFinalize(this);
    }

    private void InitializeLanguages()
    {
        Languages.Clear();
        foreach (var opt in _localizationService.GetAvailableLanguages())
        {
            Languages.Add(new ComboBoxDisplayOption(opt.DisplayName, opt.Code));
        }
    }

    private void InitializeThemes()
    {
        Themes.Clear();
        Themes.Add(new ThemeOption(WinhanceTheme.System, GetThemeDisplayName(WinhanceTheme.System)));
        Themes.Add(new ThemeOption(WinhanceTheme.LightNative, GetThemeDisplayName(WinhanceTheme.LightNative)));
        Themes.Add(new ThemeOption(WinhanceTheme.DarkNative, GetThemeDisplayName(WinhanceTheme.DarkNative)));
    }

    private string GetThemeDisplayName(WinhanceTheme theme) => theme switch
    {
        WinhanceTheme.System => _localizationService.GetStringOrDefault("Theme_System", "System"),
        WinhanceTheme.LightNative => _localizationService.GetStringOrDefault("Theme_LightNative", "Light"),
        WinhanceTheme.DarkNative => _localizationService.GetStringOrDefault("Theme_DarkNative", "Dark"),
        _ => theme.ToString()
    };

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
        OnPropertyChanged(nameof(SystemProtectionLabel));
        OnPropertyChanged(nameof(SystemProtectionHeader));
        OnPropertyChanged(nameof(SystemProtectionDescription));
        OnPropertyChanged(nameof(CreateRestorePointButtonText));
    }

    // Localized string properties for x:Bind
    public string PageTitle => _localizationService.GetStringOrDefault("Settings_Title", "Settings");
    public string PageDescription => _localizationService.GetStringOrDefault("Settings_Description", "Configure Winhance Application Preferences");
    public string GeneralLabel => _localizationService.GetStringOrDefault("Category_General", "General");
    public string LanguageHeader => _localizationService.GetStringOrDefault("Settings_Menu_Language", "Language");
    public string LanguageDescription => _localizationService.GetStringOrDefault("Settings_Language_Description", "Select your preferred language");
    public string ThemeHeader => _localizationService.GetStringOrDefault("Settings_Theme_Title", "Theme");
    public string ThemeDescription => _localizationService.GetStringOrDefault("Tooltip_ToggleTheme", "Choose your preferred theme");
    public string ConfigurationLabel => _localizationService.GetStringOrDefault("Category_Configuration", "Configuration");
    public string BackupRestoreHeader => _localizationService.GetStringOrDefault("Settings_BackupRestore_Title", "Backup & Restore");
    public string BackupRestoreDescription => _localizationService.GetStringOrDefault("Settings_BackupRestore_Description", "Import or export your settings configuration");
    public string ImportButtonText => _localizationService.GetStringOrDefault("Button_Import", "Import");
    public string ExportButtonText => _localizationService.GetStringOrDefault("Button_Export", "Export");
    public string SystemProtectionLabel => _localizationService.GetStringOrDefault("Category_SystemProtection", "System Protection");
    public string SystemProtectionHeader => _localizationService.GetStringOrDefault("Settings_SystemProtection_Title", "System Restore Point");
    public string SystemProtectionDescription => _localizationService.GetStringOrDefault("Settings_SystemProtection_Description", "Create a Windows System Restore point to allow rolling back system changes");
    public string CreateRestorePointButtonText => _localizationService.GetStringOrDefault("Settings_CreateRestorePoint_Button", "Create Restore Point");

    private void OnSelectedThemeChanged(WinhanceTheme value)
    {
        if (_themeService.CurrentTheme != value)
        {
            _themeService.SetTheme(value);
        }
    }

    private void OnSelectedLanguageChanged(string value)
    {
        if (string.IsNullOrEmpty(value) || value == _localizationService.CurrentLanguage)
            return;

        if (_localizationService.SetLanguage(value))
        {
            _preferencesService.SetPreferenceAsync("Language", value).FireAndForget(_logService);
        }
    }

    [RelayCommand]
    private async Task ImportConfigAsync()
    {
        await _configurationService.ImportConfigurationAsync();
    }

    [RelayCommand]
    private async Task ExportConfigAsync()
    {
        await _configurationService.ExportConfigurationAsync();
    }

    [RelayCommand]
    private async Task CreateRestorePointAsync()
    {
        IsCreatingRestorePoint = true;
        var cts = _taskProgressService.StartTask(
            _localizationService.GetStringOrDefault("Progress_CreatingRestorePoint", "Creating system restore point..."),
            isIndeterminate: true);
        var progress = _taskProgressService.CreateDetailedProgress();

        try
        {
            var result = await _backupService.CreateRestorePointAsync(
                progress: progress, cancellationToken: cts.Token);

            _taskProgressService.CompleteTask();

            if (result.Success && result.RestorePointCreated)
            {
                var successMsg = _localizationService.GetStringOrDefault("Settings_RestorePoint_Success", "System Restore point created successfully.");
                await _dialogService.ShowInformationAsync(successMsg);
            }
            else
            {
                var failMsg = _localizationService.GetStringOrDefault("Settings_RestorePoint_Fail", "Failed to create System Restore point.");
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                    failMsg += $"\n\n{result.ErrorMessage}";
                await _dialogService.ShowWarningAsync(failMsg);
            }
        }
        catch (Exception ex)
        {
            _taskProgressService.CompleteTask();
            _logService.LogWarning($"Failed to create restore point from Settings: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                _localizationService.GetStringOrDefault("Settings_RestorePoint_Fail", "Failed to create System Restore point."));
        }
        finally
        {
            IsCreatingRestorePoint = false;
        }
    }
}

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

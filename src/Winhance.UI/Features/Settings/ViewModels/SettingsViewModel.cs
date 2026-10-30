using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;

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
    private readonly IScheduledTaskService _scheduledTaskService;
    private readonly IInteractiveUserService _interactiveUserService;

    internal const string SignInTaskName = "Start Winhance in the tray";

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

    private bool _closeToTray;
    public bool CloseToTray
    {
        get => _closeToTray;
        set
        {
            if (SetProperty(ref _closeToTray, value))
                _preferencesService.SetPreferenceAsync(UserPreferenceKeys.CloseToTray, value).FireAndForget(_logService);
        }
    }

    private bool _minimizeToTray;
    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set
        {
            if (SetProperty(ref _minimizeToTray, value))
                _preferencesService.SetPreferenceAsync(UserPreferenceKeys.MinimizeToTray, value).FireAndForget(_logService);
        }
    }

    private bool _startInTrayAtSignIn;
    private bool _startInTrayChosen;
    public bool StartInTrayAtSignIn
    {
        get => _startInTrayAtSignIn;
        set
        {
            if (SetProperty(ref _startInTrayAtSignIn, value))
            {
                OnPropertyChanged(nameof(CanStartInTrayAtSignIn));
                _startInTrayChosen = true;
                ApplyStartInTrayAsync(value).FireAndForget(_logService);
            }
        }
    }

    // The sign-in task runs as the account that registered it. Under another account's administrator rights that is
    // the administrator, and Windows starts nothing elevated for the person who signs in without a credential prompt.
    // So there the switch only turns off a task this user already has.
    public bool CanStartInTrayAtSignIn => !_interactiveUserService.IsOtsElevation || _startInTrayAtSignIn;

    public ObservableCollection<ComboBoxDisplayOption> UpdateCheckOptions { get; } = new();

    public ObservableCollection<ComboBoxDisplayOption> UpdateActionOptions { get; } = new();

    // The option objects themselves, like the theme picker: a .NET enum is not a WinRT type, so the ComboBox cannot
    // match a boxed enum SelectedValue against its items and shows nothing selected.
    private ComboBoxDisplayOption? _selectedUpdateCheckOption;
    public ComboBoxDisplayOption? SelectedUpdateCheckOption
    {
        get => _selectedUpdateCheckOption;
        set
        {
            if (value?.Value is AppUpdateCheckFrequency frequency && SetProperty(ref _selectedUpdateCheckOption, value))
                _preferencesService.SetPreferenceAsync(UserPreferenceKeys.AppUpdateCheckFrequency, frequency).FireAndForget(_logService);
        }
    }

    private ComboBoxDisplayOption? _selectedUpdateActionOption;
    public ComboBoxDisplayOption? SelectedUpdateActionOption
    {
        get => _selectedUpdateActionOption;
        set
        {
            if (value?.Value is AppUpdateAction action && SetProperty(ref _selectedUpdateActionOption, value))
                _preferencesService.SetPreferenceAsync(UserPreferenceKeys.AppUpdateAction, action).FireAndForget(_logService);
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
        ITaskProgressService taskProgressService,
        IScheduledTaskService scheduledTaskService,
        IInteractiveUserService interactiveUserService)
    {
        _localizationService = localizationService;
        _themeService = themeService;
        _preferencesService = preferencesService;
        _dialogService = dialogService;
        _configurationService = configurationService;
        _logService = logService;
        _backupService = backupService;
        _taskProgressService = taskProgressService;
        _scheduledTaskService = scheduledTaskService;
        _interactiveUserService = interactiveUserService;

        InitializeLanguages();

        InitializeThemes();

        _selectedLanguage = _localizationService.CurrentLanguage ?? "en";
        _selectedTheme = _themeService.CurrentTheme;
        _selectedThemeOption = Themes.FirstOrDefault(t => t.Theme == _selectedTheme);
        _closeToTray = _preferencesService.GetPreference(UserPreferenceKeys.CloseToTray, false);
        _minimizeToTray = _preferencesService.GetPreference(UserPreferenceKeys.MinimizeToTray, false);
        InitializeUpdateOptions();
        var frequency = _preferencesService.GetPreference(UserPreferenceKeys.AppUpdateCheckFrequency, AppUpdateCheckFrequency.Daily);
        var action = _preferencesService.GetPreference(UserPreferenceKeys.AppUpdateAction, AppUpdateAction.Notify);
        _selectedUpdateCheckOption = UpdateCheckOptions.FirstOrDefault(o => Equals(o.Value, frequency));
        _selectedUpdateActionOption = UpdateActionOptions.FirstOrDefault(o => Equals(o.Value, action));

        _localizationService.LanguageChanged += OnLanguageChanged;
        ShowSignInTaskStateAsync().FireAndForget(_logService);
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

    private void InitializeUpdateOptions()
    {
        foreach (var frequency in Enum.GetValues<AppUpdateCheckFrequency>())
            UpdateCheckOptions.Add(new ComboBoxDisplayOption(UpdateCheckText(frequency), frequency));
        foreach (var action in Enum.GetValues<AppUpdateAction>())
            UpdateActionOptions.Add(new ComboBoxDisplayOption(UpdateActionText(action), action));
    }

    private string UpdateCheckText(AppUpdateCheckFrequency frequency)
    {
        var key = "Settings_UpdateCheck_" + frequency;
        return _localizationService.GetString(key);
    }

    private string UpdateActionText(AppUpdateAction action)
    {
        var key = "Settings_UpdateAction_" + action;
        return _localizationService.GetString(key);
    }

    // The task itself is the state, so the switch is right after the task is deleted outside Winhance. Assigns the
    // field: the setter would register or remove the task. A switch the user already flipped wins over a late answer.
    private async Task ShowSignInTaskStateAsync()
    {
        var registered = await IsSignInTaskThisUsersAsync();
        if (_startInTrayChosen || registered == _startInTrayAtSignIn)
            return;

        ShowStartInTray(registered);
    }

    private void ShowStartInTray(bool on)
    {
        _startInTrayAtSignIn = on;
        OnPropertyChanged(nameof(StartInTrayAtSignIn));
        OnPropertyChanged(nameof(CanStartInTrayAtSignIn));
    }

    // The task name is one per PC, and the task runs for whoever registered it last.
    private Task<bool> IsSignInTaskThisUsersAsync() =>
        _scheduledTaskService.IsSignInTaskRegisteredForAsync(SignInTaskName, _interactiveUserService.InteractiveUserSid);

    // The task starts Winhance elevated without a UAC prompt; a failed registration puts the switch back.
    private async Task ApplyStartInTrayAsync(bool enabled)
    {
        var result = enabled && Environment.ProcessPath is { } exePath
            ? await _scheduledTaskService.RegisterSignInTaskAsync(SignInTaskName, exePath, TrayController.StartInTrayFlag)
            : enabled ? OperationResult.Failed("Winhance's own path is unknown")
            : !await IsSignInTaskThisUsersAsync() ? OperationResult.Succeeded()
            : await _scheduledTaskService.UnregisterScheduledTaskAsync(SignInTaskName);

        if (result.Success)
            return;

        _logService.LogWarning($"Could not change the sign-in task: {result.ErrorMessage}");
        ShowStartInTray(!enabled);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var theme in Themes)
        {
            theme.DisplayText = GetThemeDisplayName(theme.Theme);
        }

        foreach (var option in UpdateCheckOptions)
            option.DisplayText = UpdateCheckText((AppUpdateCheckFrequency)option.Value);
        foreach (var option in UpdateActionOptions)
            option.DisplayText = UpdateActionText((AppUpdateAction)option.Value);
        OnPropertyChanged(nameof(BackgroundLabel));
        OnPropertyChanged(nameof(CloseToTrayHeader));
        OnPropertyChanged(nameof(CloseToTrayDescription));
        OnPropertyChanged(nameof(MinimizeToTrayHeader));
        OnPropertyChanged(nameof(MinimizeToTrayDescription));
        OnPropertyChanged(nameof(StartInTrayHeader));
        OnPropertyChanged(nameof(StartInTrayDescription));
        OnPropertyChanged(nameof(UpdateCheckHeader));
        OnPropertyChanged(nameof(UpdateCheckDescription));
        OnPropertyChanged(nameof(UpdateActionHeader));
        OnPropertyChanged(nameof(UpdateActionDescription));

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

    public string PageTitle => _localizationService.GetStringOrDefault("Settings_Title", "Winhance Settings");
    public string PageDescription => _localizationService.GetStringOrDefault("Settings_Description", "Configure Winhance Application Preferences");
    public string GeneralLabel => _localizationService.GetStringOrDefault("Category_General", "General");
    public string LanguageHeader => _localizationService.GetStringOrDefault("Settings_Menu_Language", "Language");
    public string LanguageDescription => _localizationService.GetStringOrDefault("Settings_Language_Description", "Select the application display language");
    public string ThemeHeader => _localizationService.GetStringOrDefault("Settings_Theme_Title", "Winhance Theme");
    public string ThemeDescription => _localizationService.GetString("Settings_Theme_Description");
    public string BackgroundLabel => _localizationService.GetString("Settings_Background_Label");
    public string CloseToTrayHeader => _localizationService.GetString("Settings_CloseToTray_Title");
    public string CloseToTrayDescription => _localizationService.GetString("Settings_CloseToTray_Description");
    public string MinimizeToTrayHeader => _localizationService.GetString("Settings_MinimizeToTray_Title");
    public string MinimizeToTrayDescription => _localizationService.GetString("Settings_MinimizeToTray_Description");
    public string StartInTrayHeader => _localizationService.GetString("Settings_StartInTray_Title");
    public string StartInTrayDescription => _localizationService.GetString(
        _interactiveUserService.IsOtsElevation ? "Settings_StartInTray_OtsUnavailable" : "Settings_StartInTray_Description");
    public string UpdateCheckHeader => _localizationService.GetString("Settings_UpdateCheck_Title");
    public string UpdateCheckDescription => _localizationService.GetString("Settings_UpdateCheck_Description");
    public string UpdateActionHeader => _localizationService.GetString("Settings_UpdateAction_Title");
    public string UpdateActionDescription => _localizationService.GetString("Settings_UpdateAction_Description");
    public string ConfigurationLabel => _localizationService.GetStringOrDefault("Category_Configuration", "Configuration");
    public string BackupRestoreHeader => _localizationService.GetStringOrDefault("Settings_BackupRestore_Title", "Backup & Restore");
    public string BackupRestoreDescription => _localizationService.GetString("Settings_BackupRestore_Snapshot_Description");
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

            if (result.Success && result.RestorePointCreated)
            {
                _taskProgressService.CompleteTask();
                var successMsg = _localizationService.GetStringOrDefault("Settings_RestorePoint_Success", "System Restore point created successfully.");
                await _dialogService.ShowInformationAsync(successMsg);
            }
            else
            {
                _taskProgressService.FailTask();
                var failMsg = _localizationService.GetStringOrDefault("Settings_RestorePoint_Fail", "Failed to create System Restore point.");
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                    failMsg += $"\n\n{result.ErrorMessage}";
                await _dialogService.ShowWarningAsync(failMsg);
            }
        }
        catch (Exception ex)
        {
            _taskProgressService.FailTask();
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

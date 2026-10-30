using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
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
    private readonly IProfileService _profiles;
    private readonly IBuilderModeEntry _builderEntry;
    private readonly ICoverageSummaryService _coverageSummary;
    private readonly IProcessExecutor _processExecutor;
    private readonly IFilePickerService _filePicker;
    private readonly ISaveFilePicker _saveFilePicker;

    internal const string SignInTaskName = "Start Winhance in the tray";
    internal const string ReapplyTaskName = "Re-apply the Winhance profile";

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

    public ObservableCollection<ProfileRow> Profiles { get; } = new();

    public bool IsProfileBusy => _profiles.IsBusy;

    public int ProfileNameMaxLength => ProfileService.MaxNameLength;

    private string _newProfileName = string.Empty;
    public string NewProfileName
    {
        get => _newProfileName;
        set
        {
            if (SetProperty(ref _newProfileName, value))
                SaveProfileCommand.NotifyCanExecuteChanged();
        }
    }

    private bool _reapplyAtSignIn;
    private bool _reapplyChosen;
    public bool ReapplyAtSignIn
    {
        get => _reapplyAtSignIn;
        set
        {
            if (SetProperty(ref _reapplyAtSignIn, value))
            {
                OnPropertyChanged(nameof(CanReapplyAtSignIn));
                _reapplyChosen = true;
                ApplySignInTaskAsync(ReapplyTaskName, TrayController.ReapplyProfileFlag, value, ShowReapply).FireAndForget(_logService);
            }
        }
    }

    // Same account rule as the tray task.
    public bool CanReapplyAtSignIn => !_interactiveUserService.IsOtsElevation || _reapplyAtSignIn;

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
        IInteractiveUserService interactiveUserService,
        IProfileService profiles,
        IProcessExecutor processExecutor,
        IFilePickerService filePicker,
        ISaveFilePicker saveFilePicker,
        IBuilderModeEntry builderEntry,
        ICoverageSummaryService coverageSummary)
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
        _profiles = profiles;
        _processExecutor = processExecutor;
        _filePicker = filePicker;
        _saveFilePicker = saveFilePicker;
        _builderEntry = builderEntry;
        _coverageSummary = coverageSummary;

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
        _profiles.Changed += OnProfilesChanged;
        ShowSignInTaskStateAsync().FireAndForget(_logService);
        RefreshProfilesAsync().FireAndForget(_logService);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _localizationService.LanguageChanged -= OnLanguageChanged;
        _profiles.Changed -= OnProfilesChanged;
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
        var registered = await IsSignInTaskThisUsersAsync(SignInTaskName);
        if (!_startInTrayChosen && registered != _startInTrayAtSignIn)
            ShowStartInTray(registered);

        var reapply = await IsSignInTaskThisUsersAsync(ReapplyTaskName);
        if (!_reapplyChosen && reapply != _reapplyAtSignIn)
            ShowReapply(reapply);
    }

    private void ShowStartInTray(bool on)
    {
        _startInTrayAtSignIn = on;
        OnPropertyChanged(nameof(StartInTrayAtSignIn));
        OnPropertyChanged(nameof(CanStartInTrayAtSignIn));
    }

    private void ShowReapply(bool on)
    {
        _reapplyAtSignIn = on;
        OnPropertyChanged(nameof(ReapplyAtSignIn));
        OnPropertyChanged(nameof(CanReapplyAtSignIn));
    }

    // The task name is one per PC, and the task runs for whoever registered it last.
    private Task<bool> IsSignInTaskThisUsersAsync(string taskName) =>
        _scheduledTaskService.IsSignInTaskRegisteredForAsync(taskName, _interactiveUserService.InteractiveUserSid);

    private Task ApplyStartInTrayAsync(bool enabled) =>
        ApplySignInTaskAsync(SignInTaskName, TrayController.StartInTrayFlag, enabled, ShowStartInTray);

    // The task starts Winhance elevated without a UAC prompt; a failed registration puts the switch back.
    private async Task ApplySignInTaskAsync(string taskName, string flag, bool enabled, Action<bool> showSwitch)
    {
        var result = enabled && Environment.ProcessPath is { } exePath
            ? await _scheduledTaskService.RegisterSignInTaskAsync(taskName, exePath, flag)
            : enabled ? OperationResult.Failed("Winhance's own path is unknown")
            : !await IsSignInTaskThisUsersAsync(taskName) ? OperationResult.Succeeded()
            : await _scheduledTaskService.UnregisterScheduledTaskAsync(taskName);

        if (result.Success)
            return;

        _logService.LogWarning($"Could not change the sign-in task '{taskName}': {result.ErrorMessage}");
        showSwitch(!enabled);
    }

    private void OnProfilesChanged(object? sender, EventArgs e) => RefreshProfilesAsync().FireAndForget(_logService);

    // Drift is read only for the active profile, and not mid-switch, when every setting is on its way to changing.
    private async Task RefreshProfilesAsync()
    {
        OnPropertyChanged(nameof(IsProfileBusy));
        var activeId = _profiles.ActiveProfileId;
        IReadOnlyList<string> drift = activeId is not null && !_profiles.IsBusy && !_profiles.LastSwitchFailed
            ? await _profiles.DriftAsync()
            : [];

        Profiles.Clear();
        foreach (var profile in _profiles.GetProfiles())
        {
            Profiles.Add(new ProfileRow(
                profile.Name,
                profile.Id == activeId ? ProfileStatus(drift.Count) : string.Empty,
                profile.Kind == ProfileKind.User,
                _localizationService.GetString("Button_Apply"),
                _localizationService.GetString("Profiles_EditInBuilder"),
                _localizationService.GetString("Button_Export"),
                _localizationService.GetString("Button_Delete"),
                new AsyncRelayCommand(() => SwitchProfileAsync(profile), () => !_profiles.IsBusy),
                new AsyncRelayCommand(() => _builderEntry.EnterFromProfileAsync(profile)),
                new AsyncRelayCommand(() => ExportProfileAsync(profile)),
                new AsyncRelayCommand(() => DeleteProfileAsync(profile))));
        }
    }

    private string ProfileStatus(int drifted) =>
        _profiles.LastSwitchFailed ? _localizationService.GetString("Profiles_Status_Failed")
        : _profiles.PendingReboot.Count > 0 ? _localizationService.GetString("Profiles_Status_RestartNeeded")
        : drifted == 1 ? _localizationService.GetString("Profiles_Status_ChangedOne")
        : drifted > 0 ? _localizationService.GetString("Profiles_Status_Changed", drifted)
        : _localizationService.GetString("Profiles_Status_Active");

    private async Task SwitchProfileAsync(SettingsProfile profile)
    {
        var result = await _profiles.SwitchAsync(profile);
        if (result is null)
            return;

        if (!result.Success)
        {
            var message = result.NotApplied.Count == 1
                ? _localizationService.GetString("Profiles_Failed_MessageOne", profile.Name)
                : _localizationService.GetString("Profiles_Failed_Message", profile.Name, result.NotApplied.Count);
            await _dialogService.ShowWarningAsync(message + "\n\n" + string.Join("\n", result.NotApplied), _localizationService.GetString("Profiles_Failed_Title"));
        }

        var edit = await _profiles.LoadAsync(profile) is { } config && await _coverageSummary.ShowProfileReminderAsync(profile, config);

        if (result.Success && result.NeedsRestart.Count > 0)
        {
            var response = await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
            {
                Title = _localizationService.GetString("Profiles_RestartNeeded_Title"),
                Message = _localizationService.GetString("Profiles_RestartNeeded_Dialog"),
                Items = result.NeedsRestart,
                ConfirmButtonText = _localizationService.GetString("Profiles_RestartNow"),
                CancelButtonText = _localizationService.GetString("Profiles_RestartLater"),
            });
            if (response.Confirmed)
            {
                await _processExecutor.ShellExecuteAsync("shutdown.exe", "/r /t 0");
                return;
            }
        }

        if (edit)
            await _builderEntry.EnterFromProfileAsync(profile);
    }

    private async Task ExportProfileAsync(SettingsProfile profile)
    {
        var path = _saveFilePicker.PickSavePath(
            _localizationService.GetString("Config_FileDialog_SaveConfig"),
            ConfigFileConstants.FileFilter,
            ConfigFileConstants.FilePattern,
            profile.Name + ConfigFileConstants.FileExtension,
            "winhance");
        if (path is not null && !await _profiles.ExportAsync(profile, path))
            await _dialogService.ShowErrorAsync(_localizationService.GetString("Profiles_Export_Failed"));
    }

    private async Task DeleteProfileAsync(SettingsProfile profile)
    {
        var response = await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
        {
            Title = _localizationService.GetString("Profiles_Delete_Title"),
            Message = _localizationService.GetString("Profiles_Delete_Message", profile.Name),
            ConfirmButtonText = _localizationService.GetString("Button_Delete"),
        });
        if (response.Confirmed)
            await _profiles.DeleteAsync(profile);
    }

    private bool CanSaveProfile() => _profiles.IsNameAvailable(NewProfileName);

    [RelayCommand(CanExecute = nameof(CanSaveProfile))]
    private async Task SaveProfileAsync()
    {
        if (await _profiles.SaveCurrentAsync(NewProfileName) is null)
        {
            await _dialogService.ShowErrorAsync(_localizationService.GetString("Profiles_Save_Failed"));
            return;
        }

        NewProfileName = string.Empty;
    }

    // The file's own name becomes the profile's, numbered when a profile already has it.
    [RelayCommand]
    private async Task AddProfileFromFileAsync()
    {
        if (_filePicker.PickFile([ConfigFileConstants.FileFilter, ConfigFileConstants.FilePattern], _localizationService.GetString("Profiles_AddFromFile_Title")) is not { } path)
            return;

        var baseName = Path.GetFileNameWithoutExtension(path);
        var name = Enumerable.Range(1, 99).Select(n => n == 1 ? baseName : $"{baseName} ({n})").FirstOrDefault(_profiles.IsNameAvailable);
        if (name is null || await _profiles.AddFromFileAsync(path, name) is null)
            await _dialogService.ShowErrorAsync(_localizationService.GetString("Profiles_AddFromFile_Failed"));
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
        OnPropertyChanged(nameof(ProfilesLabel));
        OnPropertyChanged(nameof(ProfilesHeader));
        OnPropertyChanged(nameof(ProfilesDescription));
        OnPropertyChanged(nameof(SaveProfileHeader));
        OnPropertyChanged(nameof(SaveProfileDescription));
        OnPropertyChanged(nameof(ProfileNamePlaceholder));
        OnPropertyChanged(nameof(SaveProfileButtonText));
        OnPropertyChanged(nameof(AddProfileFromFileButtonText));
        OnPropertyChanged(nameof(ReapplyHeader));
        OnPropertyChanged(nameof(ReapplyDescription));
        OnPropertyChanged(nameof(ReapplyWarning));
        RefreshProfilesAsync().FireAndForget(_logService);
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
    public string ProfilesLabel => _localizationService.GetString("Profiles_Label");
    public string ProfilesHeader => _localizationService.GetString("Profiles_Title");
    public string ProfilesDescription => _localizationService.GetString("Profiles_Description");
    public string SaveProfileHeader => _localizationService.GetString("Profiles_Save_Title");
    public string SaveProfileDescription => _localizationService.GetString("Profiles_Save_Description");
    public string ProfileNamePlaceholder => _localizationService.GetString("Profiles_Save_Placeholder");
    public string SaveProfileButtonText => _localizationService.GetString("Profiles_Save_Button");
    public string AddProfileFromFileButtonText => _localizationService.GetString("Profiles_AddFromFile_Button");
    public string ReapplyHeader => _localizationService.GetString("Profiles_Reapply_Title");
    public string ReapplyDescription => _localizationService.GetString(
        _interactiveUserService.IsOtsElevation ? "Settings_StartInTray_OtsUnavailable" : "Profiles_Reapply_Description");
    public string ReapplyWarning => _localizationService.GetString("Profiles_Reapply_Warning");
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

public sealed record ProfileRow(
    string Name,
    string Status,
    bool CanDelete,
    string ApplyText,
    string EditText,
    string ExportText,
    string DeleteText,
    IAsyncRelayCommand Apply,
    IAsyncRelayCommand Edit,
    IAsyncRelayCommand Export,
    IAsyncRelayCommand Delete);

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

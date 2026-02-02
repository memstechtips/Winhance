using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System.Diagnostics;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.ViewModels;

/// <summary>
/// ViewModel for the MainWindow, handling title bar commands and state.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IThemeService _themeService;
    private readonly IConfigurationService _configurationService;
    private readonly ILocalizationService _localizationService;
    private readonly IVersionService _versionService;
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _appIconSource = "ms-appx:///Assets/AppIcons/winhance-rocket-white-transparent-bg.png";

    [ObservableProperty]
    private string _versionInfo = "Winhance";

    public MainWindowViewModel(
        IThemeService themeService,
        IConfigurationService configurationService,
        ILocalizationService localizationService,
        IVersionService versionService,
        ILogService logService,
        IDialogService dialogService)
    {
        _themeService = themeService;
        _configurationService = configurationService;
        _localizationService = localizationService;
        _versionService = versionService;
        _logService = logService;
        _dialogService = dialogService;

        // Subscribe to theme changes
        _themeService.ThemeChanged += OnThemeChanged;

        // Set initial icon based on current theme
        UpdateAppIconForTheme();

        // Initialize version info
        InitializeVersionInfo();
    }

    /// <summary>
    /// Initializes the version info string from the version service.
    /// </summary>
    private void InitializeVersionInfo()
    {
        try
        {
            var versionInfo = _versionService.GetCurrentVersion();
            VersionInfo = $"Winhance {versionInfo.Version}";
        }
        catch
        {
            VersionInfo = "Winhance";
        }
    }

    #region Localized Tooltips

    public string SaveConfigTooltip =>
        _localizationService.GetString("Tooltip_SaveConfiguration") ?? "Save Configuration";

    public string ImportConfigTooltip =>
        _localizationService.GetString("Tooltip_ImportConfig") ?? "Import Configuration";

    public string WindowsFilterTooltip =>
        _localizationService.GetString("Tooltip_FilterDisabled") ?? "Windows Version Filter";

    public string DonateTooltip =>
        _localizationService.GetString("Tooltip_Donate") ?? "Donate";

    public string BugReportTooltip =>
        _localizationService.GetString("Tooltip_ReportBug") ?? "Report a Bug";

    #endregion

    #region Commands

    /// <summary>
    /// Command to export/save configuration.
    /// </summary>
    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        try
        {
            await _configurationService.ExportConfigurationAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Command to import configuration.
    /// </summary>
    [RelayCommand]
    private async Task ImportConfigAsync()
    {
        try
        {
            await _configurationService.ImportConfigurationAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to import configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Command to open Windows version filter.
    /// </summary>
    [RelayCommand]
    private void WindowsFilter()
    {
        // TODO: Implement Windows version filter functionality
        // This could open a flyout or dialog to filter settings by Windows version
        System.Diagnostics.Debug.WriteLine("Windows filter button clicked");
    }

    /// <summary>
    /// Command to open the donation page.
    /// </summary>
    [RelayCommand]
    private async Task DonateAsync()
    {
        try
        {
            await Windows.System.Launcher.LaunchUriAsync(
                new Uri("https://ko-fi.com/memstechtips"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open donation page: {ex.Message}");
        }
    }

    /// <summary>
    /// Command to open the bug report page.
    /// </summary>
    [RelayCommand]
    private async Task BugReportAsync()
    {
        try
        {
            await Windows.System.Launcher.LaunchUriAsync(
                new Uri("https://github.com/memstechtips/Winhance/issues"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open bug report page: {ex.Message}");
        }
    }

    /// <summary>
    /// Command to check for application updates.
    /// </summary>
    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            _logService.LogInformation("Checking for updates...");

            var latestVersion = await _versionService.CheckForUpdateAsync();
            var currentVersion = _versionService.GetCurrentVersion();

            if (latestVersion != null && latestVersion.Version != currentVersion.Version)
            {
                _logService.LogInformation($"Update available: {latestVersion.Version}");

                // Build the update message using localized strings
                var message = _localizationService.GetString("Dialog_Update_Message") ?? "Good News! A New Version of Winhance is available.";
                var currentVersionLabel = _localizationService.GetString("Dialog_Update_CurrentVersion") ?? "Current Version:";
                var latestVersionLabel = _localizationService.GetString("Dialog_Update_LatestVersion") ?? "Latest Version:";
                var footer = _localizationService.GetString("Dialog_Update_Footer") ?? "Would you like to download and install the update now?";
                var title = _localizationService.GetString("Dialog_Update_Title") ?? "Update Available";

                var fullMessage = $"{message}\n\n{currentVersionLabel} {currentVersion.Version}\n{latestVersionLabel} {latestVersion.Version}\n\n{footer}";

                var result = await _dialogService.ShowConfirmationAsync(fullMessage, title);

                if (result)
                {
                    await _versionService.DownloadAndInstallUpdateAsync();
                }
            }
            else
            {
                _logService.LogInformation("No updates available");
                var noUpdatesTitle = _localizationService.GetString("Dialog_Update_NoUpdates_Title") ?? "No Updates Available";
                var noUpdatesMessage = _localizationService.GetString("Dialog_Update_NoUpdates_Message") ?? "You have the latest version of Winhance.";
                await _dialogService.ShowInformationAsync(noUpdatesMessage, noUpdatesTitle);
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error checking for updates: {ex.Message}", ex);
            var errorTitle = _localizationService.GetString("Dialog_Update_CheckError_Title") ?? "Update Check Error";
            var errorMessageTemplate = _localizationService.GetString("Dialog_Update_CheckError_Message") ?? "An error occurred while checking for updates: {0}";
            var errorMessage = string.Format(errorMessageTemplate, ex.Message);
            await _dialogService.ShowErrorAsync(errorMessage, errorTitle);
        }
    }

    /// <summary>
    /// Command to open the Winhance logs folder.
    /// </summary>
    [RelayCommand]
    private void OpenLogs()
    {
        try
        {
            string logsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Winhance",
                "Logs");

            if (!Directory.Exists(logsFolder))
            {
                Directory.CreateDirectory(logsFolder);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = logsFolder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error opening logs folder: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Command to open the Winhance scripts folder.
    /// </summary>
    [RelayCommand]
    private void OpenScripts()
    {
        try
        {
            string scriptsFolder = ScriptPaths.ScriptsDirectory;

            if (!Directory.Exists(scriptsFolder))
            {
                Directory.CreateDirectory(scriptsFolder);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = scriptsFolder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error opening scripts folder: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Command to close the application.
    /// </summary>
    [RelayCommand]
    private void CloseApplication()
    {
        try
        {
            _logService.LogInformation("User requested application close from More menu");
            Application.Current.Exit();
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error closing application: {ex.Message}", ex);
        }
    }

    #endregion

    #region Theme Handling

    /// <summary>
    /// Handles theme changes to update the app icon.
    /// </summary>
    private void OnThemeChanged(object? sender, WinhanceTheme theme)
    {
        UpdateAppIconForTheme();
    }

    /// <summary>
    /// Updates the app icon based on the current effective theme.
    /// </summary>
    public void UpdateAppIconForTheme()
    {
        var effectiveTheme = _themeService.GetEffectiveTheme();
        // Use white icon on dark background, black icon on light background
        AppIconSource = effectiveTheme == ElementTheme.Dark
            ? "ms-appx:///Assets/AppIcons/winhance-rocket-white-transparent-bg.png"
            : "ms-appx:///Assets/AppIcons/winhance-rocket-black-transparent-bg.png";
    }

    #endregion
}

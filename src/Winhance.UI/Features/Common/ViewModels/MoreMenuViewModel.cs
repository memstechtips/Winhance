using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System.Diagnostics;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.ViewModels;

/// <summary>
/// ViewModel for the More menu flyout, providing localized strings and commands.
/// </summary>
public partial class MoreMenuViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;
    private readonly IVersionService _versionService;
    private readonly ILogService _logService;
    private readonly IApplicationCloseService _applicationCloseService;

    [ObservableProperty]
    private string _versionInfo = "Winhance";

    public MoreMenuViewModel(
        ILocalizationService localizationService,
        IVersionService versionService,
        ILogService logService,
        IApplicationCloseService applicationCloseService)
    {
        _localizationService = localizationService;
        _versionService = versionService;
        _logService = logService;
        _applicationCloseService = applicationCloseService;

        // Subscribe to language changes
        _localizationService.LanguageChanged += OnLanguageChanged;

        InitializeVersionInfo();
    }

    /// <summary>
    /// Handles language changes to update localized strings.
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(MenuDocumentation));
        OnPropertyChanged(nameof(MenuReportBug));
        OnPropertyChanged(nameof(MenuCheckForUpdates));
        OnPropertyChanged(nameof(MenuWinhanceLogs));
        OnPropertyChanged(nameof(MenuWinhanceScripts));
        OnPropertyChanged(nameof(MenuCloseWinhance));
    }

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

    #region Localized Strings

    public string MenuDocumentation =>
        _localizationService.GetString("Tooltip_Documentation") ?? "Documentation";

    public string MenuReportBug =>
        _localizationService.GetString("Tooltip_ReportBug") ?? "Report a Bug";

    public string MenuCheckForUpdates =>
        _localizationService.GetString("Menu_CheckForUpdates") ?? "Check for Updates";

    public string MenuWinhanceLogs =>
        _localizationService.GetString("Menu_WinhanceLogs") ?? "Winhance Logs";

    public string MenuWinhanceScripts =>
        _localizationService.GetString("Menu_WinhanceScripts") ?? "Winhance Scripts";

    public string MenuCloseWinhance =>
        _localizationService.GetString("Menu_CloseWinhance") ?? "Close Winhance";

    #endregion

    #region Commands

    [RelayCommand]
    private async Task OpenDocsAsync()
    {
        try
        {
            await Windows.System.Launcher.LaunchUriAsync(
                new Uri("https://winhance.net/docs/index.html"));
        }
        catch (Exception ex)
        {
            _logService.LogError($"Failed to open documentation page: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task ReportBugAsync()
    {
        try
        {
            await Windows.System.Launcher.LaunchUriAsync(
                new Uri("https://github.com/memstechtips/Winhance/issues"));
        }
        catch (Exception ex)
        {
            _logService.LogError($"Failed to open bug report page: {ex.Message}", ex);
        }
    }

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

    [RelayCommand]
    private async Task CloseApplicationAsync()
    {
        try
        {
            _logService.LogInformation("User requested application close from More menu");
            await _applicationCloseService.CheckOperationsAndCloseAsync();
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error closing application: {ex.Message}", ex);
        }
    }

    #endregion
}

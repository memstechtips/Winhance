using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private readonly IFileSystemService _fileSystemService;
    private readonly IExplorerWindowManager _explorerWindowManager;

    [ObservableProperty]
    public partial string VersionInfo { get; set; }

    public MoreMenuViewModel(
        ILocalizationService localizationService,
        IVersionService versionService,
        ILogService logService,
        IApplicationCloseService applicationCloseService,
        IFileSystemService fileSystemService,
        IExplorerWindowManager explorerWindowManager)
    {
        _localizationService = localizationService;
        _versionService = versionService;
        _logService = logService;
        _applicationCloseService = applicationCloseService;
        _fileSystemService = fileSystemService;
        _explorerWindowManager = explorerWindowManager;
        VersionInfo = "Winhance";

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
        catch (Exception ex)
        {
            _logService.LogDebug($"[MoreMenuViewModel] Failed to get version info: {ex.Message}");
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
    private async Task OpenLogsAsync()
    {
        try
        {
            string logsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Winhance",
                "Logs");

            if (!_fileSystemService.DirectoryExists(logsFolder))
            {
                _fileSystemService.CreateDirectory(logsFolder);
            }

            await _explorerWindowManager.OpenFolderAsync(logsFolder);
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error opening logs folder: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task OpenScriptsAsync()
    {
        try
        {
            string scriptsFolder = ScriptPaths.ScriptsDirectory;

            if (!_fileSystemService.DirectoryExists(scriptsFolder))
            {
                _fileSystemService.CreateDirectory(scriptsFolder);
            }

            await _explorerWindowManager.OpenFolderAsync(scriptsFolder);
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

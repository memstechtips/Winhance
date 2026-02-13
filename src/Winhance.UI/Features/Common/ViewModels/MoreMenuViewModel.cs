using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.ViewModels;

/// <summary>
/// ViewModel for the More menu flyout, providing localized strings and commands.
/// </summary>
public partial class MoreMenuViewModel : ObservableObject
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private const int SW_RESTORE = 9;

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

            OpenFolderOrBringToForeground(logsFolder);
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

            OpenFolderOrBringToForeground(scriptsFolder);
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error opening scripts folder: {ex.Message}", ex);
        }
    }

    private void OpenFolderOrBringToForeground(string folderPath)
    {
        string normalizedPath = Path.GetFullPath(folderPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToLowerInvariant();

        try
        {
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType != null)
            {
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic windows = shell.Windows();

                foreach (dynamic window in windows)
                {
                    try
                    {
                        string? locationUrl = window.LocationURL;
                        if (string.IsNullOrEmpty(locationUrl))
                            continue;

                        Uri uri = new Uri(locationUrl);
                        string windowPath = Path.GetFullPath(uri.LocalPath)
                            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            .ToLowerInvariant();

                        if (windowPath == normalizedPath)
                        {
                            IntPtr handle = new IntPtr(window.HWND);
                            if (IsIconic(handle))
                            {
                                ShowWindow(handle, SW_RESTORE);
                            }
                            SetForegroundWindow(handle);
                            return;
                        }
                    }
                    catch
                    {
                        // Skip windows that can't be inspected
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Error checking for existing Explorer windows: {ex.Message}");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = folderPath,
            UseShellExecute = true
        });
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

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Views;

namespace Winhance.WPF.Features.Common.ViewModels;

public class MoreMenuViewModel : ObservableObject
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    private readonly ILogService _logService;
    private readonly IVersionService _versionService;
    private readonly IEventBus _eventBus;
    private readonly IApplicationCloseService _applicationCloseService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IUserPreferencesService _preferencesService;

    private string _versionInfo;

    public MoreMenuViewModel(
        ILogService logService,
        IVersionService versionService,
        IEventBus eventBus,
        IApplicationCloseService applicationCloseService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IUserPreferencesService preferencesService)
    {
        _logService = logService;
        _versionService = versionService;
        _eventBus = eventBus;
        _applicationCloseService = applicationCloseService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _preferencesService = preferencesService;

        _versionInfo = GetVersionInfo();

        CheckForUpdatesCommand = new AsyncRelayCommand(
            execute: async () =>
            {
                _logService.LogInformation("CheckForUpdatesCommand executed");
                CloseFlyout();
                await CheckForUpdatesAsync();
            }
        );

        OpenLogsCommand = new RelayCommand(
            execute: () =>
            {
                _logService.LogInformation("OpenLogsCommand executed");
                CloseFlyout();
                OpenLogs();
            },
            canExecute: () => true
        );

        OpenScriptsCommand = new RelayCommand(
            execute: () =>
            {
                _logService.LogInformation("OpenScriptsCommand executed");
                CloseFlyout();
                OpenScripts();
            },
            canExecute: () => true
        );

        CloseApplicationCommand = new RelayCommand(
            execute: () =>
            {
                _logService.LogInformation("CloseApplicationCommand executed");
                CloseFlyout();
                CloseApplication();
            },
            canExecute: () => true
        );

    }

    private string GetVersionInfo()
    {
        try
        {
            var versionInfo = _versionService.GetCurrentVersion();
            return $"Winhance {versionInfo.Version}";
        }
        catch
        {
            return "Winhance";
        }
    }

    public string VersionInfo
    {
        get => _versionInfo;
        set => SetProperty(ref _versionInfo, value);
    }

    public ICommand CheckForUpdatesCommand { get; }

    public ICommand OpenLogsCommand { get; }

    public ICommand OpenScriptsCommand { get; }

    public ICommand CloseApplicationCommand { get; }

    public ICommand ChangeLanguageCommand { get; }


    private void CloseFlyout()
    {
        try
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow?.DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.CloseMoreMenuFlyout();
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error closing flyout: {ex.Message}", ex);
        }
    }


    private async Task CheckForUpdatesAsync()
    {
        try
        {
            _logService.LogInformation("Starting update check");

            var latestVersion = await _versionService.CheckForUpdateAsync();
            var currentVersion = _versionService.GetCurrentVersion();

            if (latestVersion != null && latestVersion.Version != currentVersion.Version)
            {
                _logService.LogInformation("Showing update dialog");
                await UpdateDialog.ShowAsync(
                    currentVersion,
                    latestVersion,
                    async () =>
                    {
                        _logService.LogInformation(
                            "User initiated update download and installation"
                        );
                        await _versionService.DownloadAndInstallUpdateAsync();
                    }
                );
            }
            else
            {
                _logService.LogInformation("No updates available");
                await _dialogService.ShowInformationAsync(
                    "You have the latest version of Winhance.",
                    "No Updates Available"
                );
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error checking for updates: {ex.Message}", ex);

            await _dialogService.ShowErrorAsync(
                $"An error occurred while checking for updates: {ex.Message}",
                "Update Check Error"
            );
        }
    }

    private void OpenLogs()
    {
        try
        {
            string logsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Winhance",
                "Logs"
            );

            if (!Directory.Exists(logsFolder))
            {
                Directory.CreateDirectory(logsFolder);
            }

            OpenFolderOrBringToForeground(logsFolder);
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error opening logs folder: {ex.Message}", ex);

            _dialogService.ShowErrorAsync(
                $"An error occurred while opening the logs folder: {ex.Message}",
                "Logs Folder Error"
            );
        }
    }

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

            _dialogService.ShowErrorAsync(
                $"An error occurred while opening the scripts folder: {ex.Message}",
                "Scripts Folder Error"
            );
        }
    }

    private async void CloseApplication()
    {
        try
        {
            await _applicationCloseService.CheckOperationsAndCloseAsync();
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error closing application: {ex.Message}", ex);
        }
    }

    private void OpenFolderOrBringToForeground(string folderPath)
    {
        string normalizedPath = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();

        try
        {
            Type shellType = Type.GetTypeFromProgID("Shell.Application");
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic windows = shell.Windows();

            foreach (dynamic window in windows)
            {
                try
                {
                    string locationUrl = window.LocationURL;
                    if (string.IsNullOrEmpty(locationUrl))
                        continue;

                    Uri uri = new Uri(locationUrl);
                    string windowPath = Path.GetFullPath(uri.LocalPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();

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
                }
            }
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Error checking windows: {ex.Message}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = folderPath,
            UseShellExecute = true,
        };
        Process.Start(psi);
    }
}
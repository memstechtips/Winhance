using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Services;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.SoftwareApps;
using Winhance.UI.ViewModels;
using System;
using System.Threading.Tasks;

namespace Winhance.UI.Helpers;

/// <summary>
/// Manages the startup loading overlay and initial navigation sequence.
/// Extracted from MainWindow to reduce code-behind complexity.
/// </summary>
internal sealed class StartupUiCoordinator
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ILogService? _logService;

    /// <summary>
    /// The backup result from the startup sequence, if any.
    /// </summary>
    public BackupResult? BackupResult { get; private set; }

    public StartupUiCoordinator(DispatcherQueue dispatcherQueue, ILogService? logService)
    {
        _dispatcherQueue = dispatcherQueue;
        _logService = logService;
    }

    /// <summary>
    /// Sets all overlay text from localization keys and updates the loading logo.
    /// </summary>
    public void InitializeLoadingOverlay(
        TextBlock loadingTitleText,
        TextBlock loadingTaglineText,
        TextBlock loadingStatusText,
        Image loadingLogo,
        Grid rootGrid)
    {
        UpdateLoadingLogo(loadingLogo, rootGrid);

        try
        {
            var localizationService = App.Services.GetService<ILocalizationService>();
            if (localizationService != null)
            {
                loadingTitleText.Text = localizationService.GetString("App_Title");
                loadingTaglineText.Text = localizationService.GetString("App_Tagline");
                loadingStatusText.Text = localizationService.GetString("Loading_PreparingApp");
            }
        }
        catch (Exception ex)
        {
            App.Services.GetService<ILogService>()?.LogDebug($"Failed to set loading overlay text: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets the loading overlay logo based on the current theme.
    /// </summary>
    public void UpdateLoadingLogo(Image loadingLogo, Grid rootGrid)
    {
        try
        {
            var isDark = rootGrid.ActualTheme != ElementTheme.Light;
            var logoUri = isDark
                ? "ms-appx:///Assets/AppIcons/winhance-rocket-white-transparent-bg.png"
                : "ms-appx:///Assets/AppIcons/winhance-rocket-black-transparent-bg.png";
            loadingLogo.Source = new BitmapImage(new Uri(logoUri));
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to set loading logo: {ex.Message}");
        }
    }

    /// <summary>
    /// Delegates startup to the orchestrator, then dispatches completion on the UI thread.
    /// </summary>
    public async Task RunStartupAndCompleteAsync(
        TextBlock loadingStatusText,
        Frame contentFrame,
        NavSidebar navSidebar,
        Grid loadingOverlay,
        MainWindowViewModel? viewModel,
        Action markStartupComplete)
    {
        try
        {
            var orchestrator = App.Services.GetRequiredService<IStartupOrchestrator>();

            var statusProgress = new Progress<string>(localizationKey =>
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        var localizationService = App.Services.GetService<ILocalizationService>();
                        loadingStatusText.Text = localizationService?.GetString(localizationKey) ?? localizationKey;
                    }
                    catch
                    {
                        loadingStatusText.Text = localizationKey;
                    }
                });
            });

            var detailedProgress = new Progress<TaskProgressDetail>(detail =>
            {
                if (!string.IsNullOrEmpty(detail.StatusText))
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        try { loadingStatusText.Text = detail.StatusText; }
                        catch (Exception ex)
                        {
                            App.Services.GetService<ILogService>()?.LogDebug(
                                $"Failed to update loading status text: {ex.Message}");
                        }
                    });
                }
            });

            var result = await orchestrator.RunStartupSequenceAsync(statusProgress, detailedProgress)
                .ConfigureAwait(false);
            BackupResult = result.BackupResult;
        }
        catch (Exception ex)
        {
            StartupLogger.Log("StartupUiCoordinator", $"RunStartupAndCompleteAsync EXCEPTION: {ex}");
        }

        // Always complete startup on the UI thread so the app is usable
        _dispatcherQueue.TryEnqueue(() =>
            _ = CompleteStartupAsync(contentFrame, navSidebar, loadingOverlay, viewModel, markStartupComplete));
    }

    /// <summary>
    /// Navigates to SoftwareApps, waits for initialization, then hides the loading overlay.
    /// </summary>
    private async Task CompleteStartupAsync(
        Frame contentFrame,
        NavSidebar navSidebar,
        Grid loadingOverlay,
        MainWindowViewModel? viewModel,
        Action markStartupComplete)
    {
        StartupLogger.Log("StartupUiCoordinator", "CompleteStartupAsync starting");

        try
        {
            // Navigate to SoftwareApps with "startup" parameter to prevent double-init
            navSidebar.SelectedTag = "SoftwareApps";
            contentFrame.Navigate(typeof(SoftwareAppsPage), "startup");

            // Wait for the SoftwareApps page to finish loading apps + installation status
            var page = contentFrame.Content as SoftwareAppsPage;
            if (page != null)
            {
                StartupLogger.Log("StartupUiCoordinator", "Awaiting SoftwareApps initialization...");
                await page.ViewModel.InitializeAsync();
                StartupLogger.Log("StartupUiCoordinator", "SoftwareApps initialization complete");
            }
        }
        catch (Exception ex)
        {
            StartupLogger.Log("StartupUiCoordinator", $"SoftwareApps initialization failed: {ex.Message}");
            _logService?.LogWarning($"SoftwareApps init failed: {ex.Message}");
        }

        // Hide overlay and mark startup complete
        markStartupComplete();
        loadingOverlay.Visibility = Visibility.Collapsed;
        StartupLogger.Log("StartupUiCoordinator", "Startup complete, overlay hidden");

        // Show backup notification dialog if backups were created
        try
        {
            var startupNotifications = App.Services.GetRequiredService<IStartupNotificationService>();
            if (BackupResult != null)
            {
                await startupNotifications.ShowBackupNotificationAsync(BackupResult);
            }
        }
        catch (Exception ex)
        {
            StartupLogger.Log("StartupUiCoordinator", $"Startup notification failed: {ex.Message}");
        }

        // Check for updates silently (only shows InfoBar if update available)
        // Ensure WinGet is ready (shows task progress if installation/update needed)
        if (viewModel != null)
        {
            _ = viewModel.UpdateCheck.CheckForUpdatesOnStartupAsync();
            _ = viewModel.EnsureWinGetReadyOnStartupAsync();
        }
    }
}

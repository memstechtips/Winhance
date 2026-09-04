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

namespace Winhance.UI.Helpers;

internal sealed class StartupUiCoordinator
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ILogService? _logService;

    public bool IsFirstLaunch { get; private set; }

    public StartupUiCoordinator(DispatcherQueue dispatcherQueue, ILogService? logService)
    {
        _dispatcherQueue = dispatcherQueue;
        _logService = logService;
    }

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

    public void UpdateLoadingLogo(Image loadingLogo, Grid rootGrid)
    {
        try
        {
            var isDark = rootGrid.ActualTheme != ElementTheme.Light;
            var logoUri = isDark
                ? "ms-appx:///Assets/AppIcons/winhance-rocket-white-transparent-bg.png"
                : "ms-appx:///Assets/AppIcons/winhance-rocket-black-transparent-bg.png";
            var bitmapImage = new BitmapImage();
            bitmapImage.DecodePixelWidth = 256;
            bitmapImage.DecodePixelHeight = 256;
            bitmapImage.DecodePixelType = DecodePixelType.Logical;
            bitmapImage.UriSource = new Uri(logoUri);
            loadingLogo.Source = bitmapImage;
        }
        catch (Exception ex)
        {
            _logService?.LogDebug($"Failed to set loading logo: {ex.Message}");
        }
    }

    public async Task RunStartupAndCompleteAsync(
        TextBlock loadingStatusText,
        Frame contentFrame,
        NavSidebar navSidebar,
        Grid loadingOverlay,
        Func<MainWindowViewModel?> getViewModel,
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
                        loadingStatusText.Text = localizationService.GetStringOrDefault(localizationKey, localizationKey);
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
            IsFirstLaunch = result.IsFirstLaunch;
        }
        catch (Exception ex)
        {
            StartupLogger.Log($"RunStartupAndCompleteAsync EXCEPTION: {ex}");
        }

        // Always complete startup on the UI thread so the app is usable.
        // A bare DispatcherQueue.TryEnqueue callback runs on the UI thread but
        // installs no SynchronizationContext — so every `await` of background
        // work inside CompleteStartupAsync (SoftwareApps load, icon resolution)
        // would resume on a thread-pool thread and mutate bound UI off-thread,
        // leaving the Windows/External Apps lists stuck on "Loaded N items"
        // with stale install status. Install the dispatcher SynchronizationContext
        // so continuations marshal back to the UI thread for the whole chain.
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (SynchronizationContext.Current is null)
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherQueueSynchronizationContext(_dispatcherQueue));
            }

            _ = CompleteStartupAsync(contentFrame, navSidebar, loadingOverlay, getViewModel, markStartupComplete);
        });
    }

    private async Task CompleteStartupAsync(
        Frame contentFrame,
        NavSidebar navSidebar,
        Grid loadingOverlay,
        Func<MainWindowViewModel?> getViewModel,
        Action markStartupComplete)
    {
        StartupLogger.Log("CompleteStartupAsync starting");

        try
        {
            // Navigate to SoftwareApps with "startup" parameter to prevent double-init
            navSidebar.SelectedTag = "SoftwareApps";
            contentFrame.Navigate(typeof(SoftwareAppsPage), "startup");

            // Block startup on Windows Apps only — those icons resolve fast (mostly
            // local AppX). External Apps icon resolution hits Wikimedia, which can
            // take tens of seconds on a cold burst; we kick that off in the
            // background so the main window appears promptly. The External Apps
            // tab has its own loading overlay (bound to ExternalAppsViewModel.IsLoading)
            // that covers the user-clicks-tab-before-ready case.
            var page = contentFrame.Content as SoftwareAppsPage;
            if (page != null)
            {
                StartupLogger.Log("Awaiting Windows Apps initialization...");
                await page.ViewModel.InitializeWindowsAppsAsync();
                StartupLogger.Log("Windows Apps initialization complete");

                StartupLogger.Log("Kicking off External Apps initialization in background");
                page.ViewModel.InitializeExternalAppsAsync().FireAndForget(_logService!);
            }
        }
        catch (Exception ex)
        {
            StartupLogger.Log($"SoftwareApps initialization failed: {ex.Message}");
            _logService?.LogWarning($"SoftwareApps init failed: {ex.Message}");
        }

        markStartupComplete();
        loadingOverlay.Visibility = Visibility.Collapsed;
        StartupLogger.Log("Startup complete, overlay hidden");

        try
        {
            if (IsFirstLaunch)
            {
                var startupNotifications = App.Services.GetRequiredService<IStartupNotificationService>();
                await startupNotifications.ShowFirstLaunchRestoreOfferAsync();
            }
        }
        catch (Exception ex)
        {
            StartupLogger.Log($"Startup notification failed: {ex.Message}");
        }

        // Pre-warm the sponsors cache so the exit dialog opens instantly even offline.
        App.Services.GetService<ISponsorsService>()?.GetSponsorsAsync().FireAndForget(_logService!);

        var viewModel = getViewModel();
        if (viewModel != null)
        {
            _ = viewModel.UpdateCheck.CheckForUpdatesOnStartupAsync();
        }
    }
}

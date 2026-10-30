using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Services;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Dialogs;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.SoftwareApps;
using Winhance.UI.ViewModels;

namespace Winhance.UI.Helpers;

internal sealed class StartupUiCoordinator
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ILogService? _logService;

    private const int MaxLoadingSponsors = 3;
    private const double LoadingSponsorLogoSize = 28d;
    private const double LoadingSponsorPillInset = 6d;
    // Softly rounded, not a full pill: about the corner ratio of a sponsor logo tile.
    private const double LoadingSponsorPillRadius = 8d;
    // A logo still loading from the network after this long no longer holds the block back.
    private const int LoadingSponsorsMaxWaitMs = 1500;
    // Sponsors pay to be seen here, so the splash holds this long once they are on screen even if start-up finished.
    private const int LoadingSponsorsMinShownMs = 3000;

    private Task _sponsorsShown = Task.CompletedTask;

    public bool IsFirstLaunch { get; private set; }

    public StartupUiCoordinator(DispatcherQueue dispatcherQueue, ILogService? logService)
    {
        _dispatcherQueue = dispatcherQueue;
        _logService = logService;
    }

    public void InitializeLoadingOverlay(
        TextBlock loadingTitleText,
        TextBlock loadingTaglineText,
        TextBlock loadingStatusText)
    {
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

    // Real gold and emerald sponsors on the splash, read from the offline snapshot so the overlay never waits on the
    // network. The block stays invisible until every logo has loaded, then fades in whole, so nothing pops in late.
    public void ShowLoadingSponsors(StackPanel loadingSponsors, TextBlock loadingSponsorsHeading, StackPanel loadingSponsorPills)
    {
        _sponsorsShown = ShowLoadingSponsorsAsync(loadingSponsors, loadingSponsorsHeading, loadingSponsorPills);
    }

    private static async Task ShowLoadingSponsorsAsync(StackPanel loadingSponsors, TextBlock loadingSponsorsHeading, StackPanel loadingSponsorPills)
    {
        var sponsorsService = App.Services.GetService<ISponsorsService>();
        var localizationService = App.Services.GetService<ILocalizationService>();
        if (sponsorsService == null || localizationService == null)
            return;

        var snapshot = await sponsorsService.GetSnapshotAsync();
        var sponsors = SponsorRow.Pick(snapshot?.Sponsors).Take(MaxLoadingSponsors).ToList();
        if (sponsors.Count == 0)
            return;

        int pending = sponsors.Count;
        var logosReady = new TaskCompletionSource();
        void LogoSettled()
        {
            if (--pending == 0)
                logosReady.TrySetResult();
        }

        loadingSponsorsHeading.Text = localizationService.GetString("Support_ThankBusinessSponsors");
        foreach (var sponsor in sponsors)
            loadingSponsorPills.Children.Add(BuildSponsorPill(sponsorsService, sponsor, LogoSettled));

        // Visible at opacity 0 rather than collapsed: an Image outside the live tree never decodes, so it would never open.
        loadingSponsors.Visibility = Visibility.Visible;
        await Task.WhenAny(logosReady.Task, Task.Delay(LoadingSponsorsMaxWaitMs));
        loadingSponsors.Opacity = 1;
        await Task.Delay(LoadingSponsorsMinShownMs);
    }

    private static Border BuildSponsorPill(ISponsorsService sponsorsService, SponsorEntry sponsor, Action logoSettled)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };
        content.Children.Add(SponsorLogo.BuildImage(sponsorsService, sponsor, LoadingSponsorLogoSize, logoSettled));
        content.Children.Add(new TextBlock
        {
            Text = sponsor.Name,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        return new Border
        {
            Style = CodeBuiltStyle.Named("CodeBuilt.CardSurface"),
            Child = content,
            Padding = new Thickness(LoadingSponsorPillInset, LoadingSponsorPillInset, 16, LoadingSponsorPillInset),
            CornerRadius = new CornerRadius(LoadingSponsorPillRadius)
        };
    }

    private static async Task LoadAppListsAsync(SoftwareAppsPage page)
    {
        await page.ViewModel.InitializeWindowsAppsAsync();
        await page.ViewModel.InitializeExternalAppsAsync();
    }

    private static async Task PrewarmSponsorsAsync()
    {
        var sponsorsService = App.Services.GetService<ISponsorsService>();
        if (sponsorsService == null)
            return;

        await sponsorsService.GetSponsorsAsync();
        await sponsorsService.CacheLogosAsync();
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
        // leaving the Windows/External Apps lists with stale install status. Install the dispatcher SynchronizationContext
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

        SoftwareAppsPage? page = null;
        var launch = App.Services.GetRequiredService<LaunchOptions>();
        try
        {
            // Navigate to SoftwareApps with "startup" parameter to prevent double-init
            navSidebar.SelectedTag = "SoftwareApps";
            contentFrame.Navigate(typeof(SoftwareAppsPage), "startup");

            // Both app tabs have their own loading overlay, so the window opens before they finish (#702). Only an
            // install or config run from the command line needs the Windows Apps states first. External Apps waits
            // for Windows Apps either way: the two share the winget detection cache.
            page = contentFrame.Content as SoftwareAppsPage;
            if (page != null)
            {
                if (launch.InstallsApps || launch.ConfigFile is not null)
                {
                    StartupLogger.Log("Awaiting Windows Apps initialization...");
                    await page.ViewModel.InitializeWindowsAppsAsync();
                    StartupLogger.Log("Windows Apps initialization complete");
                    page.ViewModel.InitializeExternalAppsAsync().FireAndForget(_logService!);
                }
                else
                {
                    StartupLogger.Log("Loading the app lists in the background");
                    LoadAppListsAsync(page).FireAndForget(_logService!);
                }
            }
        }
        catch (Exception ex)
        {
            StartupLogger.Log($"SoftwareApps initialization failed: {ex.Message}");
            _logService?.LogWarning($"SoftwareApps init failed: {ex.Message}");
        }

        try
        {
            await _sponsorsShown;
        }
        catch (Exception ex)
        {
            _logService?.LogWarning($"Showing the splash sponsors failed: {ex.Message}");
        }

        markStartupComplete();
        loadingOverlay.Visibility = Visibility.Collapsed;

        // Out of the tree for good: it is never shown again, and leaving stops the rocket on it.
        if (loadingOverlay.Parent is Panel host)
            host.Children.Remove(loadingOverlay);

        StartupLogger.Log("Startup complete, overlay hidden");

        if (launch.SwitchesProfile)
        {
            var tray = App.Services.GetRequiredService<TrayController>();
            try
            {
                // A launch made only to switch, like the sign-in re-apply, leaves nothing running behind it.
                if (await tray.RunCommandLineProfileAsync(launch) && !launch.StartInTray && !tray.StaysOpen)
                {
                    tray.RemoveIcon();
                    Application.Current.Exit();
                    return;
                }
            }
            catch (Exception ex)
            {
                _logService?.LogError("Switching the command-line profile failed", ex);
            }
        }

        if (launch.ConfigFile is not null)
        {
            try
            {
                await App.Services.GetRequiredService<IConfigurationService>().ImportConfigurationFromFileAsync(launch.ConfigFile);
            }
            catch (Exception ex)
            {
                StartupLogger.Log($"Applying the command-line config failed: {ex.Message}");
                _logService?.LogError("Applying the command-line config failed", ex);
            }
        }

        if (launch.InstallsApps && page != null)
        {
            var installOptions = App.Services.GetRequiredService<IInstallOptionsService>();
            if (launch.InstallOptions is { } carried)
                installOptions.BeginSession(carried);
            try
            {
                await page.ViewModel.InstallOnNewPcAsync(launch.InstallApps, launch.InstallFolder);
            }
            catch (Exception ex)
            {
                StartupLogger.Log($"Installing the apps for this PC failed: {ex.Message}");
                _logService?.LogError("Installing the apps for this PC failed", ex);
            }
            finally
            {
                if (launch.InstallOptions is not null)
                    installOptions.EndSession();
            }
        }

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

        // Pre-warm the sponsors cache so the exit dialog opens instantly even offline, and the next splash has the logos.
        PrewarmSponsorsAsync().FireAndForget(_logService!);

        var viewModel = getViewModel();
        if (viewModel != null)
        {
            _ = viewModel.UpdateCheck.CheckForUpdatesOnStartupAsync();
        }
    }
}

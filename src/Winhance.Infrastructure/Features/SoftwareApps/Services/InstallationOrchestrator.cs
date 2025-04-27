using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Exceptions;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Orchestrates the installation and removal of applications, capabilities, and features.
/// </summary>
public class InstallationOrchestrator : IInstallationOrchestrator
{
    private readonly IAppInstallationService _appInstallationService;
    private readonly ICapabilityInstallationService _capabilityInstallationService;
    private readonly IFeatureInstallationService _featureInstallationService;
    private readonly IAppRemovalService _appRemovalService;
    private readonly ICapabilityRemovalService _capabilityRemovalService;
    private readonly IFeatureRemovalService _featureRemovalService;
    private readonly ILogService _logService;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallationOrchestrator"/> class.
    /// </summary>
    /// <param name="appInstallationService">The app installation service.</param>
    /// <param name="capabilityInstallationService">The capability installation service.</param>
    /// <param name="featureInstallationService">The feature installation service.</param>
    /// <param name="appRemovalService">The app removal service.</param>
    /// <param name="capabilityRemovalService">The capability removal service.</param>
    /// <param name="featureRemovalService">The feature removal service.</param>
    /// <param name="logService">The log service.</param>
    public InstallationOrchestrator(
        IAppInstallationService appInstallationService,
        ICapabilityInstallationService capabilityInstallationService,
        IFeatureInstallationService featureInstallationService,
        IAppRemovalService appRemovalService,
        ICapabilityRemovalService capabilityRemovalService,
        IFeatureRemovalService featureRemovalService,
        ILogService logService)
    {
        _appInstallationService = appInstallationService ?? throw new ArgumentNullException(nameof(appInstallationService));
        _capabilityInstallationService = capabilityInstallationService ?? throw new ArgumentNullException(nameof(capabilityInstallationService));
        _featureInstallationService = featureInstallationService ?? throw new ArgumentNullException(nameof(featureInstallationService));
        _appRemovalService = appRemovalService ?? throw new ArgumentNullException(nameof(appRemovalService));
        _capabilityRemovalService = capabilityRemovalService ?? throw new ArgumentNullException(nameof(capabilityRemovalService));
        _featureRemovalService = featureRemovalService ?? throw new ArgumentNullException(nameof(featureRemovalService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    /// <summary>
    /// Installs an application, capability, or feature.
    /// </summary>
    /// <param name="item">The item to install.</param>
    /// <param name="progress">The progress reporter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InstallAsync(
        IInstallableItem item,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        _logService.LogInformation($"Installing {item.DisplayName}");

        try
        {
            if (item is AppInfo appInfo)
            {
                await _appInstallationService.InstallAppAsync(appInfo, progress, cancellationToken);
            }
            else if (item is CapabilityInfo capabilityInfo)
            {
                // Assuming InstallCapabilityAsync should be called for single items
                await _capabilityInstallationService.InstallCapabilityAsync(capabilityInfo, progress, cancellationToken);
            }
            else if (item is FeatureInfo featureInfo)
            {
                // Corrected method name
                await _featureInstallationService.InstallFeatureAsync(featureInfo, progress, cancellationToken);
            }
            else
            {
                throw new ArgumentException($"Unsupported item type: {item.GetType().Name}", nameof(item));
            }

            _logService.LogSuccess($"Successfully installed {item.DisplayName}");
        }
            catch (Exception ex) when (ex is not InstallationException)
            {
                _logService.LogError($"Failed to install {item.DisplayName}", ex);
                throw new InstallationException(item.DisplayName, $"Installation failed for {item.DisplayName}", false, ex);
            }
    }

    /// <summary>
    /// Removes an application, capability, or feature.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RemoveAsync(IInstallableItem item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        _logService.LogInformation($"Removing {item.DisplayName}");

        try
        {
            if (item is AppInfo appInfo)
            {
                await _appRemovalService.RemoveAppAsync(appInfo);
            }
            else if (item is CapabilityInfo capabilityInfo)
            {
                await _capabilityRemovalService.RemoveCapabilityAsync(capabilityInfo);
            }
            else if (item is FeatureInfo featureInfo)
            {
                await _featureRemovalService.RemoveFeatureAsync(featureInfo);
            }
            else
            {
                throw new ArgumentException($"Unsupported item type: {item.GetType().Name}", nameof(item));
            }

            _logService.LogSuccess($"Successfully removed {item.DisplayName}");
        }
        catch (Exception ex) when (ex is not RemovalException)
        {
            _logService.LogError($"Failed to remove {item.DisplayName}", ex);
            throw new RemovalException(item.DisplayName, ex.Message, true, ex);
        }
    }

    /// <summary>
    /// Installs multiple items in batch.
    /// </summary>
    /// <param name="items">The items to install.</param>
    /// <param name="progress">The progress reporter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InstallBatchAsync(
        IEnumerable<IInstallableItem> items,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        var itemsList = new List<IInstallableItem>(items);
        _logService.LogInformation($"Installing {itemsList.Count} items in batch");

        int totalItems = itemsList.Count;
        int completedItems = 0;

        foreach (var item in itemsList)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logService.LogWarning("Batch installation cancelled");
                break;
            }

            try
            {
                // Create a progress wrapper that scales the progress to the current item's portion
                var itemProgress = progress != null
                    ? new Progress<TaskProgressDetail>(detail =>
                    {
                        // Scale the progress to the current item's portion of the total
                        double scaledProgress = (completedItems * 100.0 + (detail.Progress ?? 0)) / totalItems;
                        
                        progress.Report(new TaskProgressDetail
                        {
                            Progress = scaledProgress,
                            StatusText = $"[{completedItems + 1}/{totalItems}] {detail.StatusText}",
                            DetailedMessage = detail.DetailedMessage,
                            LogLevel = detail.LogLevel
                        });
                    })
                    : null;

                await InstallAsync(item, itemProgress, cancellationToken);
                completedItems++;

                // Report overall progress
                progress?.Report(new TaskProgressDetail
                {
                    Progress = completedItems * 100.0 / totalItems,
                    StatusText = $"Completed {completedItems} of {totalItems} items",
                    DetailedMessage = $"Successfully installed {item.DisplayName}"
                });
            }
            catch (Exception ex) when (ex is not InstallationException)
            {
                _logService.LogError($"Error installing {item.DisplayName}", ex);
                
                // Report error but continue with next item
                progress?.Report(new TaskProgressDetail
                {
                    Progress = completedItems * 100.0 / totalItems,
                    StatusText = $"Error installing {item.DisplayName}",
                    DetailedMessage = ex.Message,
                    LogLevel = Winhance.Core.Features.Common.Enums.LogLevel.Error
                });

                throw new InstallationException($"Error installing {item.DisplayName}: {ex.Message}", ex);
            }
        }

        _logService.LogInformation($"Batch installation completed. {completedItems} of {totalItems} items installed successfully.");
    }

    /// <summary>
    /// Removes multiple items in batch.
    /// </summary>
    /// <param name="items">The items to remove.</param>
    /// <returns>A list of results indicating success or failure for each item.</returns>
    public async Task<List<(string Name, bool Success, string? Error)>> RemoveBatchAsync(
        IEnumerable<IInstallableItem> items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        var itemsList = new List<IInstallableItem>(items);
        _logService.LogInformation($"Removing {itemsList.Count} items in batch");

        var results = new List<(string Name, bool Success, string? Error)>();

        foreach (var item in itemsList)
        {
            try
            {
                await RemoveAsync(item);
                results.Add((item.DisplayName, true, null));
            }
            catch (Exception ex) when (ex is not RemovalException)
            {
                _logService.LogError($"Error removing {item.DisplayName}", ex);
                results.Add((item.DisplayName, false, new RemovalException(item.DisplayName, ex.Message, true, ex).Message));
            }
        }

        int successCount = results.Count(r => r.Success);
        _logService.LogInformation($"Batch removal completed. {successCount} of {results.Count} items removed successfully.");

        return results;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services
{
    /// <summary>
    /// Implementation of IBloatRemovalCoordinatorService that coordinates the removal of Windows apps, capabilities, and features.
    /// </summary>
    public class BloatRemovalCoordinatorService : IBloatRemovalCoordinatorService
    {
        private readonly ILogService _logService;
        private readonly IBloatRemovalScriptService _bloatRemovalScriptService;
        private readonly IAppRemovalService _appRemovalService;
        private readonly ICapabilityRemovalService _capabilityRemovalService;
        private readonly IFeatureRemovalService _featureRemovalService;

        /// <summary>
        /// Initializes a new instance of the <see cref="BloatRemovalCoordinatorService"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        /// <param name="bloatRemovalScriptService">The bloat removal script service.</param>
        /// <param name="appRemovalService">The app removal service.</param>
        /// <param name="capabilityRemovalService">The capability removal service.</param>
        /// <param name="featureRemovalService">The feature removal service.</param>
        public BloatRemovalCoordinatorService(
            ILogService logService,
            IBloatRemovalScriptService bloatRemovalScriptService,
            IAppRemovalService appRemovalService,
            ICapabilityRemovalService capabilityRemovalService,
            IFeatureRemovalService featureRemovalService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _bloatRemovalScriptService = bloatRemovalScriptService ?? throw new ArgumentNullException(nameof(bloatRemovalScriptService));
            _appRemovalService = appRemovalService ?? throw new ArgumentNullException(nameof(appRemovalService));
            _capabilityRemovalService = capabilityRemovalService ?? throw new ArgumentNullException(nameof(capabilityRemovalService));
            _featureRemovalService = featureRemovalService ?? throw new ArgumentNullException(nameof(featureRemovalService));
        }

        /// <inheritdoc/>
        public async Task<OperationResult<bool>> AddAppsToScriptAsync(
            List<AppInfo> apps,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (apps == null || !apps.Any())
            {
                _logService.LogWarning("No apps provided to add to the BloatRemoval script");
                return OperationResult<bool>.Succeeded(true);
            }

            try
            {
                _logService.LogInformation($"Adding {apps.Count} apps to BloatRemoval script");
                progress?.Report(new TaskProgressDetail { 
                    Progress = 0, 
                    StatusText = $"Adding {apps.Count} apps to BloatRemoval script..." 
                });

                // Add apps to the script
                await _bloatRemovalScriptService.AddAppsToScriptAsync(apps, progress, cancellationToken);
                
                progress?.Report(new TaskProgressDetail { 
                    Progress = 100, 
                    StatusText = $"Successfully added {apps.Count} apps to BloatRemoval script",
                    LogLevel = LogLevel.Success
                });
                
                return OperationResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error adding apps to BloatRemoval script: {ex.Message}", ex);
                progress?.Report(new TaskProgressDetail { 
                    Progress = 0, 
                    StatusText = $"Error adding apps to BloatRemoval script: {ex.Message}",
                    LogLevel = LogLevel.Error
                });
                return OperationResult<bool>.Failed($"Error adding apps to BloatRemoval script: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<OperationResult<bool>> AddCapabilitiesToScriptAsync(
            List<CapabilityInfo> capabilities,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (capabilities == null || !capabilities.Any())
            {
                _logService.LogWarning("No capabilities provided to add to the BloatRemoval script");
                return OperationResult<bool>.Succeeded(true);
            }

            try
            {
                _logService.LogInformation($"Adding {capabilities.Count} capabilities to BloatRemoval script");
                progress?.Report(new TaskProgressDetail { 
                    Progress = 0, 
                    StatusText = $"Adding {capabilities.Count} capabilities to BloatRemoval script..." 
                });

                // Add capabilities to the script
                await _bloatRemovalScriptService.AddCapabilitiesToScriptAsync(capabilities, progress, cancellationToken);
                
                progress?.Report(new TaskProgressDetail { 
                    Progress = 100, 
                    StatusText = $"Successfully added {capabilities.Count} capabilities to BloatRemoval script",
                    LogLevel = LogLevel.Success
                });
                
                return OperationResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error adding capabilities to BloatRemoval script: {ex.Message}", ex);
                progress?.Report(new TaskProgressDetail { 
                    Progress = 0, 
                    StatusText = $"Error adding capabilities to BloatRemoval script: {ex.Message}",
                    LogLevel = LogLevel.Error
                });
                return OperationResult<bool>.Failed($"Error adding capabilities to BloatRemoval script: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<OperationResult<bool>> AddFeaturesToScriptAsync(
            List<FeatureInfo> features,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (features == null || !features.Any())
            {
                _logService.LogWarning("No features provided to add to the BloatRemoval script");
                return OperationResult<bool>.Succeeded(true);
            }

            try
            {
                _logService.LogInformation($"Adding {features.Count} features to BloatRemoval script");
                progress?.Report(new TaskProgressDetail { 
                    Progress = 0, 
                    StatusText = $"Adding {features.Count} features to BloatRemoval script..." 
                });

                // Add features to the script
                await _bloatRemovalScriptService.AddFeaturesToScriptAsync(features, progress, cancellationToken);
                
                progress?.Report(new TaskProgressDetail { 
                    Progress = 100, 
                    StatusText = $"Successfully added {features.Count} features to BloatRemoval script",
                    LogLevel = LogLevel.Success
                });
                
                return OperationResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error adding features to BloatRemoval script: {ex.Message}", ex);
                progress?.Report(new TaskProgressDetail { 
                    Progress = 0, 
                    StatusText = $"Error adding features to BloatRemoval script: {ex.Message}",
                    LogLevel = LogLevel.Error
                });
                return OperationResult<bool>.Failed($"Error adding features to BloatRemoval script: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<OperationResult<bool>> ExecuteScriptAsync(
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logService.LogInformation("Executing BloatRemoval script");
                progress?.Report(new TaskProgressDetail { 
                    Progress = 0, 
                    StatusText = "Executing BloatRemoval script..." 
                });

                // Execute the script
                var result = await _bloatRemovalScriptService.ExecuteScriptAsync(progress, cancellationToken);
                
                if (result.Success)
                {
                    _logService.LogSuccess("BloatRemoval script executed successfully");
                    progress?.Report(new TaskProgressDetail { 
                        Progress = 100, 
                        StatusText = "BloatRemoval script executed successfully",
                        LogLevel = LogLevel.Success
                    });
                }
                else
                {
                    _logService.LogError($"BloatRemoval script execution failed: {result.ErrorMessage}");
                    progress?.Report(new TaskProgressDetail { 
                        Progress = 100, 
                        StatusText = $"BloatRemoval script execution failed: {result.ErrorMessage}",
                        LogLevel = LogLevel.Error
                    });
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error executing BloatRemoval script: {ex.Message}", ex);
                progress?.Report(new TaskProgressDetail { 
                    Progress = 0, 
                    StatusText = $"Error executing BloatRemoval script: {ex.Message}",
                    LogLevel = LogLevel.Error
                });
                return OperationResult<bool>.Failed($"Error executing BloatRemoval script: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<OperationResult<bool>> RemoveItemsAsync(
            List<AppInfo>? apps = null,
            List<CapabilityInfo>? capabilities = null,
            List<FeatureInfo>? features = null,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                int totalItems = (apps?.Count ?? 0) + (capabilities?.Count ?? 0) + (features?.Count ?? 0);
                
                if (totalItems == 0)
                {
                    _logService.LogWarning("No items provided to remove");
                    return OperationResult<bool>.Succeeded(true);
                }
                
                _logService.LogInformation($"Removing {totalItems} items ({apps?.Count ?? 0} apps, {capabilities?.Count ?? 0} capabilities, {features?.Count ?? 0} features)");
                progress?.Report(new TaskProgressDetail { 
                    Progress = 0, 
                    StatusText = $"Preparing to remove {totalItems} items..." 
                });

                // Track progress for each phase
                int currentProgress = 0;
                const int APPS_PHASE_WEIGHT = 30;
                const int CAPABILITIES_PHASE_WEIGHT = 30;
                const int FEATURES_PHASE_WEIGHT = 30;
                const int EXECUTION_PHASE_WEIGHT = 10;
                
                // Create a progress transformer for each phase
                IProgress<TaskProgressDetail>? appsProgress = null;
                if (progress != null)
                {
                    appsProgress = new Progress<TaskProgressDetail>(detail => {
                        var transformedDetail = detail;
                        transformedDetail.Progress = currentProgress + (detail.Progress * APPS_PHASE_WEIGHT / 100);
                        progress.Report(transformedDetail);
                    });
                }
                
                // Add apps to the script if any
                if (apps != null && apps.Any())
                {
                    var appsResult = await AddAppsToScriptAsync(apps, appsProgress, cancellationToken);
                    if (!appsResult.Success)
                    {
                        return appsResult;
                    }
                }
                
                // Update progress
                currentProgress += APPS_PHASE_WEIGHT;
                progress?.Report(new TaskProgressDetail { 
                    Progress = currentProgress, 
                    StatusText = "Apps added to script" 
                });
                
                // Create progress transformer for capabilities phase
                IProgress<TaskProgressDetail>? capabilitiesProgress = null;
                if (progress != null)
                {
                    capabilitiesProgress = new Progress<TaskProgressDetail>(detail => {
                        var transformedDetail = detail;
                        transformedDetail.Progress = currentProgress + (detail.Progress * CAPABILITIES_PHASE_WEIGHT / 100);
                        progress.Report(transformedDetail);
                    });
                }
                
                // Add capabilities to the script if any
                if (capabilities != null && capabilities.Any())
                {
                    var capabilitiesResult = await AddCapabilitiesToScriptAsync(capabilities, capabilitiesProgress, cancellationToken);
                    if (!capabilitiesResult.Success)
                    {
                        return capabilitiesResult;
                    }
                }
                
                // Update progress
                currentProgress += CAPABILITIES_PHASE_WEIGHT;
                progress?.Report(new TaskProgressDetail { 
                    Progress = currentProgress, 
                    StatusText = "Capabilities added to script" 
                });
                
                // Create progress transformer for features phase
                IProgress<TaskProgressDetail>? featuresProgress = null;
                if (progress != null)
                {
                    featuresProgress = new Progress<TaskProgressDetail>(detail => {
                        var transformedDetail = detail;
                        transformedDetail.Progress = currentProgress + (detail.Progress * FEATURES_PHASE_WEIGHT / 100);
                        progress.Report(transformedDetail);
                    });
                }
                
                // Add features to the script if any
                if (features != null && features.Any())
                {
                    var featuresResult = await AddFeaturesToScriptAsync(features, featuresProgress, cancellationToken);
                    if (!featuresResult.Success)
                    {
                        return featuresResult;
                    }
                }
                
                // Update progress
                currentProgress += FEATURES_PHASE_WEIGHT;
                progress?.Report(new TaskProgressDetail { 
                    Progress = currentProgress, 
                    StatusText = "Features added to script" 
                });
                
                // Create progress transformer for execution phase
                IProgress<TaskProgressDetail>? executionProgress = null;
                if (progress != null)
                {
                    executionProgress = new Progress<TaskProgressDetail>(detail => {
                        var transformedDetail = detail;
                        transformedDetail.Progress = currentProgress + (detail.Progress * EXECUTION_PHASE_WEIGHT / 100);
                        progress.Report(transformedDetail);
                    });
                }
                
                // Execute the script
                var executionResult = await ExecuteScriptAsync(executionProgress, cancellationToken);
                
                // Return the result of the execution
                return executionResult;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error removing items: {ex.Message}", ex);
                progress?.Report(new TaskProgressDetail { 
                    Progress = 0, 
                    StatusText = $"Error removing items: {ex.Message}",
                    LogLevel = LogLevel.Error
                });
                return OperationResult<bool>.Failed($"Error removing items: {ex.Message}", ex);
            }
        }
    }
}

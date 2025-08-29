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
    /// Service for removing Windows optional features from the system.
    /// </summary>
    public class FeatureRemovalService : IFeatureRemovalService
    {
        private readonly ILogService _logService;
        private readonly IAppDiscoveryService _appDiscoveryService;
        private readonly IBloatRemovalScriptService _bloatRemovalScriptService;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeatureRemovalService"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        /// <param name="appDiscoveryService">The app discovery service.</param>
        /// <param name="bloatRemovalScriptService">The bloat removal script service.</param>
        public FeatureRemovalService(
            ILogService logService,
            IAppDiscoveryService appDiscoveryService,
            IBloatRemovalScriptService bloatRemovalScriptService)
        {
            _logService = logService;
            _appDiscoveryService = appDiscoveryService;
            _bloatRemovalScriptService = bloatRemovalScriptService;
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveFeatureAsync(
            FeatureInfo featureInfo,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (featureInfo == null)
            {
                throw new ArgumentNullException(nameof(featureInfo));
            }

            try
            {
                _logService.LogInformation($"Adding feature {featureInfo.Name} to BloatRemoval script");
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = $"Adding feature {featureInfo.Name} to BloatRemoval script..."
                });

                // Add the feature to the BloatRemoval script
                await _bloatRemovalScriptService.AddFeaturesToScriptAsync(
                    new List<FeatureInfo> { featureInfo },
                    progress,
                    cancellationToken);

                _logService.LogSuccess($"Successfully added feature {featureInfo.Name} to BloatRemoval script");
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 100,
                    StatusText = $"Successfully added feature {featureInfo.Name} to BloatRemoval script",
                    LogLevel = LogLevel.Success
                });

                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error adding feature {featureInfo.Name} to BloatRemoval script", ex);
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = $"Error adding feature {featureInfo.Name} to BloatRemoval script: {ex.Message}",
                    LogLevel = LogLevel.Error
                });
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveFeatureAsync(
            string featureName,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Create a basic FeatureInfo object from the name
                var featureInfo = new FeatureInfo
                {
                    PackageName = featureName,
                    Name = featureName
                    // ItemType is already set to InstallItemType.Feature by default
                };

                // Call the other overload with the created object
                return await RemoveFeatureAsync(featureInfo, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error removing feature: {featureName}", ex);
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = $"Error removing {featureName}: {ex.Message}",
                    LogLevel = LogLevel.Error
                });
                return false;
            }
        }

        /// <inheritdoc/>
        public Task<bool> CanRemoveFeatureAsync(FeatureInfo featureInfo)
        {
            // Basic implementation: Assume all found features can be removed.
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public async Task<List<(string Name, bool Success, string? Error)>> RemoveFeaturesInBatchAsync(
            List<FeatureInfo> features)
        {
            if (features == null)
            {
                throw new ArgumentNullException(nameof(features));
            }

            var results = new List<(string Name, bool Success, string? Error)>();

            try
            {
                _logService.LogInformation($"Adding {features.Count} features to BloatRemoval script");

                // Add all features to the script at once
                await _bloatRemovalScriptService.AddFeaturesToScriptAsync(features);

                // Mark all as successful
                foreach (var feature in features)
                {
                    results.Add((feature.PackageName, true, null));
                }

                return results;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error adding features to BloatRemoval script: {ex.Message}", ex);

                // Mark all as failed
                foreach (var feature in features)
                {
                    results.Add((feature.PackageName, false, ex.Message));
                }

                return results;
            }
        }

        /// <inheritdoc/>
        public async Task<List<(string Name, bool Success, string? Error)>> RemoveFeaturesInBatchAsync(
            List<string> featureNames)
        {
            if (featureNames == null)
            {
                throw new ArgumentNullException(nameof(featureNames));
            }

            // Convert string names to FeatureInfo objects
            var features = featureNames.Select(name => new FeatureInfo
            {
                PackageName = name,
                Name = name
                // ItemType is already set to InstallItemType.Feature by default
            }).ToList();

            // Call the other overload
            return await RemoveFeaturesInBatchAsync(features);
        }
    }
}

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
    /// Service for removing Windows capabilities from the system.
    /// </summary>
    public class CapabilityRemovalService : ICapabilityRemovalService
    {
        private readonly ILogService _logService;
        private readonly IAppDiscoveryService _appDiscoveryService;
        private readonly IBloatRemovalScriptService _bloatRemovalScriptService;

        /// <summary>
        /// Initializes a new instance of the <see cref="CapabilityRemovalService"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        /// <param name="appDiscoveryService">The app discovery service.</param>
        /// <param name="bloatRemovalScriptService">The bloat removal script service.</param>
        public CapabilityRemovalService(
            ILogService logService,
            IAppDiscoveryService appDiscoveryService,
            IBloatRemovalScriptService bloatRemovalScriptService)
        {
            _logService = logService;
            _appDiscoveryService = appDiscoveryService;
            _bloatRemovalScriptService = bloatRemovalScriptService;
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveCapabilityAsync(
            CapabilityInfo capabilityInfo,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (capabilityInfo == null)
            {
                throw new ArgumentNullException(nameof(capabilityInfo));
            }
            
            try
            {
                _logService.LogInformation($"Adding capability {capabilityInfo.Name} to BloatRemoval script");
                progress?.Report(new TaskProgressDetail { 
                    Progress = 0, 
                    StatusText = $"Adding capability {capabilityInfo.Name} to BloatRemoval script..." 
                });

                // Add the capability to the BloatRemoval script
                await _bloatRemovalScriptService.AddCapabilitiesToScriptAsync(
                    new List<CapabilityInfo> { capabilityInfo },
                    progress,
                    cancellationToken);

                _logService.LogSuccess($"Successfully added capability {capabilityInfo.Name} to BloatRemoval script");
                progress?.Report(new TaskProgressDetail { 
                    Progress = 100, 
                    StatusText = $"Successfully added capability {capabilityInfo.Name} to BloatRemoval script",
                    LogLevel = LogLevel.Success
                });

                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error adding capability {capabilityInfo.Name} to BloatRemoval script", ex);
                progress?.Report(new TaskProgressDetail { 
                    Progress = 0, 
                    StatusText = $"Error adding capability {capabilityInfo.Name} to BloatRemoval script: {ex.Message}",
                    LogLevel = LogLevel.Error
                });
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> RemoveCapabilityAsync(
            string capabilityName,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Create a basic CapabilityInfo object from the name
                var capabilityInfo = new CapabilityInfo
                {
                    PackageName = capabilityName,
                    Name = capabilityName
                    // ItemType is already set to InstallItemType.Capability by default
                };

                // Call the other overload with the created object
                return await RemoveCapabilityAsync(capabilityInfo, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error removing capability: {capabilityName}", ex);
                progress?.Report(new TaskProgressDetail {
                    Progress = 0,
                    StatusText = $"Error removing {capabilityName}: {ex.Message}",
                    LogLevel = LogLevel.Error
                });
                return false;
            }
        }

        /// <inheritdoc/>
        public Task<bool> CanRemoveCapabilityAsync(CapabilityInfo capabilityInfo)
        {
            // Basic implementation: Assume all found capabilities can be removed.
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public async Task<List<(string Name, bool Success, string? Error)>> RemoveCapabilitiesInBatchAsync(
            List<CapabilityInfo> capabilities)
        {
            if (capabilities == null)
            {
                throw new ArgumentNullException(nameof(capabilities));
            }

            var results = new List<(string Name, bool Success, string? Error)>();

            try
            {
                _logService.LogInformation($"Adding {capabilities.Count} capabilities to BloatRemoval script");

                // Add all capabilities to the script at once
                await _bloatRemovalScriptService.AddCapabilitiesToScriptAsync(capabilities);

                // Mark all as successful
                foreach (var capability in capabilities)
                {
                    results.Add((capability.PackageName, true, null));
                }

                return results;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error adding capabilities to BloatRemoval script: {ex.Message}", ex);
                
                // Mark all as failed
                foreach (var capability in capabilities)
                {
                    results.Add((capability.PackageName, false, ex.Message));
                }

                return results;
            }
        }

        /// <inheritdoc/>
        public async Task<List<(string Name, bool Success, string? Error)>> RemoveCapabilitiesInBatchAsync(
            List<string> capabilityNames)
        {
            if (capabilityNames == null)
            {
                throw new ArgumentNullException(nameof(capabilityNames));
            }

            // Convert string names to CapabilityInfo objects
            var capabilities = capabilityNames.Select(name => new CapabilityInfo
            {
                PackageName = name,
                Name = name
                // ItemType is already set to InstallItemType.Capability by default
            }).ToList();

            // Call the other overload
            return await RemoveCapabilitiesInBatchAsync(capabilities);
        }
    }
}

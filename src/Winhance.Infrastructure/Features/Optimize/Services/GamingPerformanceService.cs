using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.Optimize.Services
{
    /// <summary>
    /// Service implementation for managing gaming and performance optimization settings.
    /// Handles game mode, performance tweaks, and gaming-related optimizations.
    /// Extends BaseSystemSettingsService to inherit common setting application logic.
    /// </summary>
    public class GamingPerformanceService : BaseSystemSettingsService, IGamingPerformanceService
    {
        /// <summary>
        /// Gets the domain name for gaming and performance optimizations.
        /// </summary>
        public override string DomainName => "GamingPerformance";

        /// <summary>
        /// Initializes a new instance of the <see cref="GamingPerformanceService"/> class.
        /// </summary>
        /// <param name="registryService">The registry service for registry manipulations.</param>
        /// <param name="commandService">The command service for command-based settings.</param>
        /// <param name="logService">The log service for logging operations.</param>
        /// <param name="systemSettingsDiscoveryService">The system settings discovery service.</param>
        public GamingPerformanceService(
            IRegistryService registryService,
            ICommandService commandService,
            ILogService logService,
            ISystemSettingsDiscoveryService systemSettingsDiscoveryService)
            : base(registryService, commandService, logService, systemSettingsDiscoveryService)
        {
        }

        /// <summary>
        /// Gets all gaming and performance optimization settings with their current system state.
        /// </summary>
        /// <returns>Collection of application settings for gaming and performance.</returns>
        public override async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            var optimizations = GamingandPerformanceOptimizations.GetGamingandPerformanceOptimizations();
            return await GetSettingsWithSystemStateAsync(optimizations.Settings);
        }

        // All other methods (ApplySettingAsync, GetSettingStatusAsync, GetSettingValueAsync, IsSettingEnabledAsync)
        // are inherited from BaseSystemSettingsService and work automatically with the settings from GetSettingsAsync()
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.Optimize.Services
{
    /// <summary>
    /// Service implementation for managing notification optimization settings.
    /// Handles notification policies, focus assist, and notification-related settings.
    /// </summary>
    public class NotificationService : BaseSystemSettingsService, INotificationService
    {
        /// <summary>
        /// Gets the domain name for Notification optimizations.
        /// </summary>
        public override string DomainName => "Notification";

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationService"/> class.
        /// </summary>
        public NotificationService(
            IRegistryService registryService,
            ICommandService commandService,
            ILogService logService,
            ISystemSettingsDiscoveryService systemSettingsDiscoveryService)
            : base(registryService, commandService, logService, systemSettingsDiscoveryService)
        {
        }

        /// <summary>
        /// Gets all Notification optimization settings with their current system state.
        /// </summary>
        public override async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            var optimizations = NotificationOptimizations.GetNotificationOptimizations();
            return await GetSettingsWithSystemStateAsync(optimizations.Settings);
        }

        // All other methods (ApplySettingAsync, GetSettingStatusAsync, GetSettingValueAsync, IsSettingEnabledAsync)
        // are inherited from BaseSystemSettingsService and work automatically with the settings from GetSettingsAsync()
    }
}

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
    /// Service implementation for managing sound optimization settings.
    /// Handles audio enhancements, sound schemes, and audio-related optimizations.
    /// </summary>
    public class SoundService : BaseSystemSettingsService, ISoundService
    {
        /// <summary>
        /// Gets the domain name for Sound optimizations.
        /// </summary>
        public override string DomainName => "Sound";

        /// <summary>
        /// Initializes a new instance of the <see cref="SoundService"/> class.
        /// </summary>
        public SoundService(
            IRegistryService registryService,
            ICommandService commandService,
            ILogService logService,
            ISystemSettingsDiscoveryService systemSettingsDiscoveryService)
            : base(registryService, commandService, logService, systemSettingsDiscoveryService)
        {
        }

        /// <summary>
        /// Gets all Sound optimization settings with their current system state.
        /// </summary>
        public override async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            var optimizations = SoundOptimizations.GetSoundOptimizations();
            return await GetSettingsWithSystemStateAsync(optimizations.Settings);
        }

        // All other methods (ApplySettingAsync, GetSettingStatusAsync, GetSettingValueAsync, IsSettingEnabledAsync)
        // are inherited from BaseSystemSettingsService and work automatically with the settings from GetSettingsAsync()




    }
}

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
    /// Service implementation for managing Windows security optimization settings.
    /// Handles UAC, Windows Defender, and security-related optimizations.
    /// </summary>
    public class SecurityService : BaseSystemSettingsService, ISecurityService
    {
        private readonly IComboBoxDiscoveryService _comboBoxDiscoveryService;

        /// <summary>
        /// Gets the domain name for Security optimizations.
        /// </summary>
        public override string DomainName => "Security";

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityService"/> class.
        /// </summary>
        public SecurityService(
            IRegistryService registryService,
            ICommandService commandService,
            IComboBoxDiscoveryService comboBoxDiscoveryService,
            ILogService logService,
            ISystemSettingsDiscoveryService systemSettingsDiscoveryService)
            : base(registryService, commandService, logService, systemSettingsDiscoveryService)
        {
            _comboBoxDiscoveryService = comboBoxDiscoveryService ?? throw new ArgumentNullException(nameof(comboBoxDiscoveryService));
        }

        /// <summary>
        /// Gets all Security optimization settings with their current system state.
        /// </summary>
        public override async Task<IEnumerable<ApplicationSetting>> GetSettingsAsync()
        {
            var optimizations = WindowsSecurityOptimizations.GetWindowsSecurityOptimizations();
            return await GetSettingsWithSystemStateAsync(optimizations.Settings);
        }
    }
}

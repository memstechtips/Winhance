using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Customize.Models;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.Customize.Services
{
    public class ExplorerCustomizationService(
        ILogService logService,
        ICompatibleSettingsRegistry compatibleSettingsRegistry) : IDomainService
        
    {
        private IEnumerable<SettingDefinition>? _cachedSettings;
        private readonly object _cacheLock = new object();

        public string DomainName => FeatureIds.ExplorerCustomization;

        public async Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
        {
            if (_cachedSettings != null)
                return _cachedSettings;

            lock (_cacheLock)
            {
                if (_cachedSettings != null)
                    return _cachedSettings;

                try
                {
                    logService.Log(LogLevel.Info, "Loading Explorer Customizations settings");

                    _cachedSettings = compatibleSettingsRegistry.GetFilteredSettings(FeatureIds.ExplorerCustomization);
                    return _cachedSettings;
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Error, $"Error loading Explorer Customizations settings: {ex.Message}");
                    return Enumerable.Empty<SettingDefinition>();
                }
            }
        }

        public void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cachedSettings = null;
                logService.Log(LogLevel.Debug, "Explorer Customizations settings cache cleared");
            }
        }
    }
}

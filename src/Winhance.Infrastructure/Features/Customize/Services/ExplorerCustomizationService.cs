using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        ILogService logService) : IDomainService
    {
        public string DomainName => FeatureIds.ExplorerCustomization;

        public async Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
        {
            try
            {
                logService.Log(LogLevel.Info, "Loading Explorer customization settings");

                var group = ExplorerCustomizations.GetExplorerCustomizations();
                return group.Settings;
            }
            catch (Exception ex)
            {
                logService.Log(
                    LogLevel.Error,
                    $"Error loading Explorer customization settings: {ex.Message}"
                );
                return Enumerable.Empty<SettingDefinition>();
            }
        }

    }
}

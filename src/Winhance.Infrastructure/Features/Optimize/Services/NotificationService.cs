using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.Optimize.Services
{
    public class NotificationService(
        ILogService logService,
        ICompatibleSettingsRegistry compatibleSettingsRegistry) : IDomainService
    {
        public string DomainName => FeatureIds.Notification;

        public async Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
        {
            try
            {
                return compatibleSettingsRegistry.GetFilteredSettings(FeatureIds.Notification);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error loading Notification settings: {ex.Message}");
                return Enumerable.Empty<SettingDefinition>();
            }
        }

    }
}

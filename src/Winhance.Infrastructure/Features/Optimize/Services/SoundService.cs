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

namespace Winhance.Infrastructure.Features.Optimize.Services;

public class SoundService(
    ILogService logService,
    ICompatibleSettingsRegistry compatibleSettingsRegistry) : IDomainService
{
    public string DomainName => FeatureIds.Sound;

    public Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
    {
        try
        {
            return Task.FromResult(compatibleSettingsRegistry.GetFilteredSettings(FeatureIds.Sound));
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error loading Sound settings: {ex.Message}");
            return Task.FromResult(Enumerable.Empty<SettingDefinition>());
        }
    }

}

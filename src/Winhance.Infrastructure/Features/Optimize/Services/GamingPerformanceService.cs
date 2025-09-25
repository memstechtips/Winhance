using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.Optimize.Services
{
    public class GamingPerformanceService(
        ILogService logService) : IDomainService
    {
        public string DomainName => FeatureIds.GamingPerformance;

        public async Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
        {
            try
            {
                var optimizations = GamingandPerformanceOptimizations.GetGamingandPerformanceOptimizations();
                return optimizations.Settings;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error loading Gaming Performance settings: {ex.Message}");
                return Enumerable.Empty<SettingDefinition>();
            }
        }
    }
}

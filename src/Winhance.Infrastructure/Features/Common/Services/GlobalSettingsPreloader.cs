using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class GlobalSettingsPreloader : IGlobalSettingsPreloader
    {
        private readonly IEnumerable<IDomainService> _domainServices;
        private readonly IGlobalSettingsRegistry _globalSettingsRegistry;
        private readonly IDomainServiceRouter _domainServiceRouter;
        private readonly ILogService _logService;
        private bool _isPreloaded;

        public bool IsPreloaded => _isPreloaded;

        public GlobalSettingsPreloader(
            IEnumerable<IDomainService> domainServices,
            IGlobalSettingsRegistry globalSettingsRegistry,
            IDomainServiceRouter domainServiceRouter,
            ILogService logService)
        {
            _domainServices = domainServices;
            _globalSettingsRegistry = globalSettingsRegistry;
            _domainServiceRouter = domainServiceRouter;
            _logService = logService;
        }

        public async Task PreloadAllSettingsAsync()
        {
            if (_isPreloaded)
            {
                _logService.Log(LogLevel.Debug, "[Preloader] Settings already preloaded, skipping");
                return;
            }

            _logService.Log(LogLevel.Info, "[Preloader] Starting global settings preload");

            foreach (var domainService in _domainServices)
            {
                try
                {
                    var settings = await domainService.GetSettingsAsync();
                    var settingsList = settings.ToList();

                    _globalSettingsRegistry.RegisterSettings(domainService.DomainName, settingsList);
                    _domainServiceRouter.AddSettingMappings(domainService.DomainName, settingsList.Select(s => s.Id));

                    _logService.Log(LogLevel.Debug, $"[Preloader] Registered {settingsList.Count} settings from {domainService.DomainName}");
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Warning, $"[Preloader] Failed to preload settings from {domainService.DomainName}: {ex.Message}");
                }
            }

            _isPreloaded = true;
            _logService.Log(LogLevel.Info, "[Preloader] Global settings preload completed");
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class GlobalSettingsPreloader : IGlobalSettingsPreloader
    {
        private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
        private readonly IGlobalSettingsRegistry _globalSettingsRegistry;
        private readonly IDomainServiceRouter _domainServiceRouter;
        private readonly ILogService _logService;
        private bool _isPreloaded;

        public bool IsPreloaded => _isPreloaded;

        public GlobalSettingsPreloader(
            ICompatibleSettingsRegistry compatibleSettingsRegistry,
            IGlobalSettingsRegistry globalSettingsRegistry,
            IDomainServiceRouter domainServiceRouter,
            ILogService logService)
        {
            _compatibleSettingsRegistry = compatibleSettingsRegistry;
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

            var allBypassedSettings = _compatibleSettingsRegistry.GetAllBypassedSettings();

            foreach (var (featureId, settings) in allBypassedSettings)
            {
                try
                {
                    var settingsList = settings.ToList();

                    _globalSettingsRegistry.RegisterSettings(featureId, settingsList);
                    _domainServiceRouter.AddSettingMappings(featureId, settingsList.Select(s => s.Id));

                    _logService.Log(LogLevel.Debug, $"[Preloader] Registered {settingsList.Count} bypassed settings from {featureId}");
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Warning, $"[Preloader] Failed to preload settings from {featureId}: {ex.Message}");
                }
            }

            _isPreloaded = true;
            _logService.Log(LogLevel.Info, "[Preloader] Global settings preload completed");
        }
    }
}

using System;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class DomainServiceRouter(
        IEnumerable<IDomainService> domainServices,
        ILogService logService) : IDomainServiceRouter
    {
        private readonly Dictionary<string, IDomainService> _serviceMap = InitializeServiceMap(domainServices);
        private readonly Dictionary<string, string> _settingToFeatureMap = new();

        private static Dictionary<string, IDomainService> InitializeServiceMap(IEnumerable<IDomainService> domainServices)
        {
            var serviceMap = new Dictionary<string, IDomainService>();
            foreach (var service in domainServices)
            {
                serviceMap[service.DomainName] = service;
            }
            return serviceMap;
        }

        public void AddSettingMappings(string featureId, IEnumerable<string> settingIds)
        {
            foreach (var settingId in settingIds)
            {
                _settingToFeatureMap[settingId] = featureId;
            }
        }

        public IDomainService GetDomainService(string featureIdOrSettingId)
        {
            if (_serviceMap.TryGetValue(featureIdOrSettingId, out var directService))
                return directService;

            if (_settingToFeatureMap.TryGetValue(featureIdOrSettingId, out var featureId)
                && _serviceMap.TryGetValue(featureId, out var service))
                return service;

            throw new ArgumentException($"No domain service found for '{featureIdOrSettingId}'");
        }

        public void ClearAllSettingsCaches()
        {
            logService.Log(Core.Features.Common.Enums.LogLevel.Info, "Clearing all domain service settings caches");

            foreach (var service in _serviceMap.Values)
            {
                service.ClearSettingsCache();
            }
        }
    }
}

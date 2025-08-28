using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Features;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class DomainServiceRouter : IDomainServiceRouter
    {
        private readonly Dictionary<string, IDomainService> _serviceMap;
        private readonly Dictionary<string, string> _settingToFeatureMap;
        private readonly ILogService _logService;

        public DomainServiceRouter(
            IEnumerable<IDomainService> domainServices,
            ILogService logService
        )
        {
            _logService = logService;
            _serviceMap = new Dictionary<string, IDomainService>();
            _settingToFeatureMap = new Dictionary<string, string>();

            foreach (var service in domainServices)
            {
                _serviceMap[service.DomainName] = service;
            }
        }

        public void AddSettingMappings(string featureId, IEnumerable<string> settingIds)
        {
            foreach (var settingId in settingIds)
            {
                _settingToFeatureMap[settingId] = featureId;
            }
        }

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null) =>
            await GetDomainService(settingId).ApplySettingAsync(settingId, enable, value);

        public async Task<bool> IsSettingEnabledAsync(string settingId) =>
            await GetDomainService(settingId).IsSettingEnabledAsync(settingId);

        public async Task<object?> GetSettingValueAsync(string settingId) =>
            await GetDomainService(settingId).GetSettingValueAsync(settingId);

        public async Task<Dictionary<string, bool>> GetMultipleSettingsStateAsync(
            IEnumerable<string> settingIds
        )
        {
            var results = new Dictionary<string, bool>();
            foreach (var settingId in settingIds)
            {
                try
                {
                    results[settingId] = await IsSettingEnabledAsync(settingId);
                }
                catch
                {
                    results[settingId] = false;
                }
            }
            return results;
        }

        public async Task<Dictionary<string, object?>> GetMultipleSettingsValuesAsync(
            IEnumerable<string> settingIds
        )
        {
            var results = new Dictionary<string, object?>();
            foreach (var settingId in settingIds)
            {
                try
                {
                    results[settingId] = await GetSettingValueAsync(settingId);
                }
                catch
                {
                    results[settingId] = null;
                }
            }
            return results;
        }

        public IDomainService GetDomainService(string featureIdOrSettingId)
        {
            if (_serviceMap.TryGetValue(featureIdOrSettingId, out var directService))
                return directService;

            if (
                _settingToFeatureMap.TryGetValue(featureIdOrSettingId, out var featureId)
                && _serviceMap.TryGetValue(featureId, out var service)
            )
                return service;

            throw new ArgumentException($"No domain service found for '{featureIdOrSettingId}'");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Service locator implementation for finding domain services by setting ID.
    /// Follows SRP by handling only service location logic.
    /// Follows DIP by depending on IDomainService abstractions.
    /// </summary>
    public class DomainServiceLocator : IDomainServiceLocator
    {
        private readonly IEnumerable<IDomainService> _domainServices;
        private readonly ILogService _logService;

        public DomainServiceLocator(
            IEnumerable<IDomainService> domainServices,
            ILogService logService)
        {
            _domainServices = domainServices ?? throw new ArgumentNullException(nameof(domainServices));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public async Task<IDomainService?> FindServiceForSettingAsync(string settingId)
        {
            try
            {
                _logService.Log(LogLevel.Debug, $"[DomainServiceLocator] Finding service for setting '{settingId}'");
                _logService.Log(LogLevel.Debug, $"[DomainServiceLocator] Available domain services: {_domainServices.Count()}");

                foreach (var service in _domainServices)
                {
                    _logService.Log(LogLevel.Debug, $"[DomainServiceLocator] Checking domain service: {service.DomainName} ({service.GetType().Name})");
                    
                    var settings = await service.GetSettingsAsync();
                    _logService.Log(LogLevel.Debug, $"[DomainServiceLocator] Domain '{service.DomainName}' has {settings.Count()} settings");
                    
                    foreach (var setting in settings.Take(5)) // Log first 5 settings for debugging
                    {
                        _logService.Log(LogLevel.Debug, $"[DomainServiceLocator] Domain '{service.DomainName}' setting: '{setting.Id}'");
                    }
                    
                    if (settings.Any(s => s.Id == settingId))
                    {
                        _logService.Log(LogLevel.Debug, $"[DomainServiceLocator] Found setting '{settingId}' in domain '{service.DomainName}'");
                        return service;
                    }
                }

                _logService.Log(LogLevel.Warning, $"[DomainServiceLocator] No domain service found for setting '{settingId}'");
                return null;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"[DomainServiceLocator] Error finding service for setting '{settingId}': {ex.Message}");
                return null;
            }
        }

        public async Task<ApplicationSetting?> GetSettingAsync(string settingId)
        {
            try
            {
                var service = await FindServiceForSettingAsync(settingId);
                if (service == null)
                    return null;

                var settings = await service.GetSettingsAsync();
                return settings.FirstOrDefault(s => s.Id == settingId);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"[DomainServiceLocator] Error getting setting '{settingId}': {ex.Message}");
                return null;
            }
        }
    }
}
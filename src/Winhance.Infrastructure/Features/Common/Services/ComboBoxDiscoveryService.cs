using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Infrastructure service that coordinates ComboBox value resolution across domains.
    /// Follows OCP by being extensible through resolver registration.
    /// Follows DIP by depending on IComboBoxValueResolver abstractions.
    /// Follows SRP by handling only ComboBox discovery coordination.
    /// </summary>
    public class ComboBoxDiscoveryService : IComboBoxDiscoveryService
    {
        private readonly IEnumerable<IComboBoxValueResolver> _resolvers;
        private readonly ILogService _logService;

        public ComboBoxDiscoveryService(
            IEnumerable<IComboBoxValueResolver> resolvers,
            ILogService logService)
        {
            _resolvers = resolvers ?? throw new ArgumentNullException(nameof(resolvers));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public async Task<int?> ResolveCurrentIndexAsync(ApplicationSetting setting)
        {
            try
            {
                if (setting.ControlType != ControlType.ComboBox)
                    return null;

                _logService.Log(LogLevel.Debug, $"[ComboBoxDiscovery] Resolving ComboBox value for '{setting.Id}'");

                // Find appropriate resolver for this setting
                var resolver = _resolvers.FirstOrDefault(r => r.CanResolve(setting));
                if (resolver != null)
                {
                    _logService.Log(LogLevel.Debug, $"[ComboBoxDiscovery] Using resolver '{resolver.DomainName}' for '{setting.Id}'");
                    return await resolver.ResolveCurrentIndexAsync(setting);
                }

                _logService.Log(LogLevel.Warning, $"No ComboBox resolver found for setting '{setting.Id}'. Using default LinkedSettings approach.");

                // Fallback to generic LinkedSettings approach
                return await ResolveGenericLinkedSettingsAsync(setting);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error resolving ComboBox value for '{setting.Id}': {ex.Message}");
                return null;
            }
        }

        public async Task ApplyIndexAsync(ApplicationSetting setting, int index)
        {
            try
            {
                if (setting.ControlType != ControlType.ComboBox)
                    return;

                _logService.Log(LogLevel.Debug, $"[ComboBoxDiscovery] Applying ComboBox index {index} for '{setting.Id}'");

                // Find appropriate resolver for this setting
                var resolver = _resolvers.FirstOrDefault(r => r.CanResolve(setting));
                if (resolver != null)
                {
                    _logService.Log(LogLevel.Debug, $"[ComboBoxDiscovery] Using resolver '{resolver.DomainName}' for '{setting.Id}'");
                    await resolver.ApplyIndexAsync(setting, index);
                    return;
                }

                _logService.Log(LogLevel.Warning, $"No ComboBox resolver found for setting '{setting.Id}'. Using default LinkedSettings approach.");

                // Fallback to generic LinkedSettings approach
                await ApplyGenericLinkedSettingsAsync(setting, index);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error applying ComboBox value for '{setting.Id}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Fallback resolver for ComboBox settings using generic LinkedSettings pattern.
        /// This handles settings that don't have domain-specific resolvers.
        /// </summary>
        private async Task<int?> ResolveGenericLinkedSettingsAsync(ApplicationSetting setting)
        {
            // This would implement generic LinkedSettings resolution for future ComboBox settings
            // that don't need domain-specific logic
            _logService.Log(LogLevel.Debug, $"Generic LinkedSettings resolution not yet implemented for '{setting.Id}'");
            return null;
        }

        /// <summary>
        /// Fallback applier for ComboBox settings using generic LinkedSettings pattern.
        /// This handles settings that don't have domain-specific resolvers.
        /// </summary>
        private async Task ApplyGenericLinkedSettingsAsync(ApplicationSetting setting, int index)
        {
            // This would implement generic LinkedSettings application for future ComboBox settings
            // that don't need domain-specific logic
            _logService.Log(LogLevel.Debug, $"Generic LinkedSettings application not yet implemented for '{setting.Id}'");
            await Task.CompletedTask;
        }
    }
}

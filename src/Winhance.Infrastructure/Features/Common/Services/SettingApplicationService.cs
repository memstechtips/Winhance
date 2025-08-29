using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Infrastructure.Features.Customize.Services;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class SettingApplicationService : ISettingApplicationService
    {
        private readonly IDomainServiceRouter _domainServiceRouter;
        private readonly ILogService _logService;
        private readonly IRecommendedSettingsService _recommendedSettingsService;
        private readonly IDependencyManager _dependencyManager;
        private readonly IGlobalSettingsRegistry _globalSettingsRegistry;
        private readonly IEventBus _eventBus;

        public SettingApplicationService(
            IDomainServiceRouter DomainServiceRouter,
            ILogService logService,
            IRecommendedSettingsService recommendedSettingsService,
            IDependencyManager dependencyManager,
            IGlobalSettingsRegistry globalSettingsRegistry,
            IEventBus eventBus
        )
        {
            _domainServiceRouter =
                DomainServiceRouter ?? throw new ArgumentNullException(nameof(DomainServiceRouter));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _recommendedSettingsService =
                recommendedSettingsService
                ?? throw new ArgumentNullException(nameof(recommendedSettingsService));
            _dependencyManager =
                dependencyManager ?? throw new ArgumentNullException(nameof(dependencyManager));
            _globalSettingsRegistry =
                globalSettingsRegistry
                ?? throw new ArgumentNullException(nameof(globalSettingsRegistry));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            await ApplySettingAsync(settingId, enable, value, false);
        }

        public async Task ApplySettingAsync(string settingId, bool enable, object? value, bool applyWallpaper)
        {
            var domainService = _domainServiceRouter.GetDomainService(settingId);
            var rawSettings = await domainService.GetRawSettingsAsync();
            var setting = rawSettings.FirstOrDefault(s => s.Id == settingId);

            if (setting == null)
                throw new ArgumentException(
                    $"Setting '{settingId}' not found in {domainService.DomainName} settings"
                );

            _globalSettingsRegistry.RegisterSetting(domainService.DomainName, setting);

            IEnumerable<SettingDefinition> allSettings = rawSettings;
            if (enable && setting?.Dependencies?.Any() == true)
            {
                allSettings = await domainService.GetSettingsAsync();
                setting = allSettings.FirstOrDefault(s => s.Id == settingId);

                var dependencyResult = await _dependencyManager.HandleSettingEnabledAsync(
                    settingId,
                    allSettings.Cast<ISettingItem>(),
                    this
                );
                if (!dependencyResult)
                    throw new InvalidOperationException(
                        $"Cannot enable '{settingId}' due to unsatisfied dependencies"
                    );
            }

            if (settingId == "theme-mode-windows" && domainService is WindowsThemeService themeService)
            {
                await themeService.ApplySettingWithContextAsync(settingId, enable, value, applyWallpaper);
            }
            else
            {
                await domainService.ApplySettingAsync(settingId, enable, value);
            }

            _eventBus.Publish(new SettingAppliedEvent(settingId, enable, value));

            if (!enable)
            {
                var hasDependentSettings = rawSettings.Any(s =>
                    s.Dependencies?.Any(d => d.RequiredSettingId == settingId) == true
                );

                if (hasDependentSettings)
                {
                    if (ReferenceEquals(allSettings, rawSettings))
                        allSettings = await domainService.GetSettingsAsync();
                    await _dependencyManager.HandleSettingDisabledAsync(
                        settingId,
                        allSettings,
                        this
                    );
                }
            }
            else if (value != null)
            {
                if (ReferenceEquals(allSettings, rawSettings))
                    allSettings = await domainService.GetSettingsAsync();
                await _dependencyManager.HandleSettingValueChangedAsync(
                    settingId,
                    allSettings,
                    this
                );
            }
        }

        public async Task<SettingApplicationResult> GetSettingStateAsync(string settingId)
        {
            try
            {
                var domainService = _domainServiceRouter.GetDomainService(settingId);
                var isEnabled = await domainService.IsSettingEnabledAsync(settingId);
                var currentValue = await domainService.GetSettingValueAsync(settingId);

                return new SettingApplicationResult
                {
                    Success = true,
                    IsEnabled = isEnabled,
                    CurrentValue = currentValue,
                    Status = isEnabled,
                };
            }
            catch (Exception ex)
            {
                return new SettingApplicationResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<IEnumerable<SettingDefinition>> GetAllSettingsAsync()
        {
            throw new NotSupportedException(
                "GetAllSettingsAsync should be refactored to use domain-specific queries instead of loading all domains"
            );
        }

        public async Task<IEnumerable<SettingDefinition>> GetSettingsByDomainAsync(
            string domainName
        )
        {
            throw new NotSupportedException(
                "GetSettingsByDomainAsync should be refactored to use registry-based domain lookup"
            );
        }

        public async Task ExecuteActionCommandAsync(string settingId, string commandString)
        {
            var context = new ActionExecutionContext
            {
                SettingId = settingId,
                CommandString = commandString,
                ApplyRecommendedSettings = false,
            };

            await ExecuteActionCommandAsync(context);
        }

        public async Task ExecuteActionCommandAsync(ActionExecutionContext context)
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"[SettingApplication] Executing ActionCommand '{context.CommandString}' for setting '{context.SettingId}'"
                );

                // Use SOLID registry pattern for O(1) domain service lookup
                var domainService = _domainServiceRouter.GetDomainService(context.SettingId);

                // Step 1: Execute the domain method (without recommended settings)
                var method = domainService.GetType().GetMethod(context.CommandString);
                if (method == null)
                {
                    throw new NotSupportedException(
                        $"Method '{context.CommandString}' not found on service '{domainService.GetType().Name}'"
                    );
                }

                // Verify method returns Task for async execution
                if (!typeof(Task).IsAssignableFrom(method.ReturnType))
                {
                    throw new NotSupportedException(
                        $"Method '{context.CommandString}' must return Task for async execution"
                    );
                }

                // Invoke the domain method
                var result = method.Invoke(domainService, null);
                if (result is Task task)
                {
                    await task;
                }

                // Step 2: Apply recommended settings separately if requested
                if (context.ApplyRecommendedSettings)
                {
                    _logService.Log(
                        LogLevel.Info,
                        $"[SettingApplication] Applying recommended settings for domain '{domainService.DomainName}'"
                    );

                    await _recommendedSettingsService.ApplyRecommendedSettingsAsync(
                        context.SettingId
                    );

                    _logService.Log(
                        LogLevel.Info,
                        $"[SettingApplication] Successfully applied recommended settings for domain '{domainService.DomainName}'"
                    );
                }

                _logService.Log(
                    LogLevel.Info,
                    $"[SettingApplication] Successfully executed ActionCommand '{context.CommandString}' for setting '{context.SettingId}'"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"[SettingApplication] Error executing ActionCommand '{context.CommandString}' for setting '{context.SettingId}': {ex.Message}"
                );
                throw;
            }
        }

        private IEnumerable<ISettingItem> GetAllSettingsForDependencyCheck()
        {
            try
            {
                var allSettings = _globalSettingsRegistry.GetAllSettings();
                _logService.Log(
                    LogLevel.Debug,
                    $"[SettingApplication] Retrieved {allSettings.Count()} settings for dependency checking"
                );
                return allSettings;
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Warning,
                    $"[SettingApplication] Error retrieving settings for dependency checking: {ex.Message}"
                );
                // Return empty collection to prevent null reference exceptions
                return Enumerable.Empty<ISettingItem>();
            }
        }
    }
}

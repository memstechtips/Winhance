using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{

    public class CompatibleSettingsRegistry : ICompatibleSettingsRegistry
    {
        private readonly IWindowsCompatibilityFilter _windowsFilter;
        private readonly IHardwareCompatibilityFilter _hardwareFilter;
        private readonly IPowerSettingsValidationService _powerValidation;
        private readonly ILogService _logService;

        private bool _isInitialized;
        private readonly SemaphoreSlim _initializationLock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, IEnumerable<SettingDefinition>> _preFilteredSettings = new();
        private readonly Dictionary<string, IEnumerable<SettingDefinition>> _rawSettings = new();
        private readonly Dictionary<string, IEnumerable<SettingDefinition>> _windowsFilterBypassedSettings = new();
        private bool _filterEnabled = true;

        public bool IsInitialized => _isInitialized;

        public CompatibleSettingsRegistry(
            IWindowsCompatibilityFilter windowsFilter,
            IHardwareCompatibilityFilter hardwareFilter,
            IPowerSettingsValidationService powerValidation,
            ILogService logService)
        {
            _windowsFilter = windowsFilter ?? throw new ArgumentNullException(nameof(windowsFilter));
            _hardwareFilter = hardwareFilter ?? throw new ArgumentNullException(nameof(hardwareFilter));
            _powerValidation = powerValidation ?? throw new ArgumentNullException(nameof(powerValidation));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            await _initializationLock.WaitAsync();
            try
            {
                if (_isInitialized) return;

                _logService.Log(LogLevel.Info, "Initializing compatible settings registry with auto-discovery");
                
                await PreFilterAllFeatureSettingsAsync();
                
                _isInitialized = true;
                _logService.Log(LogLevel.Info, $"Compatible settings registry initialized with {_preFilteredSettings.Count} pre-filtered features");
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        public IEnumerable<SettingDefinition> GetFilteredSettings(string featureId)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Registry not initialized");

            _logService.Log(LogLevel.Debug, $"GetFilteredSettings for {featureId}: Filter enabled = {_filterEnabled}");

            if (_filterEnabled)
            {
                return _preFilteredSettings.TryGetValue(featureId, out var settings)
                    ? settings
                    : Enumerable.Empty<SettingDefinition>();
            }
            else
            {
                return _windowsFilterBypassedSettings.TryGetValue(featureId, out var settings)
                    ? settings
                    : Enumerable.Empty<SettingDefinition>();
            }
        }

        public void SetFilterEnabled(bool enabled)
        {
            _filterEnabled = enabled;
        }

        public IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> GetAllFilteredSettings()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Registry not initialized. Call InitializeAsync first.");

            return _filterEnabled ? _preFilteredSettings : _windowsFilterBypassedSettings;
        }

        public IEnumerable<SettingDefinition> GetBypassedSettings(string featureId)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Registry not initialized");

            return _windowsFilterBypassedSettings.TryGetValue(featureId, out var settings)
                ? settings
                : Enumerable.Empty<SettingDefinition>();
        }

        public IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> GetAllBypassedSettings()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Registry not initialized");

            return _windowsFilterBypassedSettings;
        }

        private async Task PreFilterAllFeatureSettingsAsync()
        {
            _logService.Log(LogLevel.Info, "Pre-filtering settings for all features");

            var featureProviders = GetKnownFeatureProviders();
            _logService.Log(LogLevel.Info, $"Found {featureProviders.Count} feature providers");

            foreach (var (featureId, provider) in featureProviders)
            {
                try
                {
                    _logService.Log(LogLevel.Info, $"Loading raw settings for {featureId}");
                    var rawSettings = provider();
                    _rawSettings[featureId] = rawSettings.ToList();
                    _logService.Log(LogLevel.Info, $"Loaded {rawSettings.Count()} raw settings for {featureId}");

                    var filteredSettings = _windowsFilter.FilterSettingsByWindowsVersion(rawSettings);

                    if (featureId == FeatureIds.Power)
                    {
                        filteredSettings = await _hardwareFilter.FilterSettingsByHardwareAsync(filteredSettings);
                        filteredSettings = await _powerValidation.FilterSettingsByExistenceAsync(filteredSettings);
                    }

                    _preFilteredSettings[featureId] = filteredSettings;

                    var bypassedSettings = rawSettings;
                    if (featureId == FeatureIds.Power)
                    {
                        bypassedSettings = await _hardwareFilter.FilterSettingsByHardwareAsync(bypassedSettings);
                        bypassedSettings = await _powerValidation.FilterSettingsByExistenceAsync(bypassedSettings);
                    }
                    var decorated = _windowsFilter.FilterSettingsByWindowsVersion(bypassedSettings, applyFilter: false);
                    _windowsFilterBypassedSettings[featureId] = decorated;

                    _logService.Log(LogLevel.Info, $"Registered {filteredSettings.Count()} settings for {featureId}");
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Error,
                        $"Error loading settings for {featureId}: {ex.Message}");
                    _preFilteredSettings[featureId] = Enumerable.Empty<SettingDefinition>();
                    _rawSettings[featureId] = Enumerable.Empty<SettingDefinition>();
                    _windowsFilterBypassedSettings[featureId] = Enumerable.Empty<SettingDefinition>();
                }
            }

            _logService.Log(LogLevel.Info, "Pre-filtering completed");
        }

        private Dictionary<string, Func<IEnumerable<SettingDefinition>>> GetKnownFeatureProviders()
        {
            var providers = new Dictionary<string, Func<IEnumerable<SettingDefinition>>>();

            try
            {
                var settingClasses = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic && assembly.GetName().Name?.Contains("Winhance") == true)
                    .SelectMany(assembly =>
                    {
                        try
                        {
                            return assembly.GetTypes();
                        }
                        catch
                        {
                            return Enumerable.Empty<Type>();
                        }
                    })
                    .Where(type => type != null && type.IsClass && (
                        type.Name.EndsWith("Customizations") ||
                        type.Name.EndsWith("Optimizations")))
                    .ToList();

                foreach (var settingClass in settingClasses)
                {
                    try
                    {
                        var method = settingClass.GetMethods(BindingFlags.Static | BindingFlags.Public)
                            .FirstOrDefault(m =>
                                m.GetParameters().Length == 0 &&
                                m.ReturnType.GetProperty("Settings") != null &&
                                IsSettingDefinitionEnumerable(m.ReturnType.GetProperty("Settings")!.PropertyType));

                        if (method != null)
                        {
                            var featureId = GetFeatureIdFromMethod(method);

                            providers[featureId] = () => {
                                try
                                {
                                    var result = method.Invoke(null, null);
                                    var settingsProperty = result!.GetType().GetProperty("Settings");
                                    return (IEnumerable<SettingDefinition>)settingsProperty!.GetValue(result)!;
                                }
                                catch
                                {
                                    return Enumerable.Empty<SettingDefinition>();
                                }
                            };
                        }
                    }
                    catch
                    {
                        // Continue processing other classes
                    }
                }
            }
            catch
            {
                // Return empty providers if discovery fails
            }

            return providers;
        }

        private bool IsSettingDefinitionEnumerable(Type type)
        {
            return type.GetInterfaces()
                .Any(i => i.IsGenericType && 
                     i.GetGenericTypeDefinition() == typeof(IEnumerable<>) &&
                     i.GetGenericArguments()[0] == typeof(SettingDefinition));
        }

        private string GetFeatureIdFromMethod(MethodInfo method)
        {
            try
            {
                var result = method.Invoke(null, null);
                var featureIdProperty = result!.GetType().GetProperty("FeatureId");
                return (string)featureIdProperty!.GetValue(result)!;
            }
            catch
            {
                return "Unknown";
            }
        }


        private IEnumerable<SettingDefinition> FilterSettingsForFeature(string featureId, IEnumerable<SettingDefinition> rawSettings)
        {
            var filtered = rawSettings.AsEnumerable();

            filtered = _windowsFilter.FilterSettingsByWindowsVersion(filtered);

            if (featureId == FeatureIds.Power)
            {
                filtered = _hardwareFilter.FilterSettingsByHardwareAsync(filtered).GetAwaiter().GetResult();
                filtered = _powerValidation.FilterSettingsByExistenceAsync(filtered).GetAwaiter().GetResult();
            }

            return filtered;
        }
    }
}
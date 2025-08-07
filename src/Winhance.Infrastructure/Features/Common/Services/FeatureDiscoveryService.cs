using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Implementation of the feature discovery service.
    /// Manages registration and discovery of feature descriptors.
    /// </summary>
    public class FeatureDiscoveryService : IFeatureDiscoveryService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogService _logService;
        private readonly List<IFeatureDescriptor> _descriptors = new();
        private readonly object _lockObject = new();

        public FeatureDiscoveryService(IServiceProvider serviceProvider, ILogService logService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Discovers all available features for a specific category, filtered by availability.
        /// </summary>
        /// <param name="category">The category to filter by.</param>
        /// <returns>Available feature descriptors for the specified category, ordered by sort order.</returns>
        public async Task<IEnumerable<IFeatureDescriptor>> DiscoverFeaturesAsync(string category)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Discovering features for category: {category}");

                var categoryDescriptors = _descriptors.Where(d => 
                    string.Equals(d.Category, category, StringComparison.OrdinalIgnoreCase));

                var availableDescriptors = new List<IFeatureDescriptor>();

                foreach (var descriptor in categoryDescriptors)
                {
                    try
                    {
                        if (await descriptor.IsAvailableAsync())
                        {
                            availableDescriptors.Add(descriptor);
                            _logService.Log(LogLevel.Info, $"Feature '{descriptor.ModuleId}' is available");
                        }
                        else
                        {
                            _logService.Log(LogLevel.Info, $"Feature '{descriptor.ModuleId}' is not available on this system");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Error, $"Error checking availability for feature '{descriptor.ModuleId}': {ex.Message}");
                    }
                }

                var orderedDescriptors = availableDescriptors.OrderBy(d => d.SortOrder).ToList();
                _logService.Log(LogLevel.Info, $"Found {orderedDescriptors.Count} available features for category '{category}'");

                return orderedDescriptors;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error discovering features for category '{category}': {ex.Message}");
                return Enumerable.Empty<IFeatureDescriptor>();
            }
        }

        /// <summary>
        /// Gets a specific feature descriptor by its identifier.
        /// </summary>
        /// <param name="moduleId">The unique identifier of the feature.</param>
        /// <returns>The feature descriptor if found; otherwise, null.</returns>
        public async Task<IFeatureDescriptor> GetFeatureAsync(string moduleId)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Getting feature: {moduleId}");

                var descriptor = _descriptors.FirstOrDefault(d => 
                    string.Equals(d.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase));

                if (descriptor == null)
                {
                    _logService.Log(LogLevel.Warning, $"Feature '{moduleId}' not found");
                    return null;
                }

                if (await descriptor.IsAvailableAsync())
                {
                    _logService.Log(LogLevel.Info, $"Feature '{moduleId}' found and available");
                    return descriptor;
                }

                _logService.Log(LogLevel.Warning, $"Feature '{moduleId}' found but not available");
                return null;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting feature '{moduleId}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Registers a feature descriptor with the discovery service.
        /// </summary>
        /// <param name="descriptor">The feature descriptor to register.</param>
        public void RegisterFeature(IFeatureDescriptor descriptor)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            lock (_lockObject)
            {
                // Check for duplicate module IDs
                if (_descriptors.Any(d => string.Equals(d.ModuleId, descriptor.ModuleId, StringComparison.OrdinalIgnoreCase)))
                {
                    _logService.Log(LogLevel.Warning, $"Feature '{descriptor.ModuleId}' is already registered. Skipping registration.");
                    return;
                }

                _descriptors.Add(descriptor);
                _logService.Log(LogLevel.Info, $"Registered feature: {descriptor.ModuleId} (Category: {descriptor.Category}, Display: {descriptor.DisplayName})");
            }
        }

        /// <summary>
        /// Gets all registered feature descriptors.
        /// </summary>
        /// <returns>A collection of all registered feature descriptors.</returns>
        public async Task<IEnumerable<IFeatureDescriptor>> GetAllFeaturesAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Getting all registered features");
                
                var availableDescriptors = new List<IFeatureDescriptor>();

                foreach (var descriptor in _descriptors)
                {
                    try
                    {
                        if (await descriptor.IsAvailableAsync())
                        {
                            availableDescriptors.Add(descriptor);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Error, $"Error checking availability for feature '{descriptor.ModuleId}': {ex.Message}");
                    }
                }

                _logService.Log(LogLevel.Info, $"Found {availableDescriptors.Count} available features out of {_descriptors.Count} registered");
                return availableDescriptors.OrderBy(d => d.Category).ThenBy(d => d.SortOrder);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting all features: {ex.Message}");
                return Enumerable.Empty<IFeatureDescriptor>();
            }
        }

        /// <summary>
        /// Gets all available categories.
        /// </summary>
        /// <returns>A collection of category names.</returns>
        public async Task<IEnumerable<string>> GetCategoriesAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Getting all available categories");

                var allDescriptors = await GetAllFeaturesAsync();
                var categories = allDescriptors
                    .Select(d => d.Category)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(c => c)
                    .ToList();

                _logService.Log(LogLevel.Info, $"Found {categories.Count} categories: {string.Join(", ", categories)}");
                return categories;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting categories: {ex.Message}");
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// Checks if a feature with the specified module ID is registered.
        /// </summary>
        /// <param name="moduleId">The module ID to check.</param>
        /// <returns>True if the feature is registered; otherwise, false.</returns>
        public bool IsFeatureRegistered(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                return false;

            lock (_lockObject)
            {
                return _descriptors.Any(d => string.Equals(d.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}

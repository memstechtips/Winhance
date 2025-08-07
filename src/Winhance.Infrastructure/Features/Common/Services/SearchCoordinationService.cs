using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Domain service implementation for coordinating search operations across multiple features.
    /// Follows Domain Driven Design principles and maintains separation of concerns.
    /// </summary>
    public class SearchCoordinationService : ISearchCoordinationService
    {
        private readonly ConcurrentDictionary<string, Action<string>> _registeredFeatures = new();
        private readonly ConcurrentDictionary<string, bool> _featureVisibility = new();
        private readonly ILogService? _logService;
        private string _currentSearchText = string.Empty;

        public SearchCoordinationService(ILogService? logService = null)
        {
            _logService = logService;
        }

        /// <summary>
        /// Event fired when search text changes.
        /// </summary>
        public event EventHandler<SearchTextChangedEventArgs>? SearchTextChanged;

        /// <summary>
        /// Gets the current search text.
        /// </summary>
        public string CurrentSearchText => _currentSearchText;

        /// <summary>
        /// Updates the search text and notifies all subscribers.
        /// </summary>
        /// <param name="searchText">The new search text.</param>
        public void UpdateSearchText(string searchText)
        {
            var previousSearchText = _currentSearchText;
            _currentSearchText = searchText ?? string.Empty;

            _logService?.Log(LogLevel.Debug, $"[SearchCoordinationService] UpdateSearchText called - Previous: '{previousSearchText}', Current: '{_currentSearchText}', Registered features: {_registeredFeatures.Count}");

            // Notify all registered features
            foreach (var kvp in _registeredFeatures)
            {
                _logService?.Log(LogLevel.Debug, $"[SearchCoordinationService] Notifying feature '{kvp.Key}' with search text: '{_currentSearchText}'");
                kvp.Value(_currentSearchText);
            }

            // Fire the event for any additional subscribers
            SearchTextChanged?.Invoke(this, new SearchTextChangedEventArgs(_currentSearchText, previousSearchText));
            _logService?.Log(LogLevel.Debug, $"[SearchCoordinationService] SearchTextChanged event fired for '{_currentSearchText}'");
        }

        /// <summary>
        /// Registers a feature for search coordination.
        /// </summary>
        /// <param name="featureId">Unique identifier for the feature.</param>
        /// <param name="searchHandler">Handler that will be called when search text changes.</param>
        public void RegisterFeature(string featureId, Action<string> searchHandler)
        {
            if (string.IsNullOrWhiteSpace(featureId))
                throw new ArgumentException("Feature ID cannot be null or empty.", nameof(featureId));
            
            if (searchHandler == null)
                throw new ArgumentNullException(nameof(searchHandler));

            _registeredFeatures[featureId] = searchHandler;
            _featureVisibility[featureId] = true; // Default to visible

            // If there's already a search text, apply it to the newly registered feature
            if (!string.IsNullOrEmpty(_currentSearchText))
            {
                searchHandler(_currentSearchText);
            }
        }

        /// <summary>
        /// Unregisters a feature from search coordination.
        /// </summary>
        /// <param name="featureId">Unique identifier for the feature.</param>
        public void UnregisterFeature(string featureId)
        {
            if (string.IsNullOrWhiteSpace(featureId))
                return;

            _registeredFeatures.TryRemove(featureId, out _);
            _featureVisibility.TryRemove(featureId, out _);
        }

        /// <summary>
        /// Gets the visibility state for a feature based on current search results.
        /// </summary>
        /// <param name="featureId">Unique identifier for the feature.</param>
        /// <returns>True if the feature should be visible, false otherwise.</returns>
        public bool GetFeatureVisibility(string featureId)
        {
            if (string.IsNullOrWhiteSpace(featureId))
                return false;

            return _featureVisibility.GetValueOrDefault(featureId, true);
        }

        /// <summary>
        /// Updates the visibility state for a feature.
        /// </summary>
        /// <param name="featureId">Unique identifier for the feature.</param>
        /// <param name="hasVisibleItems">Whether the feature has any visible items.</param>
        public void UpdateFeatureVisibility(string featureId, bool hasVisibleItems)
        {
            if (string.IsNullOrWhiteSpace(featureId))
                return;

            var previousVisibility = _featureVisibility.GetValueOrDefault(featureId, true);
            _featureVisibility[featureId] = hasVisibleItems;
            
            _logService?.Log(LogLevel.Debug, $"[SearchCoordinationService] UpdateFeatureVisibility - FeatureId: '{featureId}', Previous: {previousVisibility}, Current: {hasVisibleItems}");
        }
    }
}

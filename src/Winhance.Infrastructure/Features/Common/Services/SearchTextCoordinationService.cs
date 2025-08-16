using System;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Clean domain service implementation for coordinating search text across application features.
    /// Follows clean architecture principles - only handles search text, no UI visibility logic.
    /// </summary>
    public class SearchTextCoordinationService : ISearchTextCoordinationService
    {
        private readonly ILogService? _logService;
        private string _currentSearchText = string.Empty;

        public SearchTextCoordinationService(ILogService? logService = null)
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

            _logService?.Log(LogLevel.Debug, $"[SearchTextCoordinationService] Search text changed from '{previousSearchText}' to '{_currentSearchText}'");

            // Fire the event for all subscribers
            SearchTextChanged?.Invoke(this, new SearchTextChangedEventArgs(_currentSearchText, previousSearchText));
        }
    }
}
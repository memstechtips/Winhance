using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

namespace Winhance.WPF.Features.SoftwareApps.Services
{
    /// <summary>
    /// Service for debounced search operations with optimized string matching
    /// </summary>
    public class DebouncedSearchService : IDisposable
    {
        private readonly DispatcherTimer _searchTimer;
        private readonly Dictionary<string, string[]> _searchTermsCache;
        private readonly object _lockObject = new object();
        
        private string _lastSearchText = string.Empty;
        private Action<string> _searchAction;
        private bool _disposed = false;

        public DebouncedSearchService(TimeSpan debounceInterval = default)
        {
            var interval = debounceInterval == default ? TimeSpan.FromMilliseconds(300) : debounceInterval;
            
            _searchTimer = new DispatcherTimer
            {
                Interval = interval
            };
            _searchTimer.Tick += OnSearchTimerTick;
            
            _searchTermsCache = new Dictionary<string, string[]>();
        }

        /// <summary>
        /// Performs a debounced search operation
        /// </summary>
        /// <param name="searchText">The search text</param>
        /// <param name="searchAction">Action to execute with the search text</param>
        public void Search(string searchText, Action<string> searchAction)
        {
            if (_disposed) return;

            lock (_lockObject)
            {
                _lastSearchText = searchText ?? string.Empty;
                _searchAction = searchAction;
                
                _searchTimer.Stop();
                _searchTimer.Start();
            }
        }

        /// <summary>
        /// Gets cached search terms for a search text (optimized for repeated searches)
        /// </summary>
        public string[] GetSearchTerms(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return Array.Empty<string>();

            lock (_lockObject)
            {
                if (_searchTermsCache.TryGetValue(searchText, out var cachedTerms))
                    return cachedTerms;

                var terms = searchText.ToLowerInvariant()
                    .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                // Cache the result (limit cache size to prevent memory issues)
                if (_searchTermsCache.Count > 100)
                {
                    // Remove oldest entries (simple FIFO)
                    var keysToRemove = _searchTermsCache.Keys.Take(50).ToList();
                    foreach (var key in keysToRemove)
                    {
                        _searchTermsCache.Remove(key);
                    }
                }

                _searchTermsCache[searchText] = terms;
                return terms;
            }
        }

        /// <summary>
        /// Optimized string matching for search terms
        /// </summary>
        public bool MatchesSearchTerms(string[] searchTerms, params string[] searchableFields)
        {
            if (searchTerms == null || searchTerms.Length == 0)
                return true;

            if (searchableFields == null || searchableFields.Length == 0)
                return false;

            // Pre-convert all searchable fields to lowercase for comparison
            var lowerFields = searchableFields
                .Where(field => !string.IsNullOrEmpty(field))
                .Select(field => field.ToLowerInvariant())
                .ToArray();

            if (lowerFields.Length == 0)
                return false;

            // All search terms must match at least one field
            return searchTerms.All(term =>
                lowerFields.Any(field => field.Contains(term))
            );
        }

        /// <summary>
        /// Creates an optimized filter predicate for collection views
        /// </summary>
        public Predicate<object> CreateFilterPredicate<T>(string searchText, Func<T, string[]> fieldExtractor)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return null;

            var searchTerms = GetSearchTerms(searchText);
            if (searchTerms.Length == 0)
                return null;

            return obj =>
            {
                if (obj is T item)
                {
                    var fields = fieldExtractor(item);
                    return MatchesSearchTerms(searchTerms, fields);
                }
                return false;
            };
        }

        /// <summary>
        /// Immediately executes the search without debouncing
        /// </summary>
        public void SearchImmediate(string searchText, Action<string> searchAction)
        {
            if (_disposed) return;

            lock (_lockObject)
            {
                _searchTimer.Stop();
                searchAction?.Invoke(searchText ?? string.Empty);
            }
        }

        private void OnSearchTimerTick(object sender, EventArgs e)
        {
            _searchTimer.Stop();
            
            string searchText;
            Action<string> action;
            
            lock (_lockObject)
            {
                searchText = _lastSearchText;
                action = _searchAction;
            }
            
            action?.Invoke(searchText);
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            
            if (_searchTimer != null)
            {
                _searchTimer.Stop();
                _searchTimer.Tick -= OnSearchTimerTick;
            }
            
            lock (_lockObject)
            {
                _searchTermsCache.Clear();
            }
            
            GC.SuppressFinalize(this);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Threading;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.WPF.Features.SoftwareApps.Services
{
    /// <summary>
    /// Optimized collection manager for table view collections
    /// Implements incremental updates, debouncing, and proper memory management
    /// </summary>
    /// <typeparam name="TSource">Source item type</typeparam>
    /// <typeparam name="TWrapper">Wrapper item type</typeparam>
    public class OptimizedCollectionManager<TSource, TWrapper> : IDisposable
        where TSource : class, INotifyPropertyChanged
        where TWrapper : class, INotifyPropertyChanged, IDisposable
    {
        private readonly ObservableCollection<TWrapper> _collection;
        private readonly ICollectionView _collectionView;
        private readonly Func<TSource, TWrapper> _wrapperFactory;
        private readonly Dictionary<TSource, TWrapper> _itemMap;
        private readonly DispatcherTimer _updateTimer;
        private readonly object _lockObject = new object();
        private readonly ILogService _logService;

        private bool _isUpdating = false;
        private bool _pendingUpdate = false;
        private bool _disposed = false;
        private IEnumerable<TSource> _pendingSourceItems;

        public ICollectionView CollectionView => _collectionView;
        public ObservableCollection<TWrapper> Collection => _collection;

        public OptimizedCollectionManager(Func<TSource, TWrapper> wrapperFactory, ILogService logService = null)
        {
            _wrapperFactory = wrapperFactory ?? throw new ArgumentNullException(nameof(wrapperFactory));
            _logService = logService;
            _collection = new ObservableCollection<TWrapper>();
            _collectionView = CollectionViewSource.GetDefaultView(_collection);
            _itemMap = new Dictionary<TSource, TWrapper>();


            // Setup debounced update timer
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 50ms debounce
            };
            _updateTimer.Tick += OnUpdateTimerTick;
        }

        /// <summary>
        /// Updates the collection with new source items using incremental updates
        /// </summary>
        public void UpdateCollection(IEnumerable<TSource> sourceItems)
        {
            if (_disposed) return;

            var sourceCount = sourceItems?.Count() ?? 0;

            lock (_lockObject)
            {
                _pendingSourceItems = sourceItems;

                if (_isUpdating)
                {
                    _pendingUpdate = true;
                    return;
                }

                _pendingUpdate = false;
                _updateTimer.Stop();
                _updateTimer.Start();
            }
        }

        /// <summary>
        /// Immediately updates the collection (bypasses debouncing)
        /// </summary>
        public void UpdateCollectionImmediate(IEnumerable<TSource> sourceItems)
        {
            if (_disposed) return;

            lock (_lockObject)
            {
                _updateTimer.Stop();
                PerformUpdate(sourceItems);
            }
        }

        private void OnUpdateTimerTick(object sender, EventArgs e)
        {
            _updateTimer.Stop();

            lock (_lockObject)
            {
                if (_pendingUpdate)
                {
                    _pendingUpdate = false;
                    _updateTimer.Start();
                    return;
                }

                // Perform the actual update
                if (_pendingSourceItems != null)
                {
                    var sourceCount = _pendingSourceItems.Count();
                    PerformUpdate(_pendingSourceItems);
                    _pendingSourceItems = null;
                }
            }
        }

        private void PerformUpdate(IEnumerable<TSource> sourceItems)
        {
            if (_disposed) return;

            try
            {
                _isUpdating = true;

                var sourceList = sourceItems?.ToList() ?? new List<TSource>();
                var currentItems = new HashSet<TSource>(_itemMap.Keys);
                var newItems = new HashSet<TSource>(sourceList);


                // Remove items that are no longer in the source
                var itemsToRemove = currentItems.Except(newItems).ToList();
                foreach (var item in itemsToRemove)
                {
                    if (_itemMap.TryGetValue(item, out var wrapper))
                    {
                        _collection.Remove(wrapper);
                        _itemMap.Remove(item);
                        wrapper.Dispose();
                    }
                }

                // Add new items
                var itemsToAdd = newItems.Except(currentItems).ToList();
                foreach (var item in itemsToAdd)
                {
                    var wrapper = _wrapperFactory(item);
                    _itemMap[item] = wrapper;
                    _collection.Add(wrapper);
                }

                // Update existing items (if needed)
                var existingItems = newItems.Intersect(currentItems);
                foreach (var item in existingItems)
                {
                    // Existing items are automatically updated through property binding
                    // No action needed unless wrapper needs refresh
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        /// <summary>
        /// Applies sorting to the collection view
        /// </summary>
        public void ApplySort(string propertyName, ListSortDirection direction)
        {
            if (_disposed || _collectionView == null) return;

            _collectionView.SortDescriptions.Clear();
            if (!string.IsNullOrEmpty(propertyName))
            {
                _collectionView.SortDescriptions.Add(new SortDescription(propertyName, direction));
            }
        }

        /// <summary>
        /// Applies a filter to the collection view with debouncing
        /// </summary>
        public void ApplyFilter(Predicate<object> filter)
        {
            if (_disposed) return;

            // Debounce filter updates
            _updateTimer.Stop();
            _updateTimer.Tick -= OnUpdateTimerTick;

            // Create a temporary handler for the filter operation
            EventHandler filterHandler = null;
            filterHandler = (s, e) =>
            {
                _updateTimer.Stop();
                _updateTimer.Tick -= filterHandler;
                _updateTimer.Tick += OnUpdateTimerTick;

                _collectionView.Filter = filter;
            };

            _updateTimer.Tick += filterHandler;
            _updateTimer.Start();
        }

        /// <summary>
        /// Clears the collection and disposes all wrappers
        /// </summary>
        public void Clear()
        {
            if (_disposed) return;

            lock (_lockObject)
            {
                foreach (var wrapper in _itemMap.Values)
                {
                    wrapper.Dispose();
                }

                _itemMap.Clear();
                _collection.Clear();
            }
        }

        /// <summary>
        /// Gets the wrapper for a source item
        /// </summary>
        public TWrapper GetWrapper(TSource sourceItem)
        {
            return _itemMap.TryGetValue(sourceItem, out var wrapper) ? wrapper : null;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            if (_updateTimer != null)
            {
                _updateTimer.Stop();
                _updateTimer.Tick -= OnUpdateTimerTick;
            }

            Clear();

            GC.SuppressFinalize(this);
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class ViewPoolService : IViewPoolService
    {
        private readonly ConcurrentDictionary<Type, ConcurrentBag<object>> _viewPool = new();
        private readonly ConcurrentDictionary<Type, int> _maxPoolSizes;
        private readonly ConcurrentDictionary<string, int> _createdCounts = new();
        private readonly ConcurrentDictionary<string, int> _reusedCounts = new();
        private readonly ILogService _logService;
        private readonly object _statsLock = new();

        public ViewPoolService(ILogService logService, Dictionary<Type, int> poolSizes = null)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _maxPoolSizes = new ConcurrentDictionary<Type, int>(poolSizes ?? new Dictionary<Type, int>());
        }

        public object GetOrCreateView(Type viewType, IServiceProvider serviceProvider)
        {
            if (viewType == null) throw new ArgumentNullException(nameof(viewType));
            if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

            // Try to get from pool first
            if (_viewPool.TryGetValue(viewType, out var pool) && pool.TryTake(out var pooledView))
            {
                lock (_statsLock)
                {
                    _reusedCounts.AddOrUpdate(viewType.Name, 1, (k, v) => v + 1);
                }

                _logService.Log(Core.Features.Common.Enums.LogLevel.Debug,
                    $"ViewPool: Reused {viewType.Name} from pool");

                ResetViewState(pooledView);
                return pooledView;
            }

            // Create new view
            var newView = serviceProvider.GetRequiredService(viewType);

            lock (_statsLock)
            {
                _createdCounts.AddOrUpdate(viewType.Name, 1, (k, v) => v + 1);
            }

            _logService.Log(Core.Features.Common.Enums.LogLevel.Debug,
                $"ViewPool: Created new {viewType.Name}");

            return newView;
        }

        public void ReturnView(Type viewType, object view, bool clearDataContext = true)
        {
            if (viewType == null || view == null) return;

            var pool = _viewPool.GetOrAdd(viewType, _ => new ConcurrentBag<object>());

            var maxSize = _maxPoolSizes.GetOrAdd(viewType, _ => 1);
            if (pool.Count >= maxSize)
            {
                _logService.Log(Core.Features.Common.Enums.LogLevel.Debug,
                    $"ViewPool: Rejected {viewType.Name} - pool full");

                if (clearDataContext && view is FrameworkElement fe)
                {
                    fe.DataContext = null;
                }
                return;
            }

            // Clear DataContext to break ViewModel reference
            if (clearDataContext && view is FrameworkElement element)
            {
                element.DataContext = null;
            }

            // Clear bindings to prevent memory leaks
            if (view is Control control)
            {
                System.Windows.Data.BindingOperations.ClearAllBindings(control);
            }

            pool.Add(view);
            _logService.Log(Core.Features.Common.Enums.LogLevel.Debug,
                $"ViewPool: Returned {viewType.Name} to pool (size: {pool.Count})");
        }

        public void ClearPool()
        {
            _logService.Log(Core.Features.Common.Enums.LogLevel.Info, "ViewPool: Clearing all pooled views");

            foreach (var pool in _viewPool.Values)
            {
                while (pool.TryTake(out _)) { }
            }

            _viewPool.Clear();
        }

        public ViewPoolStatistics GetStatistics()
        {
            lock (_statsLock)
            {
                return new ViewPoolStatistics
                {
                    PooledViewCounts = _viewPool.ToDictionary(
                        kvp => kvp.Key.Name,
                        kvp => kvp.Value.Count),
                    CreatedViewCounts = new Dictionary<string, int>(_createdCounts),
                    ReusedViewCounts = new Dictionary<string, int>(_reusedCounts),
                    TotalPooledViews = _viewPool.Sum(kvp => kvp.Value.Count),
                    TotalCreatedViews = _createdCounts.Sum(kvp => kvp.Value),
                    TotalReusedViews = _reusedCounts.Sum(kvp => kvp.Value)
                };
            }
        }

        private void ResetViewState(object view)
        {
            if (view is FrameworkElement element)
            {
                element.Visibility = Visibility.Visible;
                element.IsEnabled = true;
                element.Opacity = 1.0;
            }
        }
    }
}

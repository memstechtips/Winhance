using System;
using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IViewPoolService
    {
        object GetOrCreateView(Type viewType, IServiceProvider serviceProvider);

        void ReturnView(Type viewType, object view, bool clearDataContext = true);

        void ClearPool();

        ViewPoolStatistics GetStatistics();
    }

    public class ViewPoolStatistics
    {
        public Dictionary<string, int> PooledViewCounts { get; set; } = new();
        public Dictionary<string, int> CreatedViewCounts { get; set; } = new();
        public Dictionary<string, int> ReusedViewCounts { get; set; } = new();
        public int TotalPooledViews { get; set; }
        public int TotalCreatedViews { get; set; }
        public int TotalReusedViews { get; set; }
    }
}

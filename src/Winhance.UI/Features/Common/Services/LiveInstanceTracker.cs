using System.Runtime.CompilerServices;

namespace Winhance.UI.Features.Common.Services;

// Weak references to pages and view models, counted by type, to see which ones outlive their screen.
public sealed class LiveInstanceTracker
{
    private const int PruneFloor = 1024;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, List<WeakReference>> _byType = [];
    private readonly Dictionary<string, int> _pruneAt = [];
    private readonly ConditionalWeakTable<object, object?> _seen = new();

    // Off for the shared tracker until --diag-memory turns it on: nothing is held or counted otherwise.
    public static LiveInstanceTracker Shared { get; } = new() { Enabled = false };

    public bool Enabled { get; set; } = true;

    public void Register(object instance) => Add(instance);

    // For an object that reports itself repeatedly, such as a cached page on every navigation.
    public void RegisterOnce(object instance)
    {
        if (!Enabled)
            return;

        lock (_gate)
        {
            if (!_seen.TryAdd(instance, null))
                return;
            Add(instance);
        }
    }

    // Alive means not yet collected: without a full GC first, this includes garbage nobody references.
    public IReadOnlyDictionary<string, int> AliveCounts()
    {
        lock (_gate)
        {
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var (type, list) in _byType)
            {
                list.RemoveAll(r => !r.IsAlive);
                if (list.Count > 0)
                    counts[type] = list.Count;
            }

            return counts;
        }
    }

    private void Add(object instance)
    {
        if (!Enabled)
            return;

        var type = instance.GetType().Name;
        lock (_gate)
        {
            if (!_byType.TryGetValue(type, out var list))
                _byType[type] = list = [];
            list.Add(new WeakReference(instance));

            if (list.Count >= _pruneAt.GetValueOrDefault(type, PruneFloor))
            {
                list.RemoveAll(r => !r.IsAlive);
                _pruneAt[type] = Math.Max(PruneFloor, list.Count * 2);
            }
        }
    }
}

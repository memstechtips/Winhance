using System.Collections.ObjectModel;

namespace Winhance.UI.Features.Common.Helpers;

// Clear() raises Reset, so a bound list drops every container and its selection. This edits the collection into
// the new contents (PowerToys CmdPal ListHelpers.InPlaceUpdateList) on the calling thread, never deferred: bound
// controls read dependency properties in their handlers, which is how NotificationDeferrer broke the apps DataGrid.
public static class ObservableCollectionExtensions
{
    // Returns the items that left the collection, so the caller can dispose exactly those.
    public static List<T> ReplaceInPlace<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
        where T : class
    {
        // Copied first: items may be a query over the collection itself.
        var target = items.ToList();
        var removed = new List<T>();
        var comparer = EqualityComparer<T>.Default;
        var wanted = new HashSet<T>(target, comparer);

        // With nothing in common, an indexed set reuses each container instead of a Remove plus an Add.
        if (!collection.Any(wanted.Contains))
        {
            var shared = Math.Min(collection.Count, target.Count);
            for (var i = 0; i < shared; i++)
            {
                removed.Add(collection[i]);
                collection[i] = target[i];
            }

            TrimTo(collection, target.Count, removed);

            for (var i = shared; i < target.Count; i++)
                collection.Add(target[i]);

            return removed;
        }

        var index = 0;
        for (; index < target.Count && index < collection.Count; index++)
        {
            var item = target[index];

            var found = -1;
            for (var j = index; j < collection.Count; j++)
            {
                if (comparer.Equals(collection[j], item))
                {
                    found = j;
                    break;
                }
            }

            if (found < 0)
            {
                collection.Insert(index, item);
                continue;
            }

            // Items still wanted further down stay put, so they are moved once instead of removed and re-added.
            for (var k = found - 1; k >= index; k--)
            {
                if (!wanted.Contains(collection[k]))
                {
                    removed.Add(collection[k]);
                    collection.RemoveAt(k);
                    found--;
                }
            }

            if (found > index)
                collection.Move(found, index);
        }

        TrimTo(collection, target.Count, removed);

        for (; index < target.Count; index++)
            collection.Add(target[index]);

        return removed;
    }

    private static void TrimTo<T>(ObservableCollection<T> collection, int count, List<T> removed)
    {
        while (collection.Count > count)
        {
            removed.Add(collection[^1]);
            collection.RemoveAt(collection.Count - 1);
        }
    }
}

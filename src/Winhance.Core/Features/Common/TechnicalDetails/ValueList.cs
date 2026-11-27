using System.Collections;

namespace Winhance.Core.Features.Common.TechnicalDetails;

// A record compares a list member by reference, so two matrices built from the same machine state never compared
// equal and every refresh redrew every table. Holding the lists in this makes record equality compare contents.
public sealed class ValueList<T> : IReadOnlyList<T>, IEquatable<ValueList<T>>
{
    private readonly T[] _items;

    private ValueList(T[] items) => _items = items;

    public static ValueList<T> Empty { get; } = new([]);

    public static ValueList<T> From(IReadOnlyList<T> items) =>
        items as ValueList<T> ?? (items.Count == 0 ? Empty : new([.. items]));

    public T this[int index] => _items[index];

    public int Count => _items.Length;

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(ValueList<T>? other) =>
        other is not null && (ReferenceEquals(this, other) || _items.SequenceEqual(other._items));

    public override bool Equals(object? obj) => Equals(obj as ValueList<T>);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in _items)
            hash.Add(item);
        return hash.ToHashCode();
    }
}

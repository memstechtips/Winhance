namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// One registry target's reduced reading. <paramref name="Present"/> is false when the target is
/// absent OR unusable, so no matcher can ever see a value it cannot interpret.
/// </summary>
/// <param name="Value">The reduced value (a bool for a bitmask/flag target, a byte for a
/// single-byte target, the raw value otherwise), or null when not present.</param>
/// <param name="Present">Whether the target yielded a usable value.</param>
/// <param name="KindMismatch">The value EXISTS and was read, but its CLR type cannot satisfy the
/// target's declared shape - e.g. a bitmask target whose value is not a byte array. Distinct from
/// plain absence: absence is normal and states handle it via <c>.OrAbsent()</c>, whereas a kind
/// mismatch means the stored value is malformed and no state can honestly match it.</param>
public readonly record struct TargetReading(object? Value, bool Present, bool KindMismatch)
{
    /// <summary>A target that is simply not there.</summary>
    public static readonly TargetReading Absent = new(null, false, false);

    /// <summary>A usable reading.</summary>
    public static TargetReading Of(object? value) => new(value, true, false);

    /// <summary>The value exists but its type cannot satisfy the target's shape. Never Present -
    /// a malformed value must not reach the matchers.</summary>
    public static readonly TargetReading Malformed = new(null, false, true);

    /// <summary>Two-element deconstruction for the many call sites that only care about the reduced
    /// value and presence. The three-element positional deconstruction is also available.</summary>
    public void Deconstruct(out object? value, out bool present)
    {
        value = Value;
        present = Present;
    }
}

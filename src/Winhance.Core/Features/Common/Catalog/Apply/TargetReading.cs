namespace Winhance.Core.Features.Common.Catalog;

// Present is false when the target is absent OR unusable, so no matcher sees a value it cannot interpret.
// KindMismatch: the value exists but its CLR type cannot satisfy the target's shape - malformed, not absent.
public readonly record struct TargetReading(object? Value, bool Present, bool KindMismatch)
{
    public static readonly TargetReading Absent = new(null, false, false);

    public static TargetReading Of(object? value) => new(value, true, false);

    // Never Present: a malformed value must not reach the matchers.
    public static readonly TargetReading Malformed = new(null, false, true);

    public void Deconstruct(out object? value, out bool present)
    {
        value = Value;
        present = Present;
    }
}

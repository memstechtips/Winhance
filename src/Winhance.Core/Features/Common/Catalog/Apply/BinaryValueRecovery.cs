namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// The rule for turning whatever is currently stored at a REG_BINARY target into the byte array a surgical
/// edit (single bit / single byte) should operate on - WITHOUT ever discarding data we were able to read.
///
/// This is a domain rule, not registry plumbing, which is why it lives in Core and is pure: it decides what
/// counts as recoverable, and getting it wrong destroys user data silently. Before it existed, every
/// non-<c>byte[]</c> value took the "nothing there, start fresh" path and was overwritten with a zeroed
/// array - so one click on a setting backed by a wrongly-typed UserPreferencesMask reset every unrelated
/// preference packed into that same value.
/// </summary>
public static class BinaryValueRecovery
{
    /// <summary>The length Windows uses for the values this applies to, and the floor for a fresh array.</summary>
    public const int MinimumLength = 12;

    /// <summary>
    /// The buffer to edit, or <c>null</c> when the current value cannot be safely interpreted as bytes - in
    /// which case the caller must REFUSE the write, never fall back to a zeroed array.
    /// </summary>
    /// <param name="currentValue">What the registry currently holds: a byte array, null (absent), a string
    /// (the value was rewritten under REG_SZ / REG_EXPAND_SZ), or something else.</param>
    /// <param name="byteIndex">The index the caller is about to edit; the buffer is always long enough.</param>
    public static byte[]? Resolve(object? currentValue, int byteIndex)
    {
        int minLength = Math.Max(MinimumLength, byteIndex + 1);

        switch (currentValue)
        {
            case byte[] bytes:
                return bytes;

            // Genuinely absent - there is nothing to preserve, so a zeroed array is correct.
            case null:
                return new byte[minLength];

            // The value was rewritten under a string type with its BYTES INTACT: a registry string is
            // UTF-16LE, so the original bytes are still there, merely being interpreted as text (which is
            // why such a value displays as unexpected CJK/Ethiopic glyphs). Round-trip it back.
            //
            // Lossless: RegistryKey.GetValue stops at the UTF-16 terminator, so the only bytes it can have
            // dropped are the trailing zeros that FORMED that terminator - and zeros are exactly what the
            // padding below restores.
            case string text:
            {
                var recovered = System.Text.Encoding.Unicode.GetBytes(text);
                var buffer = new byte[Math.Max(minLength, recovered.Length)];
                Array.Copy(recovered, buffer, recovered.Length);
                return buffer;
            }

            // A DWORD, a multi-string, anything else: there is no honest byte representation, so refuse.
            // Recover or refuse - never guess.
            default:
                return null;
        }
    }

    /// <summary>Whether <paramref name="currentValue"/> is a value we RECOVERED rather than used as-is or
    /// created fresh. Callers use this only to log the repair; it never changes what is written.</summary>
    public static bool IsRecoveredFromString(object? currentValue) => currentValue is string;
}

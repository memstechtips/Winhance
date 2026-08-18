namespace Winhance.Core.Features.Common.Catalog;

// Decides what counts as a recoverable REG_BINARY buffer without ever discarding data we could read. Getting it
// wrong destroys user data silently: a wrongly-typed UserPreferencesMask once reset every preference packed into it.
public static class BinaryValueRecovery
{
    // The length Windows uses for these values; the floor for a fresh array.
    public const int MinimumLength = 12;

    // Null means the caller must REFUSE the write - never fall back to a zeroed array.
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

    // Only used to log the repair; never changes what is written.
    public static bool IsRecoveredFromString(object? currentValue) => currentValue is string;
}

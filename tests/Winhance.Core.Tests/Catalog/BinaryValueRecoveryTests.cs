using System.Text;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

// Guards a DATA-LOSS bug: "present but not a byte[]" once read as "absent" and was overwritten with a zeroed
// array, wiping every unrelated bit packed into the same value.
public class BinaryValueRecoveryTests
{
    private static readonly string[] MultiString = ["a", "b"];

    [Fact]
    public void Resolve_ExistingByteArray_ReturnsItUnchanged()
    {
        var current = new byte[] { 0x9E, 0x3E, 0x03, 0x80, 0x12, 0x00, 0x00, 0x00 };
        var buffer = BinaryValueRecovery.Resolve(current, byteIndex: 1);
        Assert.Same(current, buffer);
    }

    [Fact]
    public void Resolve_AbsentValue_ReturnsFreshZeroedBuffer()
    {
        var buffer = BinaryValueRecovery.Resolve(null, byteIndex: 1);
        Assert.NotNull(buffer);
        Assert.Equal(BinaryValueRecovery.MinimumLength, buffer!.Length);
        Assert.All(buffer, b => Assert.Equal(0, b));
    }

    // The real case: UserPreferencesMask stored as REG_EXPAND_SZ; the bytes were intact (a registry string is
    // UTF-16LE), displayed as U+1290 U+8003. Recovery must return those exact bytes, NOT zeros.
    [Fact]
    public void Resolve_StringValue_RecoversUnderlyingUtf16Bytes()
    {
        var original = new byte[] { 0x90, 0x12, 0x03, 0x80, 0x12, 0x00, 0x00, 0x00 };

        // Exactly what RegistryKey.GetValue hands back for these bytes under a string type: decoded as
        // UTF-16LE and truncated at the 00 00 terminator.
        string asStoredString = Encoding.Unicode.GetString(original).TrimEnd('\0');

        var buffer = BinaryValueRecovery.Resolve(asStoredString, byteIndex: 1);

        Assert.NotNull(buffer);
        // The leading bytes survive verbatim; the trailing zeros the terminator swallowed are restored by
        // the zero padding, so the whole original value round-trips.
        Assert.Equal(original, buffer!.AsSpan(0, original.Length).ToArray());
    }

    [Fact]
    public void Resolve_RecoveredString_BufferCoversEditAndMinimumLength()
    {
        var buffer = BinaryValueRecovery.Resolve("ab", byteIndex: 20);
        Assert.NotNull(buffer);
        Assert.True(buffer!.Length >= 21, "buffer must cover the byte about to be edited");
        Assert.True(buffer.Length >= BinaryValueRecovery.MinimumLength);
    }

    [Fact]
    public void Resolve_EmptyString_ReturnsUsableBuffer()
    {
        var buffer = BinaryValueRecovery.Resolve(string.Empty, byteIndex: 0);
        Assert.NotNull(buffer);
        Assert.Equal(BinaryValueRecovery.MinimumLength, buffer!.Length);
    }

    [Theory]
    [InlineData(1)]                       // a DWORD sitting where REG_BINARY belongs
    [InlineData(1L)]
    [InlineData(true)]
    public void Resolve_UnrecoverableType_ReturnsNull(object current)
    {
        Assert.Null(BinaryValueRecovery.Resolve(current, byteIndex: 1));
    }

    [Fact]
    public void Resolve_MultiString_ReturnsNull()
    {
        Assert.Null(BinaryValueRecovery.Resolve(MultiString, byteIndex: 1));
    }

    [Fact]
    public void IsRecoveredFromString_OnlyString_IsRecovered()
    {
        Assert.True(BinaryValueRecovery.IsRecoveredFromString("x"));
        Assert.False(BinaryValueRecovery.IsRecoveredFromString(null));
        Assert.False(BinaryValueRecovery.IsRecoveredFromString(new byte[] { 1 }));
        Assert.False(BinaryValueRecovery.IsRecoveredFromString(1));
    }

    [Fact]
    public void Resolve_EditingRecoveredBuffer_PreservesUnrelatedBytes()
    {
        var original = new byte[] { 0x90, 0x12, 0x03, 0x80, 0x12, 0x00, 0x00, 0x00 };
        string asStoredString = Encoding.Unicode.GetString(original).TrimEnd('\0');

        var buffer = BinaryValueRecovery.Resolve(asStoredString, byteIndex: 1)!;
        buffer[1] |= 0x08; // set fade-tooltip's bit, as ModifyBinaryBit would

        Assert.Equal(0x90, buffer[0]);
        Assert.Equal(0x12 | 0x08, buffer[1]);
        Assert.Equal(0x03, buffer[2]);
        Assert.Equal(0x80, buffer[3]);            // untouched - a byte Winhance does not even manage
        Assert.Equal(0x12, buffer[4]);
    }
}

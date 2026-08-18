using System;
using System.Text;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

/// <summary>
/// The recover-or-refuse rule for surgical REG_BINARY edits. These guard a DATA-LOSS bug, not a cosmetic
/// one: the old code treated "present but not a byte[]" as "absent" and wrote a zeroed array, wiping every
/// unrelated bit packed into the same value.
/// </summary>
public class BinaryValueRecoveryTests
{
    private static readonly string[] MultiString = ["a", "b"];

    [Fact]
    public void Existing_byte_array_is_used_as_is()
    {
        var current = new byte[] { 0x9E, 0x3E, 0x03, 0x80, 0x12, 0x00, 0x00, 0x00 };
        var buffer = BinaryValueRecovery.Resolve(current, byteIndex: 1);
        Assert.Same(current, buffer);
    }

    [Fact]
    public void Absent_value_yields_a_fresh_zeroed_buffer()
    {
        var buffer = BinaryValueRecovery.Resolve(null, byteIndex: 1);
        Assert.NotNull(buffer);
        Assert.Equal(BinaryValueRecovery.MinimumLength, buffer!.Length);
        Assert.All(buffer, b => Assert.Equal(0, b));
    }

    /// <summary>The real case from Marco's machine (2026-07-27): UserPreferencesMask stored as
    /// REG_EXPAND_SZ. The bytes were intact - a registry string is UTF-16LE - and displayed as the glyphs
    /// U+1290 U+8003. Recovery must return those exact bytes, NOT zeros.
    /// </summary>
    [Fact]
    public void String_value_is_recovered_to_its_underlying_utf16_bytes()
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
    public void Recovered_string_buffer_is_long_enough_for_the_edit_and_the_minimum()
    {
        var buffer = BinaryValueRecovery.Resolve("ab", byteIndex: 20);
        Assert.NotNull(buffer);
        Assert.True(buffer!.Length >= 21, "buffer must cover the byte about to be edited");
        Assert.True(buffer.Length >= BinaryValueRecovery.MinimumLength);
    }

    [Fact]
    public void Empty_string_still_yields_a_usable_buffer_rather_than_a_refusal()
    {
        var buffer = BinaryValueRecovery.Resolve(string.Empty, byteIndex: 0);
        Assert.NotNull(buffer);
        Assert.Equal(BinaryValueRecovery.MinimumLength, buffer!.Length);
    }

    [Theory]
    [InlineData(1)]                       // a DWORD sitting where REG_BINARY belongs
    [InlineData(1L)]
    [InlineData(true)]
    public void Unrecoverable_types_refuse_instead_of_guessing(object current)
    {
        Assert.Null(BinaryValueRecovery.Resolve(current, byteIndex: 1));
    }

    [Fact]
    public void Multi_string_refuses_too()
    {
        Assert.Null(BinaryValueRecovery.Resolve(MultiString, byteIndex: 1));
    }

    [Fact]
    public void Only_a_string_counts_as_recovered_for_logging()
    {
        Assert.True(BinaryValueRecovery.IsRecoveredFromString("x"));
        Assert.False(BinaryValueRecovery.IsRecoveredFromString(null));
        Assert.False(BinaryValueRecovery.IsRecoveredFromString(new byte[] { 1 }));
        Assert.False(BinaryValueRecovery.IsRecoveredFromString(1));
    }

    /// <summary>The whole point, stated as an assertion: recovering and then editing one bit must leave
    /// every other byte exactly as it was. This is what the old zeroing behaviour destroyed.</summary>
    [Fact]
    public void Editing_a_recovered_buffer_preserves_every_unrelated_byte()
    {
        var original = new byte[] { 0x90, 0x12, 0x03, 0x80, 0x12, 0x00, 0x00, 0x00 };
        string asStoredString = Encoding.Unicode.GetString(original).TrimEnd('\0');

        var buffer = BinaryValueRecovery.Resolve(asStoredString, byteIndex: 1)!;
        buffer[1] |= 0x08; // set fade-tooltip's bit, as ModifyBinaryBit would

        Assert.Equal(0x90, buffer[0]);            // untouched
        Assert.Equal(0x12 | 0x08, buffer[1]);     // the one edited byte
        Assert.Equal(0x03, buffer[2]);            // untouched
        Assert.Equal(0x80, buffer[3]);            // untouched - a byte Winhance does not even manage
        Assert.Equal(0x12, buffer[4]);            // untouched
    }
}

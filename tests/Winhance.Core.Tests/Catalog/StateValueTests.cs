using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class StateValueTests
{
    [Fact]
    public void Of_matches_equal_present_value()
    {
        var v = StateValue.Of(3);
        Assert.True(v.Matches(3, present: true));
        Assert.False(v.Matches(4, present: true));
        Assert.False(v.Matches(null, present: false));
    }

    [Fact]
    public void Of_matches_numerically_across_boxed_types()
    {
        var v = StateValue.Of(3);
        Assert.True(v.Matches(3L, present: true));
        Assert.True(v.Matches((byte)3, present: true));
    }

    [Fact]
    public void OrAbsent_matches_both_value_and_absent()
    {
        var v = StateValue.Of(3).OrAbsent();
        Assert.True(v.Matches(3, present: true));
        Assert.True(v.Matches(null, present: false));
        Assert.False(v.Matches(4, present: true));
    }

    [Fact]
    public void Absent_matches_only_when_absent_and_is_a_delete()
    {
        var v = StateValue.Absent;
        Assert.True(v.Matches(null, present: false));
        Assert.False(v.Matches(0, present: true));
        Assert.True(v.DeleteOnWrite);
    }

    [Fact]
    public void Exists_matches_any_present_reading()
    {
        var v = StateValue.Exists;
        Assert.True(v.Matches("anything", present: true));
        Assert.True(v.Matches(0, present: true));
        Assert.False(v.Matches(null, present: false));
    }

    [Fact]
    public void Of_compares_byte_arrays_by_content_not_reference()
    {
        // REG_BINARY selection values (e.g. explorer-customization-shortcut-suffix) are raw byte arrays.
        // Equal content must match; different content must not - never "all byte[] are equal".
        var v = StateValue.Of(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        Assert.True(v.Matches(new byte[] { 0x00, 0x00, 0x00, 0x00 }, present: true));
        Assert.False(v.Matches(new byte[] { 0x1E, 0x00, 0x00, 0x00 }, present: true));
        Assert.False(v.Matches(null, present: false));
    }

    [Fact]
    public void OneOf_matches_any_listed_value()
    {
        var v = StateValue.OneOf(2, 0x26);
        Assert.True(v.Matches(2, present: true));
        Assert.True(v.Matches(0x26, present: true));
        Assert.False(v.Matches(1, present: true));
    }

    [Fact]
    public void WritePayload_is_the_first_concrete_value()
    {
        Assert.Equal(3, StateValue.Of(3).WritePayload);
        Assert.Equal(3, StateValue.Of(3).OrAbsent().WritePayload);
        Assert.Equal(2, StateValue.OneOf(2, 0x26).WritePayload);
    }

    [Fact]
    public void OneOf_with_all_null_throws_to_fail_loudly_on_a_bad_migration()
    {
        Assert.Throws<System.ArgumentException>(() => StateValue.OneOf(null, null));
    }
}

using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class StateValueTests
{
    [Fact]
    public void Matches_OfWithEqualPresentValue_IsTrue()
    {
        var v = StateValue.Of(3);
        Assert.True(v.Matches(3, present: true));
        Assert.False(v.Matches(4, present: true));
        Assert.False(v.Matches(null, present: false));
    }

    [Fact]
    public void Matches_OfAcrossBoxedNumericTypes_MatchesNumerically()
    {
        var v = StateValue.Of(3);
        Assert.True(v.Matches(3L, present: true));
        Assert.True(v.Matches((byte)3, present: true));
    }

    [Fact]
    public void Matches_OrAbsent_MatchesBothValueAndAbsent()
    {
        var v = StateValue.Of(3).OrAbsent();
        Assert.True(v.Matches(3, present: true));
        Assert.True(v.Matches(null, present: false));
        Assert.False(v.Matches(4, present: true));
    }

    [Fact]
    public void Matches_Absent_MatchesOnlyWhenAbsentAndIsADelete()
    {
        var v = StateValue.Absent;
        Assert.True(v.Matches(null, present: false));
        Assert.False(v.Matches(0, present: true));
        Assert.True(v.DeleteOnWrite);
    }

    [Fact]
    public void Matches_Exists_MatchesAnyPresentReading()
    {
        var v = StateValue.Exists;
        Assert.True(v.Matches("anything", present: true));
        Assert.True(v.Matches(0, present: true));
        Assert.False(v.Matches(null, present: false));
    }

    [Fact]
    public void Matches_OfByteArray_ComparesByContentNotReference()
    {
        // REG_BINARY selection values (e.g. explorer-customization-shortcut-suffix) are raw byte arrays.
        // Equal content must match; different content must not - never "all byte[] are equal".
        var v = StateValue.Of(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        Assert.True(v.Matches(new byte[] { 0x00, 0x00, 0x00, 0x00 }, present: true));
        Assert.False(v.Matches(new byte[] { 0x1E, 0x00, 0x00, 0x00 }, present: true));
        Assert.False(v.Matches(null, present: false));
    }

    [Fact]
    public void Matches_OneOf_MatchesAnyListedValue()
    {
        var v = StateValue.OneOf(2, 0x26);
        Assert.True(v.Matches(2, present: true));
        Assert.True(v.Matches(0x26, present: true));
        Assert.False(v.Matches(1, present: true));
    }

    [Fact]
    public void WritePayload_Always_IsTheFirstConcreteValue()
    {
        Assert.Equal(3, StateValue.Of(3).WritePayload);
        Assert.Equal(3, StateValue.Of(3).OrAbsent().WritePayload);
        Assert.Equal(2, StateValue.OneOf(2, 0x26).WritePayload);
    }

    [Fact]
    public void OneOf_AllNull_ThrowsToFailLoudlyOnABadMigration()
    {
        Assert.Throws<System.ArgumentException>(() => StateValue.OneOf(null, null));
    }
}

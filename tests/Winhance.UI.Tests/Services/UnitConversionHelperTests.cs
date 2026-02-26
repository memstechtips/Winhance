using FluentAssertions;
using Winhance.UI.Features.Common.Utilities;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class UnitConversionHelperTests
{
    // -------------------------------------------------------
    // ConvertFromSystemUnits - "minutes" divides by 60
    // -------------------------------------------------------

    [Fact]
    public void ConvertFromSystemUnits_Minutes_DividesBy60()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(1200, "minutes");

        result.Should().Be(20);
    }

    [Fact]
    public void ConvertFromSystemUnits_Minutes_60SecondsReturns1()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(60, "minutes");

        result.Should().Be(1);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(60, 1)]
    [InlineData(120, 2)]
    [InlineData(300, 5)]
    [InlineData(3600, 60)]
    public void ConvertFromSystemUnits_Minutes_VariousValues(int systemValue, int expected)
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(systemValue, "minutes");

        result.Should().Be(expected);
    }

    // -------------------------------------------------------
    // ConvertFromSystemUnits - "hours" divides by 3600
    // -------------------------------------------------------

    [Fact]
    public void ConvertFromSystemUnits_Hours_DividesBy3600()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(7200, "hours");

        result.Should().Be(2);
    }

    [Fact]
    public void ConvertFromSystemUnits_Hours_3600SecondsReturns1()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(3600, "hours");

        result.Should().Be(1);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3600, 1)]
    [InlineData(7200, 2)]
    [InlineData(36000, 10)]
    public void ConvertFromSystemUnits_Hours_VariousValues(int systemValue, int expected)
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(systemValue, "hours");

        result.Should().Be(expected);
    }

    // -------------------------------------------------------
    // ConvertFromSystemUnits - "milliseconds" multiplies by 1000
    // -------------------------------------------------------

    [Fact]
    public void ConvertFromSystemUnits_Milliseconds_MultipliesBy1000()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(5, "milliseconds");

        result.Should().Be(5000);
    }

    [Fact]
    public void ConvertFromSystemUnits_Milliseconds_1Returns1000()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(1, "milliseconds");

        result.Should().Be(1000);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1000)]
    [InlineData(10, 10000)]
    [InlineData(100, 100000)]
    public void ConvertFromSystemUnits_Milliseconds_VariousValues(int systemValue, int expected)
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(systemValue, "milliseconds");

        result.Should().Be(expected);
    }

    // -------------------------------------------------------
    // ConvertFromSystemUnits - null/unknown units pass through
    // -------------------------------------------------------

    [Fact]
    public void ConvertFromSystemUnits_NullUnits_ReturnsValueUnchanged()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(42, null);

        result.Should().Be(42);
    }

    [Fact]
    public void ConvertFromSystemUnits_UnknownUnits_ReturnsValueUnchanged()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(42, "fathoms");

        result.Should().Be(42);
    }

    [Fact]
    public void ConvertFromSystemUnits_EmptyString_ReturnsValueUnchanged()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(42, "");

        result.Should().Be(42);
    }

    [Theory]
    [InlineData("seconds")]
    [InlineData("bytes")]
    [InlineData("percent")]
    [InlineData("unknown")]
    public void ConvertFromSystemUnits_UnsupportedUnits_PassThrough(string units)
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(99, units);

        result.Should().Be(99);
    }

    // -------------------------------------------------------
    // ConvertFromSystemUnits - edge cases: zero
    // -------------------------------------------------------

    [Fact]
    public void ConvertFromSystemUnits_ZeroWithMinutes_ReturnsZero()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(0, "minutes");

        result.Should().Be(0);
    }

    [Fact]
    public void ConvertFromSystemUnits_ZeroWithHours_ReturnsZero()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(0, "hours");

        result.Should().Be(0);
    }

    [Fact]
    public void ConvertFromSystemUnits_ZeroWithMilliseconds_ReturnsZero()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(0, "milliseconds");

        result.Should().Be(0);
    }

    [Fact]
    public void ConvertFromSystemUnits_ZeroWithNull_ReturnsZero()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(0, null);

        result.Should().Be(0);
    }

    // -------------------------------------------------------
    // ConvertFromSystemUnits - edge cases: negative values
    // -------------------------------------------------------

    [Fact]
    public void ConvertFromSystemUnits_NegativeWithMinutes_DividesCorrectly()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(-120, "minutes");

        result.Should().Be(-2);
    }

    [Fact]
    public void ConvertFromSystemUnits_NegativeWithHours_DividesCorrectly()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(-7200, "hours");

        result.Should().Be(-2);
    }

    [Fact]
    public void ConvertFromSystemUnits_NegativeWithMilliseconds_MultipliesCorrectly()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(-5, "milliseconds");

        result.Should().Be(-5000);
    }

    [Fact]
    public void ConvertFromSystemUnits_NegativeWithNull_ReturnsUnchanged()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(-42, null);

        result.Should().Be(-42);
    }

    // -------------------------------------------------------
    // ConvertFromSystemUnits - case sensitivity
    // -------------------------------------------------------

    [Fact]
    public void ConvertFromSystemUnits_UppercaseMinutes_StillConverts()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(120, "Minutes");

        result.Should().Be(2);
    }

    [Fact]
    public void ConvertFromSystemUnits_MixedCaseMinutes_StillConverts()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(120, "MINUTES");

        result.Should().Be(2);
    }

    [Fact]
    public void ConvertFromSystemUnits_UppercaseHours_StillConverts()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(7200, "Hours");

        result.Should().Be(2);
    }

    [Fact]
    public void ConvertFromSystemUnits_UppercaseMilliseconds_StillConverts()
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(5, "Milliseconds");

        result.Should().Be(5000);
    }

    [Theory]
    [InlineData("MINUTES", 120, 2)]
    [InlineData("Minutes", 120, 2)]
    [InlineData("mInUtEs", 120, 2)]
    [InlineData("HOURS", 3600, 1)]
    [InlineData("Hours", 3600, 1)]
    [InlineData("MILLISECONDS", 5, 5000)]
    [InlineData("Milliseconds", 5, 5000)]
    public void ConvertFromSystemUnits_CaseInsensitive(string units, int systemValue, int expected)
    {
        var result = UnitConversionHelper.ConvertFromSystemUnits(systemValue, units);

        result.Should().Be(expected);
    }

    // -------------------------------------------------------
    // ConvertFromSystemUnits - integer division truncation
    // -------------------------------------------------------

    [Fact]
    public void ConvertFromSystemUnits_Minutes_IntegerDivisionTruncates()
    {
        // 59 / 60 = 0 (integer division)
        var result = UnitConversionHelper.ConvertFromSystemUnits(59, "minutes");

        result.Should().Be(0);
    }

    [Fact]
    public void ConvertFromSystemUnits_Hours_IntegerDivisionTruncates()
    {
        // 3599 / 3600 = 0 (integer division)
        var result = UnitConversionHelper.ConvertFromSystemUnits(3599, "hours");

        result.Should().Be(0);
    }

    [Fact]
    public void ConvertFromSystemUnits_Minutes_PartialValueTruncates()
    {
        // 90 / 60 = 1 (integer division, 1.5 truncated to 1)
        var result = UnitConversionHelper.ConvertFromSystemUnits(90, "minutes");

        result.Should().Be(1);
    }
}

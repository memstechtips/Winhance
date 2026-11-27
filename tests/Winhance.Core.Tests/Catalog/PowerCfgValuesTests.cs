using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class PowerCfgValuesTests
{
    [Fact]
    public void ToDisplay_Minutes_DividesBy60()
    {
        var result = PowerCfgValues.ToDisplay(1200, "minutes");

        result.Should().Be(20);
    }

    [Fact]
    public void ToDisplay_Minutes_60SecondsReturns1()
    {
        var result = PowerCfgValues.ToDisplay(60, "minutes");

        result.Should().Be(1);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(60, 1)]
    [InlineData(120, 2)]
    [InlineData(300, 5)]
    [InlineData(3600, 60)]
    public void ToDisplay_Minutes_VariousValues(int systemValue, int expected)
    {
        var result = PowerCfgValues.ToDisplay(systemValue, "minutes");

        result.Should().Be(expected);
    }

    [Fact]
    public void ToDisplay_Hours_DividesBy3600()
    {
        var result = PowerCfgValues.ToDisplay(7200, "hours");

        result.Should().Be(2);
    }

    [Fact]
    public void ToDisplay_Hours_3600SecondsReturns1()
    {
        var result = PowerCfgValues.ToDisplay(3600, "hours");

        result.Should().Be(1);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3600, 1)]
    [InlineData(7200, 2)]
    [InlineData(36000, 10)]
    public void ToDisplay_Hours_VariousValues(int systemValue, int expected)
    {
        var result = PowerCfgValues.ToDisplay(systemValue, "hours");

        result.Should().Be(expected);
    }

    // "milliseconds" passes through 1:1: Win32 powercfg stores the USB selective suspend timeout natively in ms.

    [Fact]
    public void ToDisplay_Milliseconds_PassesThroughUnchanged()
    {
        var result = PowerCfgValues.ToDisplay(5, "milliseconds");

        result.Should().Be(5);
    }

    [Fact]
    public void ToDisplay_Milliseconds_1Returns1()
    {
        var result = PowerCfgValues.ToDisplay(1, "milliseconds");

        result.Should().Be(1);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    [InlineData(100, 100)]
    [InlineData(1000, 1000)]
    public void ToDisplay_Milliseconds_VariousValues(int systemValue, int expected)
    {
        var result = PowerCfgValues.ToDisplay(systemValue, "milliseconds");

        result.Should().Be(expected);
    }

    [Fact]
    public void ToDisplay_NullUnits_ReturnsValueUnchanged()
    {
        var result = PowerCfgValues.ToDisplay(42, null);

        result.Should().Be(42);
    }

    [Fact]
    public void ToDisplay_UnknownUnits_ReturnsValueUnchanged()
    {
        var result = PowerCfgValues.ToDisplay(42, "fathoms");

        result.Should().Be(42);
    }

    [Fact]
    public void ToDisplay_EmptyString_ReturnsValueUnchanged()
    {
        var result = PowerCfgValues.ToDisplay(42, "");

        result.Should().Be(42);
    }

    [Theory]
    [InlineData("seconds")]
    [InlineData("bytes")]
    [InlineData("percent")]
    [InlineData("unknown")]
    public void ToDisplay_UnsupportedUnits_PassThrough(string units)
    {
        var result = PowerCfgValues.ToDisplay(99, units);

        result.Should().Be(99);
    }

    [Fact]
    public void ToDisplay_ZeroWithMinutes_ReturnsZero()
    {
        var result = PowerCfgValues.ToDisplay(0, "minutes");

        result.Should().Be(0);
    }

    [Fact]
    public void ToDisplay_ZeroWithHours_ReturnsZero()
    {
        var result = PowerCfgValues.ToDisplay(0, "hours");

        result.Should().Be(0);
    }

    [Fact]
    public void ToDisplay_ZeroWithMilliseconds_ReturnsZero()
    {
        var result = PowerCfgValues.ToDisplay(0, "milliseconds");

        result.Should().Be(0);
    }

    [Fact]
    public void ToDisplay_ZeroWithNull_ReturnsZero()
    {
        var result = PowerCfgValues.ToDisplay(0, null);

        result.Should().Be(0);
    }

    [Fact]
    public void ToDisplay_NegativeWithMinutes_DividesCorrectly()
    {
        var result = PowerCfgValues.ToDisplay(-120, "minutes");

        result.Should().Be(-2);
    }

    [Fact]
    public void ToDisplay_NegativeWithHours_DividesCorrectly()
    {
        var result = PowerCfgValues.ToDisplay(-7200, "hours");

        result.Should().Be(-2);
    }

    [Fact]
    public void ToDisplay_NegativeWithMilliseconds_PassesThroughUnchanged()
    {
        var result = PowerCfgValues.ToDisplay(-5, "milliseconds");

        result.Should().Be(-5);
    }

    [Fact]
    public void ToDisplay_NegativeWithNull_ReturnsUnchanged()
    {
        var result = PowerCfgValues.ToDisplay(-42, null);

        result.Should().Be(-42);
    }

    [Fact]
    public void ToDisplay_UppercaseMinutes_StillConverts()
    {
        var result = PowerCfgValues.ToDisplay(120, "Minutes");

        result.Should().Be(2);
    }

    [Fact]
    public void ToDisplay_MixedCaseMinutes_StillConverts()
    {
        var result = PowerCfgValues.ToDisplay(120, "MINUTES");

        result.Should().Be(2);
    }

    [Fact]
    public void ToDisplay_UppercaseHours_StillConverts()
    {
        var result = PowerCfgValues.ToDisplay(7200, "Hours");

        result.Should().Be(2);
    }

    [Fact]
    public void ToDisplay_UppercaseMilliseconds_StillPassesThrough()
    {
        var result = PowerCfgValues.ToDisplay(5, "Milliseconds");

        result.Should().Be(5);
    }

    [Theory]
    [InlineData("MINUTES", 120, 2)]
    [InlineData("Minutes", 120, 2)]
    [InlineData("mInUtEs", 120, 2)]
    [InlineData("HOURS", 3600, 1)]
    [InlineData("Hours", 3600, 1)]
    [InlineData("MILLISECONDS", 5, 5)]
    [InlineData("Milliseconds", 5, 5)]
    public void ToDisplay_CaseInsensitive(string units, int systemValue, int expected)
    {
        var result = PowerCfgValues.ToDisplay(systemValue, units);

        result.Should().Be(expected);
    }

    [Fact]
    public void ToDisplay_Minutes_IntegerDivisionTruncates()
    {
        var result = PowerCfgValues.ToDisplay(59, "minutes");

        result.Should().Be(0);
    }

    [Fact]
    public void ToDisplay_Hours_IntegerDivisionTruncates()
    {
        var result = PowerCfgValues.ToDisplay(3599, "hours");

        result.Should().Be(0);
    }

    [Fact]
    public void ToDisplay_Minutes_PartialValueTruncates()
    {
        var result = PowerCfgValues.ToDisplay(90, "minutes");

        result.Should().Be(1);
    }

    [Theory]
    [InlineData(1200, "minutes")]
    [InlineData(7200, "hours")]
    [InlineData(1000, "milliseconds")]
    [InlineData(42, null)]
    public void ToSystem_AfterToDisplay_RoundTripsWholeUnits(int systemValue, string? units)
    {
        PowerCfgValues.ToSystem(PowerCfgValues.ToDisplay(systemValue, units), units).Should().Be(systemValue);
    }

    [Fact]
    public void OptionIndex_ReadingMatchesAnOption_ReturnsThatOption()
    {
        PowerCfgValues.OptionIndex(PowerSelection(), 600).Should().Be(1);
    }

    [Fact]
    public void OptionIndex_ReadingMatchesNoOption_ReturnsNull()
    {
        PowerCfgValues.OptionIndex(PowerSelection(), 480).Should().BeNull();
    }

    [Fact]
    public void OptionIndex_NoReading_ReturnsNull()
    {
        PowerCfgValues.OptionIndex(PowerSelection(), null).Should().BeNull();
    }

    private static Setting PowerSelection() => new()
    {
        Id = "power-selection",
        Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d") },
        Targets = new Target[] { new PowerCfgTarget("Power", "sub-guid", "set-guid", PowerModeSupport.Separate) },
        States = new[]
        {
            new SettingState { Label = TestKeys.Of("Never"), Set = new Dictionary<string, StateValue> { ["Power"] = StateValue.Of(0) } },
            new SettingState { Label = TestKeys.Of("Ten"), Set = new Dictionary<string, StateValue> { ["Power"] = StateValue.Of(600) } },
        },
    };
}

using System.Text.Json;
using FluentAssertions;
using Winhance.Infrastructure.Features.Common.Utilities;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class NumericConversionHelperTests
{
    [Fact]
    public void ConvertNumericValue_Int_ReturnsAsIs()
    {
        NumericConversionHelper.ConvertNumericValue(42).Should().Be(42);
    }

    [Fact]
    public void ConvertNumericValue_Long_CastsToInt()
    {
        NumericConversionHelper.ConvertNumericValue(100L).Should().Be(100);
    }

    [Fact]
    public void ConvertNumericValue_Double_CastsToInt()
    {
        NumericConversionHelper.ConvertNumericValue(3.7).Should().Be(3);
    }

    [Fact]
    public void ConvertNumericValue_Float_CastsToInt()
    {
        NumericConversionHelper.ConvertNumericValue(5.9f).Should().Be(5);
    }

    [Fact]
    public void ConvertNumericValue_StringParseable_Parses()
    {
        NumericConversionHelper.ConvertNumericValue("123").Should().Be(123);
    }

    [Fact]
    public void ConvertNumericValue_StringNonNumeric_ThrowsArgument()
    {
        var action = () => NumericConversionHelper.ConvertNumericValue("abc");
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConvertNumericValue_JsonElement_Parses()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("42");
        NumericConversionHelper.ConvertNumericValue(json).Should().Be(42);
    }

    [Fact]
    public void ConvertNumericValue_UnsupportedType_ThrowsArgument()
    {
        var action = () => NumericConversionHelper.ConvertNumericValue(new object());
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConvertNumericValue_ZeroInt_ReturnsZero()
    {
        NumericConversionHelper.ConvertNumericValue(0).Should().Be(0);
    }

    [Fact]
    public void ConvertNumericValue_NegativeInt_ReturnsNegative()
    {
        NumericConversionHelper.ConvertNumericValue(-5).Should().Be(-5);
    }

    [Fact]
    public void ConvertNumericValue_NegativeString_Parses()
    {
        NumericConversionHelper.ConvertNumericValue("-10").Should().Be(-10);
    }
}

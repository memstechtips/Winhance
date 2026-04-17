using FluentAssertions;
using Winhance.UI.Features.Common.Converters;
using Xunit;

namespace Winhance.UI.Tests.Converters;

public class BoolToDimOpacityConverterTests
{
    [Fact]
    public void Convert_True_ReturnsOne()
    {
        var sut = new BoolToDimOpacityConverter();
        var result = sut.Convert(true, typeof(double), null, "");
        result.Should().Be(1.0);
    }

    [Fact]
    public void Convert_False_ReturnsDimConstant()
    {
        var sut = new BoolToDimOpacityConverter();
        var result = sut.Convert(false, typeof(double), null, "");
        result.Should().Be(0.35);
    }

    [Fact]
    public void Convert_Null_ReturnsOne()
    {
        // Defensive default — unexpected input renders highlighted rather than invisible.
        var sut = new BoolToDimOpacityConverter();
        var result = sut.Convert(null!, typeof(double), null, "");
        result.Should().Be(1.0);
    }
}

using FluentAssertions;
using Winhance.UI.Features.Common.Converters;
using Xunit;

namespace Winhance.UI.Tests.Converters;

public class BadgeStateToForegroundConverterTests
{
    private readonly BadgeStateToForegroundConverter _sut = new();

    [Fact]
    public void Convert_NonEnumValue_ReturnsNull()
    {
        var result = _sut.Convert(42, typeof(object), null!, "en");
        result.Should().BeNull();
    }

    [Fact]
    public void ConvertBack_Throws()
    {
        var act = () => _sut.ConvertBack(null!, typeof(object), null!, "en");
        act.Should().Throw<NotImplementedException>();
    }
}

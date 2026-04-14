using FluentAssertions;
using Winhance.Core.Features.Common.Enums;
using Winhance.UI.Features.Common.Converters;
using Xunit;

namespace Winhance.UI.Tests.Converters;

public class BadgeKindToStyleConverterTests
{
    private readonly BadgeKindToStyleConverter _sut = new();

    [Fact]
    public void Convert_NonEnumValue_ReturnsNull()
    {
        var result = _sut.Convert("not an enum", typeof(object), null!, "en");
        result.Should().BeNull();
    }

    [Fact]
    public void ConvertBack_Throws()
    {
        var act = () => _sut.ConvertBack(null!, typeof(object), null!, "en");
        act.Should().Throw<NotImplementedException>();
    }

    [Theory]
    [InlineData(SettingBadgeKind.Recommended, "BadgeRecommendedStyle")]
    [InlineData(SettingBadgeKind.Default, "BadgeDefaultStyle")]
    [InlineData(SettingBadgeKind.Custom, "BadgeCustomStyle")]
    [InlineData(SettingBadgeKind.Preference, "BadgePreferenceStyle")]
    public void GetResourceKey_ReturnsMatchingStyleKey(SettingBadgeKind state, string expected)
    {
        BadgeKindToStyleConverter.GetResourceKey(state).Should().Be(expected);
    }

    [Fact]
    public void GetResourceKey_OutOfRangeEnumValue_ReturnsNull()
    {
        // Guard against a new SettingBadgeKind value being added without updating the switch.
        var invalid = (SettingBadgeKind)999;
        BadgeKindToStyleConverter.GetResourceKey(invalid).Should().BeNull();
    }
}

using FluentAssertions;
using Winhance.Core.Features.Common.Enums;
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

    [Theory]
    [InlineData(SettingBadgeState.Recommended, "BadgeRecommendedForeground")]
    [InlineData(SettingBadgeState.Default, "BadgeDefaultForeground")]
    [InlineData(SettingBadgeState.Custom, "BadgeCustomForeground")]
    [InlineData(SettingBadgeState.Preference, "BadgePreferenceForeground")]
    public void GetResourceKey_ReturnsMatchingForegroundKey(SettingBadgeState state, string expected)
    {
        BadgeStateToForegroundConverter.GetResourceKey(state).Should().Be(expected);
    }

    [Fact]
    public void GetResourceKey_OutOfRangeEnumValue_ReturnsNull()
    {
        // Guard against a new SettingBadgeState value being added without updating the switch.
        var invalid = (SettingBadgeState)999;
        BadgeStateToForegroundConverter.GetResourceKey(invalid).Should().BeNull();
    }
}
